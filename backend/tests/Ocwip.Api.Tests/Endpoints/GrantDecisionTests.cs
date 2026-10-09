using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;
using static Ocwip.Api.Tests.Endpoints.EvaluationScene;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// T-42: amounts on the ranking list are a draft nobody outside sees; one
/// approval writes every result at once, only after every evaluation ended,
/// and freezes the amounts; the checksum of the submitted application
/// (D15) does not move with any of it.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class GrantDecisionTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public GrantDecisionTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task One_approval_writes_funded_reserve_and_rejected_and_freezes_the_amounts()
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var (fundedApplicant, funded, _) = await SubmittedAsync(host, _database, competition.Id);
        var (_, reserve, _) = await SubmittedAsync(host, _database, competition.Id);
        var (_, rejected, _) = await SubmittedAsync(host, _database, competition.Id);

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        await PrepareAsync(operatorClient, competition.Id);

        var approve = $"/competitions/{competition.Id}/results/approve";

        // Under review only (T-97): while the intake is open nothing is approved.
        var open = await operatorClient.PostAsync(approve, content: null);
        Assert.Equal(HttpStatusCode.Conflict, open.StatusCode);
        Assert.Contains("w trakcie oceny", await open.Content.ReadAsStringAsync());
        await StartReviewAsync(operatorClient, competition.Id);

        // Nothing is evaluated yet: no result can be written.
        var early = await operatorClient.PostAsync(approve, content: null);
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Contains("czeka: 3", await early.Content.ReadAsStringAsync());

        await FormalAsync(operatorClient, funded, passed: true);
        await FormalAsync(operatorClient, reserve, passed: true);
        await FormalAsync(operatorClient, rejected, passed: false);

        var (expert, expertId) = await SeedReviewerAsync(host);
        await AcceptDeclarationAsync(expert, competition.Id);
        await ScoreAsync(operatorClient, expert, expertId, funded, 18);
        await ScoreAsync(operatorClient, expert, expertId, reserve, 12);

        var checksum = (await fundedApplicant.GetFromJsonAsync<ApplicationResponse>($"/applications/{funded}"))!.Checksum;

        var decision = $"/applications/{funded}/grant-decision";
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await operatorClient.PutAsJsonAsync(decision, new GrantDecisionRequest(0m, null))).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await expert.PutAsJsonAsync(decision, new GrantDecisionRequest(5000m, null))).StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            (await operatorClient.PutAsJsonAsync(decision, new GrantDecisionRequest(5000.50m, "Obniżona o catering."))).StatusCode);

        var ranking = (await operatorClient.GetFromJsonAsync<RankingResponse>($"/competitions/{competition.Id}/ranking"))!;
        Assert.Equal(5000.50m, ranking.AwardedTotal);
        Assert.Null(ranking.ResultsApprovedAt);
        var row = ranking.Rows.Single(x => x.ApplicationId == funded);
        Assert.Equal("Obniżona o catering.", row.DecisionNote);

        // A draft decision is invisible to the applicant.
        var before = (await fundedApplicant.GetFromJsonAsync<ApplicationResponse>($"/applications/{funded}"))!;
        Assert.Equal(ApplicationStatus.Submitted, before.Status);
        Assert.Equal(checksum, before.Checksum);

        Assert.Equal(HttpStatusCode.Forbidden, (await fundedApplicant.PostAsync(approve, content: null)).StatusCode);

        var approved = await operatorClient.PostAsync(approve, content: null);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var counts = (await approved.Content.ReadFromJsonAsync<ResultsApprovalResponse>())!;
        Assert.Equal((1, 1, 1), (counts.Funded, counts.Reserve, counts.Rejected));

        // The same approval resolved the competition (T-97).
        var resolved = (await operatorClient.GetFromJsonAsync<CompetitionResponse>($"/competitions/{competition.Id}"))!;
        Assert.Equal(CompetitionStatus.Resolved, resolved.Status);

        Assert.Equal(HttpStatusCode.Conflict, (await operatorClient.PostAsync(approve, content: null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await operatorClient.PutAsJsonAsync(decision, new GrantDecisionRequest(7000m, null))).StatusCode);

        var after = (await operatorClient.GetFromJsonAsync<RankingResponse>($"/competitions/{competition.Id}/ranking"))!;
        Assert.NotNull(after.ResultsApprovedAt);
        Assert.Equal(ApplicationStatus.Funded, after.Rows.Single(x => x.ApplicationId == funded).Status);
        Assert.Equal(ApplicationStatus.Reserve, after.Rows.Single(x => x.ApplicationId == reserve).Status);
        Assert.Equal(ApplicationStatus.Rejected, after.Rows.Single(x => x.ApplicationId == rejected).Status);
        Assert.Equal(5000.50m, after.AwardedTotal);

        var mine = (await fundedApplicant.GetFromJsonAsync<ApplicationResponse>($"/applications/{funded}"))!;
        Assert.Equal(ApplicationStatus.Funded, mine.Status);
        Assert.Equal(checksum, mine.Checksum);
        Assert.Equal(
            HttpStatusCode.OK,
            (await fundedApplicant.GetAsync($"/applications/{funded}/confirmation")).StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task A_return_left_uncorrected_past_its_deadline_is_rejected_at_approval()
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var (_, evaluated, _) = await SubmittedAsync(host, _database, competition.Id);
        var (_, returned, email) = await SubmittedAsync(host, _database, competition.Id);

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        await PrepareAsync(operatorClient, competition.Id);
        await StartReviewAsync(operatorClient, competition.Id);
        await FormalAsync(operatorClient, evaluated, passed: false);

        var deadline = clock.Now.AddDays(3);
        deadline = deadline.AddTicks(-(deadline.UtcTicks % TimeSpan.TicksPerMinute));
        (await operatorClient.PostAsJsonAsync($"/applications/{returned}/return",
            new ApplicationReturnRequest(["sekcja"], false, "Popraw opis.", deadline))).EnsureSuccessStatusCode();

        // Inside its window the correction may still come: nothing is approved.
        var approve = $"/competitions/{competition.Id}/results/approve";
        var waiting = await operatorClient.PostAsync(approve, content: null);
        Assert.Equal(HttpStatusCode.Conflict, waiting.StatusCode);
        Assert.Contains("czeka: 1", await waiting.Content.ReadAsStringAsync());

        // At the deadline the window is closed for good.
        clock.Now = deadline;
        operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var approved = await operatorClient.PostAsync(approve, content: null);
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var counts = (await approved.Content.ReadFromJsonAsync<ResultsApprovalResponse>())!;
        Assert.Equal((0, 0, 2), (counts.Funded, counts.Reserve, counts.Rejected));

        var applicant = await LoginAsync(host, email);
        var mine = (await applicant.GetFromJsonAsync<ApplicationResponse>($"/applications/{returned}"))!;
        Assert.Equal(ApplicationStatus.Rejected, mine.Status);

        var history = (await operatorClient.GetFromJsonAsync<ApplicationCorrectionsResponse>($"/applications/{returned}/corrections"))!;
        Assert.Contains(history.History, x => x.FromStatus == ApplicationStatus.Returned && x.ToStatus == ApplicationStatus.Rejected);
    }

    private const string AskedFor = """{"opis":"projekt","dotacja":3000.00}""";

    private static System.Text.Json.JsonElement FormWithRequestedGrant() => FormDefinitionSamples.Parse($$"""
        {
          "schemaVersion": 1,
          "sections": [
            {
              "key": "dane",
              "title": "Dane projektu",
              "fields": [
                {{FormDefinitionSamples.Field("opis", "shortText", "\"maxLength\": 500")}},
                {{FormDefinitionSamples.Field("dotacja", "amount", "\"role\": \"requestedGrant\"")}}
              ]
            }
          ]
        }
        """);

    /// <summary>
    /// P4-19: 25 000 zł went to a group that asked for 3500, saved without a
    /// word. A grant on the ranking list is bounded by what was asked for,
    /// like a promotion from the reserve list already was (S-33).
    /// </summary>
    [RequiresDatabaseFact]
    public async Task A_grant_above_what_the_application_asked_for_is_refused()
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host, FormWithRequestedGrant());
        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (_, application, _) = await SubmittedAsync(host, _database, competition.Id, AskedFor);
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        var decision = $"/applications/{application}/grant-decision";

        var tooMuch = await operatorClient.PutAsJsonAsync(decision, new GrantDecisionRequest(3000.01m, null));

        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        Assert.Contains("ubiegał się o 3000,00", await tooMuch.Content.ReadAsStringAsync());
        Assert.Equal(
            HttpStatusCode.OK,
            (await operatorClient.PutAsJsonAsync(decision, new GrantDecisionRequest(3000m, null))).StatusCode);
    }

    /// <summary>
    /// P4-18: an expert's card recommended 5700 zł to an application asking
    /// for 3500, and finished without a word. The recommendation may be lower
    /// than the request (M5-ocena), never higher.
    /// </summary>
    [RequiresDatabaseFact]
    public async Task A_merit_card_recommending_more_than_was_asked_for_does_not_finish()
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host, FormWithRequestedGrant());
        clock.Now = CompetitionTestHost.Start.AddDays(1);
        var (_, application, _) = await SubmittedAsync(host, _database, competition.Id, AskedFor);
        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        await PrepareAsync(operatorClient, competition.Id);
        var (expert, expertId) = await SeedReviewerAsync(host);
        await AcceptDeclarationAsync(expert, competition.Id);
        (await operatorClient.PostAsJsonAsync(
            $"/applications/{application}/assignments", new AssignReviewerRequest(expertId))).EnsureSuccessStatusCode();
        var card = (await (await expert.PostAsync($"/applications/{application}/evaluations/merit", content: null))
            .Content.ReadFromJsonAsync<EvaluationResponse>())!;

        async Task<HttpResponseMessage> FinishWith(decimal recommended)
        {
            (await expert.PutAsJsonAsync($"/evaluations/{card.Id}", new
            {
                answers = new JsonObject
                {
                    ["pomysl"] = 10,
                    ["pomysl_uzasadnienie"] = "Uzasadnienie.",
                    ["budzet"] = 1,
                    ["biale_plamy"] = false,
                    ["kwota"] = recommended,
                },
            })).EnsureSuccessStatusCode();
            return await expert.PostAsync($"/evaluations/{card.Id}/finish", content: null);
        }

        var over = await FinishWith(3500m);
        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
        Assert.Contains("nie może być wyższa od wnioskowanej (3000,00", await over.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, (await FinishWith(2500m)).StatusCode);
    }
}
