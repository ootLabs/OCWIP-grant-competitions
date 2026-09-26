using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;
using static Ocwip.Api.Tests.Endpoints.EvaluationScene;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// T-50a: only a funded application reports; the report starts as the
/// application said and keeps it; the applicant fills in and submits, the
/// operator sends back with a reason or accepts; nobody else reads it.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ReportEndpointsTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public ReportEndpointsTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task A_funded_project_reports_from_start_to_acceptance()
    {
        var (host, clock) = CompetitionTestHost.Create(_factory, _database);
        var competition = await PublishedCompetitionWithFormAsync(host, ReportFormSamples.Application());
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var (applicant, _, _) = await SeedApplicantAsync(host, _database);
        var draft = await CreateAsync(applicant, competition.Id);
        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse(ReportFormSamples.ApplicationAnswers));
        (await applicant.PostAsync($"/applications/{draft.Id}/submit", content: null)).EnsureSuccessStatusCode();

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        (await operatorClient.PostAsJsonAsync(
            $"/competitions/{competition.Id}/report-form",
            new FormDefinitionRequest(ReportFormSamples.Report()))).EnsureSuccessStatusCode();

        var start = $"/applications/{draft.Id}/report";
        Assert.Equal(HttpStatusCode.Conflict, (await applicant.PostAsync(start, content: null)).StatusCode);

        await PrepareAsync(operatorClient, competition.Id);
        await FormalAsync(operatorClient, draft.Id, passed: true);
        var (expert, expertId) = await SeedReviewerAsync(host);
        await AcceptDeclarationAsync(expert, competition.Id);
        await ScoreAsync(operatorClient, expert, expertId, draft.Id, 18);
        (await operatorClient.PutAsJsonAsync(
            $"/applications/{draft.Id}/grant-decision", new GrantDecisionRequest(1600m, null))).EnsureSuccessStatusCode();
        (await operatorClient.PostAsync($"/competitions/{competition.Id}/results/approve", content: null))
            .EnsureSuccessStatusCode();

        var created = await applicant.PostAsync(start, content: null);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var report = (await created.Content.ReadFromJsonAsync<ReportResponse>())!;
        Assert.Equal("Ławki w parku", report.Answers.GetProperty("tytul").GetString());
        Assert.Equal(1500m, report.Answers.GetProperty("budzet")[0].GetProperty("planowana").GetDecimal());

        var again = (await (await applicant.PostAsync(start, content: null)).Content.ReadFromJsonAsync<ReportResponse>())!;
        Assert.Equal(report.Id, again.Id);

        var address = $"/reports/{report.Id}";
        var (stranger, _, _) = await SeedApplicantAsync(host, _database);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync(address)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await expert.GetAsync(address)).StatusCode);

        // A hand made request cannot change what the application said.
        var saved = await SaveReportAsync(applicant, address, """
            {"tytul":"Inny","budzet":[{"pozycja":"Inne","planowana":1,"wykonana":1400},{"wykonana":90}]}
            """);
        Assert.Equal("Ławki w parku", saved.Answers.GetProperty("tytul").GetString());
        Assert.Equal(1500m, saved.Answers.GetProperty("budzet")[0].GetProperty("planowana").GetDecimal());

        var incomplete = await applicant.PostAsync($"{address}/submit", content: null);
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        Assert.Contains("przebieg", await incomplete.Content.ReadAsStringAsync());

        await SaveReportAsync(applicant, address, """
            {"przebieg":"Zbudowaliśmy ławki.","budzet":[{"wykonana":1400},{"wykonana":90}]}
            """);
        (await applicant.PostAsync($"{address}/submit", content: null)).EnsureSuccessStatusCode();
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await applicant.PutAsJsonAsync(address, new { answers = new { } })).StatusCode);

        var list = (await operatorClient.GetFromJsonAsync<List<ReportListItem>>($"/competitions/{competition.Id}/reports"))!;
        Assert.Equal(ReportStatus.Submitted, Assert.Single(list).Status);

        Assert.Equal(HttpStatusCode.Forbidden, (await applicant.PostAsync($"{address}/accept", content: null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await operatorClient.PostAsJsonAsync($"{address}/return", new ReturnReportRequest(" "))).StatusCode);
        (await operatorClient.PostAsJsonAsync(
            $"{address}/return", new ReturnReportRequest("Brakuje opisu promocji."))).EnsureSuccessStatusCode();

        var returned = (await applicant.GetFromJsonAsync<ReportResponse>(address))!;
        Assert.Equal(ReportStatus.Returned, returned.Status);
        Assert.Equal("Brakuje opisu promocji.", returned.ReturnReason);

        await SaveReportAsync(applicant, address, """
            {"przebieg":"Zbudowaliśmy ławki i ogłosiliśmy to w gazetce.","budzet":[{"wykonana":1400},{"wykonana":90}]}
            """);
        (await applicant.PostAsync($"{address}/submit", content: null)).EnsureSuccessStatusCode();
        (await operatorClient.PostAsync($"{address}/accept", content: null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await operatorClient.PostAsync($"{address}/accept", content: null)).StatusCode);

        var accepted = (await operatorClient.GetFromJsonAsync<ReportResponse>(address))!;
        Assert.Equal(ReportStatus.Accepted, accepted.Status);
        Assert.Null(accepted.ReturnReason);
    }

    private static async Task<ReportResponse> SaveReportAsync(HttpClient client, string address, string answers)
    {
        var response = await client.PutAsJsonAsync(address, new SaveReportRequest(FormDefinitionSamples.Parse(answers)));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReportResponse>())!;
    }
}
