using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services.Jobs;
using Ocwip.Api.Tests.Data;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;
using static Ocwip.Api.Tests.Endpoints.EvaluationScene;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// Resignation and the reserve list (T-109): the clock reminds the operator
/// after 14 days, the operator confirms the resignation, and the money goes
/// to the reserve application the system proposes, within the pool.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ResignationTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    : IClassFixture<OcwipWebApplicationFactory>
{
    private sealed record Scene(
        WebApplicationFactory<Program> Host, FixedTimeProvider Clock, RecordingEmailSender Emails, HttpClient Operator,
        string OperatorEmail, Guid CompetitionId, Guid Funded, string FundedEmail, Guid Reserve, string ReserveEmail,
        DateTimeOffset ApprovedAt);

    private async Task<Scene> ApprovedAsync()
    {
        var emails = new RecordingEmailSender();
        var (host, clock) = CompetitionTestHost.Create(factory, database, s => s.AddSingleton<IEmailSender>(emails));
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var (_, funded, fundedEmail) = await SubmittedAsync(host, database, competition.Id);
        var (_, reserve, reserveEmail) = await SubmittedAsync(host, database, competition.Id);

        var operatorEmail = SessionTestHost.Email("operator-rezygnacje");
        await SessionTestHost.CreateAccountAsync(host, operatorEmail, Role.Operator);
        var operatorClient = await LoginAsync(host, operatorEmail);

        await PrepareAsync(operatorClient, competition.Id);
        await FormalAsync(operatorClient, funded, passed: true);
        await FormalAsync(operatorClient, reserve, passed: true);
        var (expert, expertId) = await SeedReviewerAsync(host);
        await AcceptDeclarationAsync(expert, competition.Id);
        await ScoreAsync(operatorClient, expert, expertId, funded, 18);
        await ScoreAsync(operatorClient, expert, expertId, reserve, 12);
        (await operatorClient.PutAsJsonAsync(
            $"/applications/{funded}/grant-decision", new GrantDecisionRequest(6500m, null))).EnsureSuccessStatusCode();
        await StartReviewAsync(operatorClient, competition.Id);
        (await operatorClient.PostAsync($"/competitions/{competition.Id}/results/approve", content: null)).EnsureSuccessStatusCode();

        return new Scene(host, clock, emails, operatorClient, operatorEmail, competition.Id,
            funded, fundedEmail, reserve, reserveEmail, clock.Now);
    }

    private async Task<ApplicationStatus> StatusAsync(Guid id)
    {
        await using var context = database.CreateContext();
        return await context.Applications.AsNoTracking().Where(x => x.Id == id).Select(x => x.Status).SingleAsync();
    }

    private async Task<List<(ApplicationStatus From, ApplicationStatus To)>> HistoryAsync(Guid id)
    {
        await using var context = database.CreateContext();
        return [.. (await context.ApplicationStatusHistory.AsNoTracking()
                .Where(x => x.ApplicationId == id).OrderBy(x => x.ChangedAt).ToListAsync())
            .Select(x => (x.FromStatus, x.ToStatus))];
    }

    [RequiresDatabaseFact]
    public async Task A_confirmed_resignation_frees_the_money_for_the_proposed_reserve_within_the_pool()
    {
        var scene = await ApprovedAsync();
        await using (var context = database.CreateContext())
        {
            await context.Competitions.Where(x => x.Id == scene.CompetitionId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.TotalPoolAmount, 10000m));
        }

        var address = $"/competitions/{scene.CompetitionId}/resignations";
        var before = (await scene.Operator.GetFromJsonAsync<ResignationsResponse>(address))!;
        Assert.Equal(scene.Funded, Assert.Single(before.Unsigned).ApplicationId);
        Assert.False(before.Unsigned[0].Overdue);
        Assert.Equal(scene.ApprovedAt + TimeSpan.FromDays(14), before.ContractDeadline);
        Assert.Equal(scene.Reserve, before.NextReserve!.ApplicationId);
        Assert.Equal(3500m, before.FreePool);

        Assert.Equal(HttpStatusCode.NoContent, (await scene.Operator.PostAsync($"/applications/{scene.Funded}/resignation", content: null)).StatusCode);
        Assert.Equal(ApplicationStatus.Resigned, await StatusAsync(scene.Funded));
        Assert.Contains((ApplicationStatus.Funded, ApplicationStatus.Resigned), await HistoryAsync(scene.Funded));
        Assert.Single(scene.Emails.Sent, x => x.To == scene.FundedEmail && x.Subject.StartsWith("Rezygnacja", StringComparison.Ordinal));

        var after = (await scene.Operator.GetFromJsonAsync<ResignationsResponse>(address))!;
        Assert.Equal(0m, after.AwardedTotal);
        Assert.Equal(10000m, after.FreePool);
        Assert.Empty(after.Unsigned);

        var promotion = $"/applications/{scene.Reserve}/promotion";
        var tooMuch = await scene.Operator.PostAsJsonAsync(promotion, new PromotionRequest(12000m));
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        Assert.Contains("W puli zostało", await tooMuch.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await scene.Operator.PostAsJsonAsync(promotion, new PromotionRequest(7000m))).StatusCode);
        Assert.Equal(ApplicationStatus.Funded, await StatusAsync(scene.Reserve));
        Assert.Contains((ApplicationStatus.Reserve, ApplicationStatus.Funded), await HistoryAsync(scene.Reserve));
        Assert.Single(scene.Emails.Sent, x => x.To == scene.ReserveEmail && x.Subject.StartsWith("Dofinansowanie z listy rezerwowej", StringComparison.Ordinal));

        // The published list follows: the resigned one is gone, the promoted one is funded.
        var published = (await scene.Host.CreateClient().GetFromJsonAsync<PublicResultsResponse>($"/public/competitions/{scene.CompetitionId}/results"))!;
        Assert.Equal([(ApplicationStatus.Funded, (decimal?)7000m)], published.Rows.Select(x => (x.Status, x.AwardedGrant)));
    }

    [RequiresDatabaseFact]
    public async Task Each_action_starts_only_from_its_own_status_and_only_for_an_operator()
    {
        var scene = await ApprovedAsync();

        Assert.Equal(HttpStatusCode.Conflict, (await scene.Operator.PostAsync($"/applications/{scene.Reserve}/resignation", content: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await scene.Operator.PostAsJsonAsync($"/applications/{scene.Funded}/promotion", new PromotionRequest(100m))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await scene.Operator.PostAsJsonAsync($"/applications/{scene.Reserve}/promotion", new PromotionRequest(0.001m))).StatusCode);

        var applicant = await LoginAsync(scene.Host, scene.FundedEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await applicant.PostAsync($"/applications/{scene.Funded}/resignation", content: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await applicant.GetAsync($"/competitions/{scene.CompetitionId}/resignations")).StatusCode);

        Assert.Equal(ApplicationStatus.Funded, await StatusAsync(scene.Funded));
    }

    [RequiresDatabaseFact]
    public async Task After_14_days_without_a_signature_the_operator_is_reminded_once()
    {
        var scene = await ApprovedAsync();

        async Task RunAsync()
        {
            await using var scope = scene.Host.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetServices<IBackgroundJob>().OfType<ContractDeadlineJob>().Single().RunAsync(CancellationToken.None);
        }

        List<EmailMessage> Reminders() =>
            [.. scene.Emails.Sent.Where(x => x.To == scene.OperatorEmail && x.Body.Contains(scene.CompetitionId.ToString()))];

        scene.Clock.Now = scene.ApprovedAt + TimeSpan.FromDays(14) - TimeSpan.FromMinutes(1);
        await RunAsync();
        Assert.Empty(Reminders());

        scene.Clock.Now = scene.ApprovedAt + TimeSpan.FromDays(14);
        await RunAsync();
        var reminder = Assert.Single(Reminders());
        Assert.Contains("nie mają podpisanej umowy", reminder.Body);

        scene.Clock.Now += TimeSpan.FromHours(3);
        await RunAsync();
        Assert.Single(Reminders());

        var overview = (await (await LoginAsync(scene.Host, scene.OperatorEmail))
            .GetFromJsonAsync<ResignationsResponse>($"/competitions/{scene.CompetitionId}/resignations"))!;
        Assert.True(Assert.Single(overview.Unsigned).Overdue);
    }
}
