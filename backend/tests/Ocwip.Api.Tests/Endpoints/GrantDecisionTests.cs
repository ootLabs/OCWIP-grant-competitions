using System.Net;
using System.Net.Http.Json;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
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

        var (fundedApplicant, funded) = await SubmittedAsync(host, _database, competition.Id);
        var (_, reserve) = await SubmittedAsync(host, _database, competition.Id);
        var (_, rejected) = await SubmittedAsync(host, _database, competition.Id);

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        await PrepareAsync(operatorClient, competition.Id);

        var approve = $"/competitions/{competition.Id}/results/approve";

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
}
