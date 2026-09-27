using System.Net;
using System.Net.Http.Json;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Services;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;
using static Ocwip.Api.Tests.Endpoints.EvaluationScene;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// T-45: a contract of a funded application, drawn up on the template in
/// force, filled in by the operator, printed with Polish letters and the
/// amount in words, signed only when nothing is left blank; the application
/// then reads "umowa podpisana". The applicant reads their own, nobody else.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ContractEndpointsTests : IClassFixture<OcwipWebApplicationFactory>
{
    private const string Template = """
        UMOWA NR {{numer_umowy}}
        zawarta w dniu {{data_zawarcia}} w Opolu z {{nazwa_realizatora}}.
        Dotacja: {{kwota_dotacji}} (słownie: {{kwota_dotacji_slownie}}).
        Rachunek: {{numer_rachunku}}. Reprezentuje: {{reprezentanci}}.
        """;

    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public ContractEndpointsTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task A_funded_application_gets_a_contract_that_is_signed_only_when_complete()
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var (applicant, funded, _) = await SubmittedAsync(host, _database, competition.Id);
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        await PrepareAsync(operatorClient, competition.Id);
        await FormalAsync(operatorClient, funded, passed: true);
        var (expert, expertId) = await SeedReviewerAsync(host);
        await AcceptDeclarationAsync(expert, competition.Id);
        await ScoreAsync(operatorClient, expert, expertId, funded, 18);

        var templateAddress = $"/competitions/{competition.Id}/contract-template";
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await operatorClient.PostAsJsonAsync(templateAddress, new DocumentTemplateRequest("Umowa {{ Numer }}"))).StatusCode);
        var template = (await (await operatorClient.PostAsJsonAsync(templateAddress, new DocumentTemplateRequest(Template)))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<DocumentTemplateResponse>())!;
        Assert.Equal(["numer_rachunku", "reprezentanci"], template.Placeholders.Where(x => !x.System).Select(x => x.Name));

        var drawUp = $"/applications/{funded}/contract";
        Assert.Equal(HttpStatusCode.Conflict, (await operatorClient.PostAsync(drawUp, content: null)).StatusCode);

        (await operatorClient.PutAsJsonAsync(
            $"/applications/{funded}/grant-decision", new GrantDecisionRequest(6500.50m, null))).EnsureSuccessStatusCode();
        (await operatorClient.PostAsync($"/competitions/{competition.Id}/results/approve", content: null))
            .EnsureSuccessStatusCode();

        var created = await operatorClient.PostAsync(drawUp, content: null);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var contract = (await created.Content.ReadFromJsonAsync<ContractResponse>())!;
        var address = $"/contracts/{contract.Id}";

        // A system value cannot be typed over.
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await operatorClient.PutAsJsonAsync($"{address}/values", new ContractValuesRequest(
                new Dictionary<string, string?> { ["kwota_dotacji"] = "1 zł" }))).StatusCode);

        var draft = PdfTextReader.Text(await operatorClient.GetByteArrayAsync($"{address}/pdf"));
        Assert.Contains("sześć tysięcy pięćset złotych i pięćdziesiąt groszy", draft);
        Assert.Contains("6500,50 zł", draft);
        Assert.Contains("Rachunek: ……………………", draft);

        var incomplete = await operatorClient.PostAsJsonAsync($"{address}/sign", new SignContractRequest(new DateOnly(2026, 5, 4)));
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        Assert.Contains("numer_rachunku", await incomplete.Content.ReadAsStringAsync());

        (await operatorClient.PutAsJsonAsync($"{address}/values", new ContractValuesRequest(new Dictionary<string, string?>
        {
            ["numer_rachunku"] = "12 3456 7890",
            ["reprezentanci"] = "Łucja Żółkiewska, prezeska",
        }))).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Forbidden, (await applicant.PostAsJsonAsync($"{address}/sign", new SignContractRequest(new DateOnly(2026, 5, 4)))).StatusCode);
        (await operatorClient.PostAsJsonAsync($"{address}/sign", new SignContractRequest(new DateOnly(2026, 5, 4))))
            .EnsureSuccessStatusCode();

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await operatorClient.PutAsJsonAsync($"{address}/values", new ContractValuesRequest(
                new Dictionary<string, string?> { ["numer_rachunku"] = "inny" }))).StatusCode);

        var signed = PdfTextReader.Text(await applicant.GetByteArrayAsync($"{address}/pdf"));
        Assert.Contains("zawarta w dniu 4 maja 2026 r.", signed);
        Assert.Contains("Łucja Żółkiewska, prezeska", signed);

        var mine = (await applicant.GetFromJsonAsync<ApplicationResponse>($"/applications/{funded}"))!;
        Assert.Equal(ApplicationStatus.ContractSigned, mine.Status);
        Assert.Equal(6500.50m, mine.AwardedGrant);
        Assert.Equal(ContractStatus.Signed, (await applicant.GetFromJsonAsync<ContractResponse>(drawUp))!.Status);

        Assert.Equal(HttpStatusCode.Forbidden, (await expert.GetAsync(drawUp)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await expert.GetAsync($"{address}/pdf")).StatusCode);
        var (stranger, _, _) = await SeedApplicantAsync(host, _database);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"{address}/pdf")).StatusCode);
    }
}
