using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Data;
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
        var (host, applicant, operatorClient, expert, applicationId, competitionId) = await FundedAsync(
            ReportFormSamples.Report(),
            async (client, id) => Assert.Equal(
                HttpStatusCode.Conflict,
                (await client.PostAsync($"/applications/{id}/report", content: null)).StatusCode));
        var start = $"/applications/{applicationId}/report";

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

        var list = (await operatorClient.GetFromJsonAsync<List<ReportListItem>>($"/competitions/{competitionId}/reports"))!;
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

    [RequiresDatabaseFact]
    public async Task The_operator_refuses_costs_row_by_row_and_the_refund_follows()
    {
        var (host, applicant, operatorClient, _, applicationId, _) = await FundedAsync(ReportFormSamples.SettledReport(), null);
        var report = (await (await applicant.PostAsync($"/applications/{applicationId}/report", content: null))
            .Content.ReadFromJsonAsync<ReportResponse>())!;
        var address = $"/reports/{report.Id}";
        var review = $"{address}/cost-review";

        await SaveReportAsync(applicant, address, """
            {"przebieg":"Zbudowaliśmy ławki.","budzet":[{"wykonana":1400},{"wykonana":90}]}
            """);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await operatorClient.PutAsJsonAsync(review, new ReviewCostsRequest([]))).StatusCode);
        (await applicant.PostAsync($"{address}/submit", content: null)).EnsureSuccessStatusCode();

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await applicant.PutAsJsonAsync(review, new ReviewCostsRequest([]))).StatusCode);

        var refused = await operatorClient.PutAsJsonAsync(review, new ReviewCostsRequest(
            [new CostReviewItem(0, 1500m, "Za dużo."), new CostReviewItem(1, 10m, " "), new CostReviewItem(2, 1m, "Brak.")]));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var problem = await refused.Content.ReadAsStringAsync();
        Assert.Contains("nie przekraczać", problem);
        Assert.Contains("Podaj powód", problem);
        Assert.Contains("nie ma pozycji 3", problem);

        var reviewed = await operatorClient.PutAsJsonAsync(review, new ReviewCostsRequest(
            [new CostReviewItem(1, 40m, "Faktura bez opisu."), new CostReviewItem(0, 100m, "Deski ponad plan.")]));
        reviewed.EnsureSuccessStatusCode();
        var settlement = (await reviewed.Content.ReadFromJsonAsync<ReportResponse>())!.Settlement!;
        Assert.Equal("budzet", settlement.BudgetKey);
        Assert.Equal(1600m, settlement.AwardedGrant);
        Assert.Equal(1490m, settlement.GrantSpent);
        Assert.Equal(140m, settlement.Refused);
        Assert.Equal(1350m, settlement.Accepted);
        Assert.Equal(250m, settlement.Refund);

        // The applicant reads why, at the cost it concerns.
        var seen = (await applicant.GetFromJsonAsync<ReportResponse>(address))!.Settlement!;
        Assert.Equal("Faktura bez opisu.", seen.Rows[1].Reason);

        // A row changed after a return loses its judgement, the other keeps it.
        (await operatorClient.PostAsJsonAsync($"{address}/return", new ReturnReportRequest("Popraw farbę."))).EnsureSuccessStatusCode();
        await SaveReportAsync(applicant, address, """
            {"przebieg":"Zbudowaliśmy ławki.","budzet":[{"wykonana":1400},{"wykonana":60}]}
            """);
        (await applicant.PostAsync($"{address}/submit", content: null)).EnsureSuccessStatusCode();

        var resubmitted = (await operatorClient.GetFromJsonAsync<ReportResponse>(address))!.Settlement!;
        Assert.Equal(100m, resubmitted.Refused);
        Assert.Null(resubmitted.Rows[1].Reason);
        Assert.Equal("Deski ponad plan.", resubmitted.Rows[0].Reason);

        // Accepting the report settles the project, once.
        Assert.Equal(
            ApplicationStatus.Funded,
            (await applicant.GetFromJsonAsync<ApplicationResponse>($"/applications/{applicationId}"))!.Status);
        // A review read before another operator accepted cannot land on the accepted report.
        using var scope = host.Services.CreateScope();
        var stale = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var readBefore = await stale.Reports.SingleAsync(x => x.Id == report.Id);

        (await operatorClient.PostAsync($"{address}/accept", content: null)).EnsureSuccessStatusCode();
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await operatorClient.PutAsJsonAsync(review, new ReviewCostsRequest([]))).StatusCode);

        readBefore.CostReview = JsonSerializer.SerializeToElement(Array.Empty<object>());
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());

        var settled = (await applicant.GetFromJsonAsync<ApplicationResponse>($"/applications/{applicationId}"))!;
        Assert.Equal(ApplicationStatus.Settled, settled.Status);
        Assert.Equal(1600m, settled.AwardedGrant);
        Assert.NotNull((await applicant.GetFromJsonAsync<ReportResponse>(address))!.AcceptedAt);
    }

    /// <summary>
    /// A competition with the sample application and the given report form,
    /// one application funded with 1600. The check runs after the report
    /// form is published and before the results are approved.
    /// </summary>
    private async Task<(WebApplicationFactory<Program> Host, HttpClient Applicant, HttpClient Operator, HttpClient Expert, Guid ApplicationId, Guid CompetitionId)> FundedAsync(
        JsonElement reportForm, Func<HttpClient, Guid, Task>? beforeFunding)
    {
        var (scene, _) = await FundedWithMailAsync(reportForm, beforeFunding);
        return scene;
    }

    /// <summary>The same scene with the mail the applicant receives recorded.</summary>
    private async Task<((WebApplicationFactory<Program> Host, HttpClient Applicant, HttpClient Operator, HttpClient Expert, Guid ApplicationId, Guid CompetitionId) Scene, RecordingEmailSender Emails)> FundedWithMailAsync(
        JsonElement reportForm, Func<HttpClient, Guid, Task>? beforeFunding)
    {
        var emails = new RecordingEmailSender();
        var (host, clock) = CompetitionTestHost.Create(
            _factory, _database, services => services.AddSingleton<IEmailSender>(emails));
        var competition = await PublishedCompetitionWithFormAsync(host, ReportFormSamples.Application());
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var (applicant, _, _) = await SeedApplicantAsync(host, _database);
        var draft = await CreateAsync(applicant, competition.Id);
        await SaveAsync(applicant, draft.Id, FormDefinitionSamples.Parse(ReportFormSamples.ApplicationAnswers));
        (await applicant.PostAsync($"/applications/{draft.Id}/submit", content: null)).EnsureSuccessStatusCode();

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        (await operatorClient.PostAsJsonAsync(
            $"/competitions/{competition.Id}/report-form",
            new FormDefinitionRequest(reportForm))).EnsureSuccessStatusCode();

        if (beforeFunding is not null)
        {
            await beforeFunding(applicant, draft.Id);
        }

        await PrepareAsync(operatorClient, competition.Id);
        await FormalAsync(operatorClient, draft.Id, passed: true);
        var (expert, expertId) = await SeedReviewerAsync(host);
        await AcceptDeclarationAsync(expert, competition.Id);
        await ScoreAsync(operatorClient, expert, expertId, draft.Id, 18);
        (await operatorClient.PutAsJsonAsync(
            $"/applications/{draft.Id}/grant-decision", new GrantDecisionRequest(1600m, null))).EnsureSuccessStatusCode();
        await StartReviewAsync(operatorClient, competition.Id);
        (await operatorClient.PostAsync($"/competitions/{competition.Id}/results/approve", content: null))
            .EnsureSuccessStatusCode();

        return ((host, applicant, operatorClient, expert, draft.Id, competition.Id), emails);
    }

    // Returning a report and accepting it both put the next move on the
    // applicant, and both used to change the state silently: the applicant
    // learnt of it only by opening the panel, which at a report filed once
    // every few months means not at all (znalezisko 12).
    [RequiresDatabaseFact]
    public async Task Returning_and_accepting_a_report_are_both_mailed_to_the_applicant()
    {
        var (scene, emails) = await FundedWithMailAsync(ReportFormSamples.SettledReport(), null);
        var (_, applicant, operatorClient, _, applicationId, _) = scene;
        var report = (await (await applicant.PostAsync($"/applications/{applicationId}/report", content: null))
            .Content.ReadFromJsonAsync<ReportResponse>())!;
        var address = $"/reports/{report.Id}";
        var answers = """
            {"przebieg":"Zbudowaliśmy ławki.","budzet":[{"wykonana":1400},{"wykonana":90}]}
            """;

        await SaveReportAsync(applicant, address, answers);
        (await applicant.PostAsync($"{address}/submit", content: null)).EnsureSuccessStatusCode();
        (await operatorClient.PostAsJsonAsync(
            $"{address}/return", new ReturnReportRequest("Brakuje faktury za drewno."))).EnsureSuccessStatusCode();

        var sentBack = Assert.Single(emails.Sent, x => x.Subject.Contains("zwrócone do poprawy"));
        Assert.Contains("Brakuje faktury za drewno.", sentBack.Body);
        Assert.Contains("ponownie", sentBack.Body);

        (await applicant.PostAsync($"{address}/submit", content: null)).EnsureSuccessStatusCode();
        (await operatorClient.PutAsJsonAsync(
            $"{address}/cost-review", new ReviewCostsRequest([new CostReviewItem(0, 100m, "Brak faktury.")])))
            .EnsureSuccessStatusCode();
        (await operatorClient.PostAsync($"{address}/accept", content: null)).EnsureSuccessStatusCode();

        var accepted = Assert.Single(emails.Sent, x => x.Subject.Contains("przyjęte"));
        Assert.Equal(sentBack.To, accepted.To);
        // The refund is the one thing the applicant has to act on: 1600
        // awarded, 1490 spent of it, 100 refused, so 210 goes back.
        Assert.Contains("Do zwrotu", accepted.Body);
        Assert.Contains("210,00", accepted.Body);
    }

    /// <summary>
    /// A return keeps the version it sends back (S-35). The report row is
    /// overwritten in place by the correction, and a changed amount stops
    /// the settlement counting the judgement written against the old one, so
    /// without the copy neither what the applicant declared nor what the
    /// operator questioned survives to the settlement of a public grant.
    /// </summary>
    [RequiresDatabaseFact]
    public async Task A_return_keeps_the_version_it_sends_back_with_what_the_operator_questioned()
    {
        var (scene, _) = await FundedWithMailAsync(ReportFormSamples.SettledReport(), null);
        var (_, applicant, operatorClient, _, applicationId, _) = scene;
        var report = (await (await applicant.PostAsync($"/applications/{applicationId}/report", content: null))
            .Content.ReadFromJsonAsync<ReportResponse>())!;
        var address = $"/reports/{report.Id}";

        await SaveReportAsync(applicant, address, """
            {"przebieg":"Zbudowaliśmy ławki.","budzet":[{"wykonana":1400},{"wykonana":90}]}
            """);
        (await applicant.PostAsync($"{address}/submit", content: null)).EnsureSuccessStatusCode();

        (await operatorClient.PutAsJsonAsync(
            $"{address}/cost-review", new ReviewCostsRequest([new CostReviewItem(0, 1400m, "Faktura bez opisu.")])))
            .EnsureSuccessStatusCode();
        (await operatorClient.PostAsJsonAsync(
            $"{address}/return", new ReturnReportRequest("Proszę o poprawną fakturę."))).EnsureSuccessStatusCode();

        // The correction moves the very amount that was questioned, by a
        // grosz, which is what used to take the judgement with it.
        await SaveReportAsync(applicant, address, """
            {"przebieg":"Zbudowaliśmy ławki.","budzet":[{"wykonana":1399.99},{"wykonana":90}]}
            """);
        (await applicant.PostAsync($"{address}/submit", content: null)).EnsureSuccessStatusCode();

        await using var context = _database.CreateContext();
        var version = await context.ReportVersions.AsNoTracking().SingleAsync(x => x.ReportId == report.Id);

        Assert.Equal(1, version.VersionNumber);
        Assert.Equal(1400m, version.Answers.GetProperty("budzet")[0].GetProperty("wykonana").GetDecimal());

        var questioned = Assert.Single(version.CostReview.EnumerateArray());
        Assert.Equal("Faktura bez opisu.", questioned.GetProperty("reason").GetString());

        // The report row itself moved on, which is the point of the copy.
        var current = await context.Reports.AsNoTracking().SingleAsync(x => x.Id == report.Id);
        Assert.Equal(1399.99m, current.Answers.GetProperty("budzet")[0].GetProperty("wykonana").GetDecimal());
    }

    private static async Task<ReportResponse> SaveReportAsync(HttpClient client, string address, string answers)
    {
        var response = await client.PutAsJsonAsync(address, new SaveReportRequest(FormDefinitionSamples.Parse(answers)));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReportResponse>())!;
    }
}
