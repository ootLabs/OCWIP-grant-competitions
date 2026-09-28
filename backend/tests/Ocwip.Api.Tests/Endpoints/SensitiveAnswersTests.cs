using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// The answers of fields a form marks sensitive (T-47a): encrypted inside
/// applications.answers, and plaintext to whoever may read the application.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SensitiveAnswersTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    : IClassFixture<OcwipWebApplicationFactory>
{
    private static JsonElement Form() =>
        FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field("opis", "shortText", "\"maxLength\": 500"),
            FormDefinitionSamples.Field("rachunek", "shortText", "\"maxLength\": 40, \"sensitive\": true"));

    private async Task<string> StoredAsync(Guid applicationId)
    {
        await using var context = database.CreateContext();
        return await context.Database
            .SqlQueryRaw<string>("SELECT answers::text AS \"Value\" FROM applications WHERE id = {0}", applicationId)
            .SingleAsync();
    }

    [RequiresDatabaseFact]
    public async Task A_sensitive_answer_is_stored_encrypted_and_read_back_as_typed()
    {
        var logs = new CapturedLogs();
        var (host, clock) = CompetitionTestHost.Create(factory, database, services => services.AddSingleton<ILoggerProvider>(logs));
        var competition = await PublishedCompetitionWithFormAsync(host, Form());
        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host, database);
        var draft = await CreateAsync(applicant, competition.Id);

        var saved = await SaveAsync(applicant, draft.Id,
            FormDefinitionSamples.Parse("""{"opis":"Nasz projekt","rachunek":"12 3456 7890"}"""));

        // The response of the save and a later read are both plaintext.
        Assert.Equal("12 3456 7890", saved.Answers.GetProperty("rachunek").GetString());
        var read = (await (await applicant.GetAsync($"/applications/{draft.Id}")).Content
            .ReadFromJsonAsync<Ocwip.Api.Contracts.ApplicationResponse>())!;
        Assert.Equal("12 3456 7890", read.Answers.GetProperty("rachunek").GetString());

        var stored = await StoredAsync(draft.Id);
        Assert.DoesNotContain("3456", stored);
        Assert.Contains("Nasz projekt", stored);

        // Nor in the log, where no application content belongs (T-47a).
        Assert.DoesNotContain("3456", logs.Text);
        Assert.DoesNotContain("Nasz projekt", logs.Text);
    }

    [RequiresDatabaseFact]
    public async Task Text_that_looks_encrypted_in_an_ordinary_field_does_not_break_the_application()
    {
        var (host, clock) = CompetitionTestHost.Create(factory, database);
        var competition = await PublishedCompetitionWithFormAsync(host, Form());
        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (applicant, _, _) = await SeedApplicantAsync(host, database);
        var draft = await CreateAsync(applicant, competition.Id);

        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse("""{"opis":"enc:1:AAAA"}"""));

        var read = await applicant.GetAsync($"/applications/{draft.Id}");
        read.EnsureSuccessStatusCode();
        Assert.Contains("enc:1:AAAA", await read.Content.ReadAsStringAsync());
    }
}
