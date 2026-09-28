using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Ocwip.Api.Admin;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;

namespace Ocwip.Api.Tests.Admin;

/// <summary>
/// import-content (T-96): the starting content of a competition from the files
/// in backend/seed/, through the same contract gate and the same service as
/// the operator's screens, safe to run twice, and all or nothing.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ImportContentCommandTests(PostgresDatabaseFixture database)
{
    private static string Seed(params string[] parts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine([directory.FullName, "seed", .. parts]);
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException(string.Join('/', parts));
    }

    private static readonly string Application = Seed("forms", "application-2026.json");
    private static readonly string Formal = Seed("evaluation-cards", "formal-2026.json");
    private static readonly string Merit = Seed("evaluation-cards", "merit-2026.json");
    private static readonly string Contract = Seed("templates", "contract-2026.txt");

    private IConfiguration Configuration => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Postgres"] = database.ConnectionString })
        .Build();

    private async Task<Guid> DraftCompetitionAsync()
    {
        await using var context = database.CreateContext();
        var competition = TestCompetition.New();
        competition.Status = CompetitionStatus.Draft;
        context.Competitions.Add(competition);
        await context.SaveChangesAsync();
        return competition.Id;
    }

    private async Task<(int Exit, string Output)> RunAsync(params string[] args)
    {
        await using var output = new StringWriter();
        var exit = await AdminCommandRunner.RunAsync(args, Configuration, output);
        return (exit, output.ToString());
    }

    private async Task<int> VersionsAsync(Guid competitionId)
    {
        await using var context = database.CreateContext();
        return await context.FormDefinitions.CountAsync(x => x.CompetitionId == competitionId);
    }

    [RequiresDatabaseFact]
    public async Task A_new_competition_gets_its_form_both_cards_and_the_report_form_and_can_be_published()
    {
        var id = await DraftCompetitionAsync();
        var report = Path.Combine(Path.GetTempPath(), $"report-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(report, ReportFormSamples.Report().GetRawText());

        var (exit, output) = await RunAsync(
            "import-content", "--competition", id.ToString(),
            "--application", Application, "--formal", Formal, "--merit", Merit, "--report", report);

        Assert.Equal(AdminCommandRunner.Success, exit);
        Assert.Contains("Published the application form as version 1.", output);
        await using var context = database.CreateContext();
        var competition = await context.Competitions.SingleAsync(x => x.Id == id);
        Assert.NotNull(competition.FormDefinitionId);
        Assert.NotNull(competition.FormalCardDefinitionId);
        Assert.NotNull(competition.MeritCardDefinitionId);
        Assert.NotNull(competition.ReportFormDefinitionId);
        Assert.Empty(Ocwip.Api.Services.CompetitionService.PublicationGaps(competition));
    }

    [RequiresDatabaseFact]
    public async Task Running_it_again_with_the_same_files_changes_nothing_and_says_so()
    {
        var id = await DraftCompetitionAsync();
        string[] args = ["import-content", "--competition", id.ToString(), "--application", Application, "--formal", Formal];
        await RunAsync(args);

        var (exit, output) = await RunAsync(args);

        Assert.Equal(AdminCommandRunner.Success, exit);
        Assert.Contains("The application form is already version 1. Nothing changed.", output);
        Assert.Contains("The formal evaluation card is already version 1. Nothing changed.", output);
        Assert.Equal(2, await VersionsAsync(id));
    }

    [RequiresDatabaseFact]
    public async Task One_file_off_the_contract_publishes_nothing_and_lists_why()
    {
        var id = await DraftCompetitionAsync();
        var broken = Path.Combine(Path.GetTempPath(), $"broken-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(broken, """{"schemaVersion": 1, "sections": [{"key": "a", "title": "A", "fields": [{"key": "x", "type": "shortText", "label": "X", "required": true, "printed": true}]}]}""");

        var (exit, output) = await RunAsync(
            "import-content", "--competition", id.ToString(), "--application", Application, "--merit", broken);

        Assert.Equal(AdminCommandRunner.Failure, exit);
        Assert.Contains($"The merit evaluation card in {broken} does not pass the form contract:", output);
        Assert.Contains("Nothing was published.", output);
        Assert.Equal(0, await VersionsAsync(id));
    }

    [RequiresDatabaseFact]
    public async Task The_contract_template_is_published_once_and_a_broken_one_publishes_nothing()
    {
        var id = await DraftCompetitionAsync();
        string[] args = ["import-content", "--competition", id.ToString(), "--application", Application, "--contract", Contract];

        var (exit, output) = await RunAsync(args);
        Assert.Equal(AdminCommandRunner.Success, exit);
        Assert.Contains("Published the contract template as version 1.", output);

        (exit, output) = await RunAsync(args);
        Assert.Equal(AdminCommandRunner.Success, exit);
        Assert.Contains("The contract template is already version 1. Nothing changed.", output);

        var broken = Path.Combine(Path.GetTempPath(), $"broken-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(broken, "Umowa nr {{ Numer }}");
        var fresh = await DraftCompetitionAsync();
        (exit, output) = await RunAsync(
            "import-content", "--competition", fresh.ToString(), "--application", Application, "--contract", broken);

        Assert.Equal(AdminCommandRunner.Failure, exit);
        Assert.Contains($"The contract template in {broken} is refused:", output);
        Assert.Equal(0, await VersionsAsync(fresh));
        await using var context = database.CreateContext();
        Assert.Equal(1, await context.DocumentTemplates.CountAsync(x => x.CompetitionId == id));
        Assert.False(await context.DocumentTemplates.AnyAsync(x => x.CompetitionId == fresh));
    }

    [Theory]
    [InlineData(new[] { "import-content", "--application", "a.json" }, "--competition is missing.")]
    [InlineData(new[] { "import-content", "--competition", "00000000-0000-4000-a000-000000000021", "--contract", "a", "--contract", "b" }, "--contract was given twice.")]
    [InlineData(new[] { "import-content", "--competition", "00000000-0000-4000-a000-000000000021" }, "Nothing to import")]
    [InlineData(new[] { "import-content", "--competition", "nie-id", "--formal", "a.json" }, "is not a competition id")]
    [InlineData(new[] { "import-content", "--competition", "00000000-0000-4000-a000-000000000021", "--formal", "a", "--formal", "b" }, "--formal was given twice.")]
    [InlineData(new[] { "import-content", "--competition", "00000000-0000-4000-a000-000000000021", "--cards", "a" }, "Unknown option --cards.")]
    public async Task A_broken_command_line_is_refused_before_anything_connects(string[] args, string message)
    {
        await using var output = new StringWriter();

        var exit = await AdminCommandRunner.RunAsync(args, new ConfigurationBuilder().Build(), output);

        Assert.Equal(AdminCommandRunner.Failure, exit);
        Assert.Contains(message, output.ToString());
        Assert.Contains("import-content --competition <id>", output.ToString());
    }
}
