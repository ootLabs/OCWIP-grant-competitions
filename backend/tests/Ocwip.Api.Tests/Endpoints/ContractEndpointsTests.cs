using System.Net;
using System.Net.Http.Json;
using Ocwip.Api.Contracts;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Services.Documents;
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
        await StartReviewAsync(operatorClient, competition.Id);
        (await operatorClient.PostAsync($"/competitions/{competition.Id}/results/approve", content: null))
            .EnsureSuccessStatusCode();

        var created = await operatorClient.PostAsync(drawUp, content: null);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var contract = (await created.Content.ReadFromJsonAsync<ContractResponse>())!;
        var address = $"/contracts/{contract.Id}";

        // A body without values is refused, not a 500.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await operatorClient.PutAsync($"{address}/values", JsonContent.Create(new { }))).StatusCode);

        // A system value cannot be typed over.
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await operatorClient.PutAsJsonAsync($"{address}/values", new ContractValuesRequest(
                new Dictionary<string, string?> { ["kwota_dotacji"] = "1 zł" }))).StatusCode);

        var draft = PdfTextReader.Text(await operatorClient.GetByteArrayAsync($"{address}/pdf"));
        Assert.Contains("sześć tysięcy pięćset złotych i pięćdziesiąt groszy", draft);
        Assert.Contains("6500,50 zł", draft);
        // P4-20: the account comes from the entity card frozen at submission,
        // not retyped by the operator; a blank the card cannot answer stays one.
        Assert.Contains($"Rachunek: {TestEntity.BankAccount}", draft);
        Assert.Contains("Reprezentuje: ……………………", draft);

        var incomplete = await operatorClient.PostAsJsonAsync($"{address}/sign", new SignContractRequest(new DateOnly(2026, 5, 4)));
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        var refusal = await incomplete.Content.ReadAsStringAsync();
        Assert.Contains("reprezentanci", refusal);
        Assert.DoesNotContain("numer_rachunku", refusal);

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

        // The applicant edits the card after signing: the party printed on
        // the signed contract is the one submitted, every time.
        string submittedName;
        await using (var context = _database.CreateContext())
        {
            var entity = context.Entities.Single(x => context.Applications.Any(a => a.Id == funded && a.EntityId == x.Id));
            submittedName = entity.Name;
            entity.Name = "Nazwa zmieniona po podpisaniu";
            await context.SaveChangesAsync();
        }

        var signed = PdfTextReader.Text(await applicant.GetByteArrayAsync($"{address}/pdf"));
        Assert.Contains(submittedName, signed);
        Assert.DoesNotContain("Nazwa zmieniona po podpisaniu", signed);
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

    /// <summary>
    /// The same template for both kinds of party, with the clause about the
    /// register marked for the registered ones. An informal group used to be
    /// asked for a register and a number in it, which left the operator
    /// typing "nie dotyczy" into a contract somebody signs (znalezisko 11).
    /// </summary>
    private const string KindTemplate = """
        UMOWA NR {{numer_umowy}} z {{nazwa_realizatora}}
        {{#Organisation,PatronInformalGroup}}wpisaną do {{rejestr}} pod numerem {{numer_w_rejestrze}}{{/}}{{#InformalGroup}}reprezentowaną przez lidera, adres {{adres_lidera}}{{/}}
        Rachunek: {{numer_rachunku}}.
        """;

    [RequiresDatabaseFact]
    public async Task An_informal_groups_contract_asks_for_its_blanks_only_and_signs_without_a_register()
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var (_, funded, _) = await SubmittedAsync(host, _database, competition.Id);

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        await PrepareAsync(operatorClient, competition.Id);
        await FormalAsync(operatorClient, funded, passed: true);
        var (expert, expertId) = await SeedReviewerAsync(host);
        await AcceptDeclarationAsync(expert, competition.Id);
        await ScoreAsync(operatorClient, expert, expertId, funded, 18);
        (await operatorClient.PutAsJsonAsync(
            $"/applications/{funded}/grant-decision", new GrantDecisionRequest(5000m, null))).EnsureSuccessStatusCode();
        await StartReviewAsync(operatorClient, competition.Id);
        (await operatorClient.PostAsync($"/competitions/{competition.Id}/results/approve", content: null))
            .EnsureSuccessStatusCode();

        // Submitted as an informal group (T-94): the kind is written the way
        // submission writes it, after the cards of EvaluationCardSamples,
        // which this test is not about, have been filled in as they are.
        await using (var context = _database.CreateContext())
        {
            var application = await context.Applications.SingleAsync(x => x.Id == funded);
            application.ApplicantType = EntityType.InformalGroup;
            await context.SaveChangesAsync();
        }

        (await operatorClient.PostAsJsonAsync($"/competitions/{competition.Id}/contract-template",
            new DocumentTemplateRequest(KindTemplate))).EnsureSuccessStatusCode();

        var contract = (await (await operatorClient.PostAsync($"/applications/{funded}/contract", content: null))
            .Content.ReadFromJsonAsync<ContractResponse>())!;
        var address = $"/contracts/{contract.Id}";

        Assert.Equal(
            ["adres_lidera", "numer_rachunku"],
            contract.Fields.Where(x => !x.System).Select(x => x.Name));

        // A blank of the other kind is not this contract's to fill in.
        var alien = await operatorClient.PutAsJsonAsync($"{address}/values", new ContractValuesRequest(
            new Dictionary<string, string?> { ["rejestr"] = "KRS" }));
        Assert.Equal(HttpStatusCode.BadRequest, alien.StatusCode);
        Assert.Contains("nie zostawia do wpisania", await alien.Content.ReadAsStringAsync());

        (await operatorClient.PutAsJsonAsync($"{address}/values", new ContractValuesRequest(
            new Dictionary<string, string?>
            {
                ["adres_lidera"] = "ul. Polna 1, 45-001 Opole",
                ["numer_rachunku"] = "12 3456 7890",
            }))).EnsureSuccessStatusCode();

        // Nothing is missing, although the register blanks have no value.
        (await operatorClient.PostAsJsonAsync($"{address}/sign", new SignContractRequest(new DateOnly(2026, 5, 4))))
            .EnsureSuccessStatusCode();

        var pdf = PdfTextReader.Text(await operatorClient.GetByteArrayAsync($"{address}/pdf"));
        Assert.Contains("reprezentowaną przez lidera, adres ul. Polna 1, 45-001 Opole", pdf);
        Assert.DoesNotContain("wpisaną do", pdf);
        Assert.DoesNotContain(TemplatePlaceholders.Blank, pdf);
    }
}
