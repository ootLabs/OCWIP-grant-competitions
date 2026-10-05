using Ocwip.Api.Tests.Models.Forms;
using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
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
        DateTimeOffset ApprovedAt, Guid? SecondReserve = null);

    /// <summary>
    /// A form that states the grant applied for, which the default sample
    /// does not: the promotion ceiling (S-33) is measured against it, so a
    /// test of that ceiling needs an application that asked for an amount.
    /// </summary>
    private static JsonElement FormWithRequestedGrant() => FormDefinitionSamples.Parse($$"""
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

    private const string AskedFor = """{"opis":"projekt","dotacja":3000.00}""";

    private async Task<Scene> ApprovedAsync(bool secondReserve = false, bool withRequestedGrant = false)
    {
        var emails = new RecordingEmailSender();
        var (host, clock) = CompetitionTestHost.Create(factory, database, s => s.AddSingleton<IEmailSender>(emails));
        var competition = withRequestedGrant
            ? await PublishedCompetitionWithFormAsync(host, FormWithRequestedGrant())
            : await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var answers = withRequestedGrant ? AskedFor : null;
        var (_, funded, fundedEmail) = await SubmittedAsync(host, database, competition.Id, answers);
        var (_, reserve, reserveEmail) = await SubmittedAsync(host, database, competition.Id, answers);
        Guid? second = secondReserve ? (await SubmittedAsync(host, database, competition.Id, answers)).Id : null;

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
        if (second is { } other)
        {
            await FormalAsync(operatorClient, other, passed: true);
            await ScoreAsync(operatorClient, expert, expertId, other, 11);
        }
        (await operatorClient.PutAsJsonAsync(
            $"/applications/{funded}/grant-decision", new GrantDecisionRequest(6500m, null))).EnsureSuccessStatusCode();
        await StartReviewAsync(operatorClient, competition.Id);
        (await operatorClient.PostAsync($"/competitions/{competition.Id}/results/approve", content: null)).EnsureSuccessStatusCode();

        return new Scene(host, clock, emails, operatorClient, operatorEmail, competition.Id,
            funded, fundedEmail, reserve, reserveEmail, clock.Now, second);
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

        Assert.True((await (await scene.Operator.PostAsync($"/applications/{scene.Funded}/resignation", content: null))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<ResignationActionResponse>())!.MailSent);
        Assert.Equal(ApplicationStatus.Resigned, await StatusAsync(scene.Funded));
        Assert.Contains((ApplicationStatus.Funded, ApplicationStatus.Resigned), await HistoryAsync(scene.Funded));
        var resignation = Assert.Single(scene.Emails.Sent, x => x.To == scene.FundedEmail && x.Subject.StartsWith("Rezygnacja", StringComparison.Ordinal));
        // Recorded before the 14 days passed: the mail does not claim a missed deadline.
        Assert.Contains("odnotował rezygnację", resignation.Body);
        Assert.DoesNotContain("nie została podpisana", resignation.Body);

        var after = (await scene.Operator.GetFromJsonAsync<ResignationsResponse>(address))!;
        Assert.Equal(0m, after.AwardedTotal);
        Assert.Equal(10000m, after.FreePool);
        Assert.Empty(after.Unsigned);

        var promotion = $"/applications/{scene.Reserve}/promotion";
        var tooMuch = await scene.Operator.PostAsJsonAsync(promotion, new PromotionRequest(12000m));
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        // The amount in the sentence is grouped the way the screens write it:
        // the operator used to read "10000,00 zł" under a field that said
        // "10 000,00 zł" (obserwacja 4). Read from the parsed problem, because
        // the JSON writer escapes the no-break space in the raw body.
        var tooMuchProblem = (await tooMuch.Content.ReadFromJsonAsync<HttpValidationProblemDetails>())!;
        Assert.Equal(
            "W puli zostało 10\u00a0000,00\u00a0zł. Kwota nie może być większa.",
            Assert.Single(tooMuchProblem.Errors["awardedGrant"]));

        Assert.True((await (await scene.Operator.PostAsJsonAsync(promotion, new PromotionRequest(7000m)))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<ResignationActionResponse>())!.MailSent);
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

    /// <summary>
    /// The reserve list is a queue (ZR-09, S-33): the operator promotes the
    /// one whose turn it is, for no more than it applied for. Both were
    /// already worked out for the screen and taken from the request on the
    /// way in, so an application from further down could be funded with an
    /// amount over the announced ceiling, leaving nothing in the history to
    /// say it happened.
    /// </summary>
    [RequiresDatabaseFact]
    public async Task A_promotion_takes_the_next_on_the_list_and_no_more_than_it_asked_for()
    {
        var scene = await ApprovedAsync(secondReserve: true, withRequestedGrant: true);
        var second = scene.SecondReserve!.Value;

        (await scene.Operator.PostAsync($"/applications/{scene.Funded}/resignation", content: null))
            .EnsureSuccessStatusCode();

        // The one further down the list waits its turn.
        var outOfOrder = await scene.Operator.PostAsJsonAsync(
            $"/applications/{second}/promotion", new PromotionRequest(1000m));
        Assert.Equal(HttpStatusCode.Conflict, outOfOrder.StatusCode);
        Assert.Contains("Następny na liście rezerwowej", await outOfOrder.Content.ReadAsStringAsync());
        Assert.Equal(ApplicationStatus.Reserve, await StatusAsync(second));

        // And the one whose turn it is cannot be given more than it applied for.
        var overview = (await scene.Operator.GetFromJsonAsync<ResignationsResponse>(
            $"/competitions/{scene.CompetitionId}/resignations"))!;
        var asked = overview.NextReserve!.RequestedGrant!.Value;

        var tooMuch = await scene.Operator.PostAsJsonAsync(
            $"/applications/{scene.Reserve}/promotion", new PromotionRequest(asked + 1m));
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        Assert.Equal(ApplicationStatus.Reserve, await StatusAsync(scene.Reserve));

        (await scene.Operator.PostAsJsonAsync($"/applications/{scene.Reserve}/promotion", new PromotionRequest(asked)))
            .EnsureSuccessStatusCode();
        Assert.Equal(ApplicationStatus.Funded, await StatusAsync(scene.Reserve));
    }

    /// <summary>
    /// After the announcement the ranking is a published fact, and it is
    /// counted from the cards on every read (S-05): the settings that compute
    /// it, the cards themselves and who fills them in are all closed. The
    /// last one is the leverage the first review did not name, and it needs
    /// no card of its own to move a result.
    /// </summary>
    [RequiresDatabaseFact]
    public async Task Approved_results_close_the_settings_the_cards_and_the_set_of_experts()
    {
        var scene = await ApprovedAsync();
        var (expert, expertId) = await SeedReviewerAsync(scene.Host);
        await AcceptDeclarationAsync(expert, scene.CompetitionId);

        var settings = await scene.Operator.PutAsJsonAsync(
            $"/competitions/{scene.CompetitionId}/evaluation-settings",
            new EvaluationSettingsRequest(1, ScoreAggregation.Sum, 1m, false, null));
        Assert.Equal(HttpStatusCode.Conflict, settings.StatusCode);

        var assign = await scene.Operator.PostAsJsonAsync(
            $"/applications/{scene.Reserve}/assignments", new AssignReviewerRequest(expertId));
        Assert.Equal(HttpStatusCode.Conflict, assign.StatusCode);

        var card = await scene.Operator.PostAsync($"/applications/{scene.Reserve}/evaluations/formal", content: null);
        Assert.Equal(HttpStatusCode.Conflict, card.StatusCode);

        // The threshold the competition was settled with stands.
        var ranking = (await scene.Operator.GetFromJsonAsync<RankingResponse>(
            $"/competitions/{scene.CompetitionId}/ranking"))!;
        Assert.Equal(ApplicationStatus.Funded, ranking.Rows.Single(x => x.ApplicationId == scene.Funded).Status);
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

    private async Task RunDeadlineJobAsync(Scene scene)
    {
        await using var scope = scene.Host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetServices<IBackgroundJob>().OfType<ContractDeadlineJob>().Single().RunAsync(CancellationToken.None);
    }

    [RequiresDatabaseFact]
    public async Task A_promotion_after_the_deadline_gets_a_window_and_a_reminder_of_its_own()
    {
        var scene = await ApprovedAsync();
        var address = $"/competitions/{scene.CompetitionId}/resignations";

        // The funded one missed its 14 days; resigned, the mail says so.
        scene.Clock.Now = scene.ApprovedAt + TimeSpan.FromDays(20);
        var operatorClient = await LoginAsync(scene.Host, scene.OperatorEmail);
        (await operatorClient.PostAsync($"/applications/{scene.Funded}/resignation", content: null)).EnsureSuccessStatusCode();
        Assert.Contains("nie została podpisana w terminie", Assert.Single(scene.Emails.Sent, x => x.To == scene.FundedEmail
            && x.Subject.StartsWith("Rezygnacja", StringComparison.Ordinal)).Body);

        (await operatorClient.PostAsJsonAsync($"/applications/{scene.Reserve}/promotion", new PromotionRequest(5000m))).EnsureSuccessStatusCode();
        var promotedAt = scene.Clock.Now;

        var overview = (await operatorClient.GetFromJsonAsync<ResignationsResponse>(address))!;
        var promoted = Assert.Single(overview.Unsigned);
        Assert.Equal(scene.Reserve, promoted.ApplicationId);
        Assert.False(promoted.Overdue);
        Assert.Equal(promotedAt + TimeSpan.FromDays(14), promoted.Deadline);

        List<EmailMessage> Reminders() =>
            [.. scene.Emails.Sent.Where(x => x.To == scene.OperatorEmail && x.Body.Contains(scene.CompetitionId.ToString()))];

        await RunDeadlineJobAsync(scene);
        Assert.Empty(Reminders());

        scene.Clock.Now = promotedAt + TimeSpan.FromDays(14);
        await RunDeadlineJobAsync(scene);
        var reminder = Assert.Single(Reminders());
        Assert.Contains(overview.Unsigned[0].Number!, reminder.Body);
    }

    [RequiresDatabaseFact]
    public async Task An_operator_added_long_after_the_deadline_is_not_reminded_of_it()
    {
        var scene = await ApprovedAsync();
        scene.Clock.Now = scene.ApprovedAt + TimeSpan.FromDays(14) + ContractDeadlineJob.RecentWindow + TimeSpan.FromMinutes(1);

        var newcomer = SessionTestHost.Email("operator-nowy");
        await SessionTestHost.CreateAccountAsync(scene.Host, newcomer, Role.Operator);
        await RunDeadlineJobAsync(scene);

        Assert.DoesNotContain(scene.Emails.Sent, x => x.To == newcomer);
        Assert.DoesNotContain(scene.Emails.Sent, x => x.To == scene.OperatorEmail && x.Body.Contains(scene.CompetitionId.ToString()));
    }

    [RequiresDatabaseFact]
    public async Task A_refused_mail_keeps_the_stored_change_and_says_so_instead_of_failing()
    {
        var scene = await ApprovedAsync();
        scene.Emails.Refuse = true;

        var response = await scene.Operator.PostAsync($"/applications/{scene.Funded}/resignation", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await response.Content.ReadFromJsonAsync<ResignationActionResponse>())!.MailSent);
        Assert.Equal(ApplicationStatus.Resigned, await StatusAsync(scene.Funded));
    }

    [RequiresDatabaseFact]
    public async Task Two_promotions_at_once_never_spend_the_same_free_money()
    {
        var scene = await ApprovedAsync(secondReserve: true);
        var second = scene.SecondReserve!.Value;
        Assert.Equal(ApplicationStatus.Reserve, await StatusAsync(second));
        await using (var context = database.CreateContext())
        {
            // A pool only one of the two reserve applications fits in.
            await context.Competitions.Where(x => x.Id == scene.CompetitionId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.TotalPoolAmount, 10000m));
        }

        (await scene.Operator.PostAsync($"/applications/{scene.Funded}/resignation", content: null)).EnsureSuccessStatusCode();

        var answers = await Task.WhenAll(
            scene.Operator.PostAsJsonAsync($"/applications/{scene.Reserve}/promotion", new PromotionRequest(7000m)),
            scene.Operator.PostAsJsonAsync($"/applications/{second}/promotion", new PromotionRequest(7000m)));

        // One goes through and one does not, whichever reason catches it
        // first: since S-33 the queue refuses the application whose turn it
        // is not (409), and the pool refuses an amount that no longer fits
        // (400). Before the queue rule both requests were about the money
        // alone, which is what this test was written for and still proves.
        Assert.Equal(1, answers.Count(x => x.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, answers.Count(x => x.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict));

        var overview = (await scene.Operator.GetFromJsonAsync<ResignationsResponse>($"/competitions/{scene.CompetitionId}/resignations"))!;
        Assert.Equal(7000m, overview.AwardedTotal);

        // And the money rule still stands on its own, now that the one left
        // on the list is the one whose turn it is: 3000 of the pool is free.
        var overTheRest = await scene.Operator.PostAsJsonAsync(
            $"/applications/{overview.NextReserve!.ApplicationId}/promotion", new PromotionRequest(7000m));
        Assert.Equal(HttpStatusCode.BadRequest, overTheRest.StatusCode);
        Assert.Contains("W puli zostało", await overTheRest.Content.ReadAsStringAsync());
    }

    [RequiresDatabaseFact]
    public async Task A_report_started_before_the_resignation_is_withdrawn_with_the_grant()
    {
        var scene = await ApprovedAsync();
        (await scene.Operator.PostAsJsonAsync($"/competitions/{scene.CompetitionId}/report-form",
            new FormDefinitionRequest(Ocwip.Api.Tests.Models.Forms.ReportFormSamples.Report()))).EnsureSuccessStatusCode();
        var applicant = await LoginAsync(scene.Host, scene.FundedEmail);
        (await applicant.PostAsync($"/applications/{scene.Funded}/report", content: null)).EnsureSuccessStatusCode();

        (await scene.Operator.PostAsync($"/applications/{scene.Funded}/resignation", content: null)).EnsureSuccessStatusCode();

        await using var context = database.CreateContext();
        Assert.False(await context.Reports.AnyAsync(x => x.ApplicationId == scene.Funded && x.IsActive));
    }

    /// <summary>
    /// A resignation between loading the contract and recording the signing:
    /// the application is no longer funded, so nothing is signed.
    /// </summary>
    [RequiresDatabaseFact]
    public async Task A_contract_is_not_signed_once_its_application_is_no_longer_funded()
    {
        var scene = await ApprovedAsync();
        (await scene.Operator.PostAsJsonAsync($"/competitions/{scene.CompetitionId}/contract-template",
            new DocumentTemplateRequest("Umowa {{numer_umowy}} z {{nazwa_realizatora}}."))).EnsureSuccessStatusCode();
        var contract = (await (await scene.Operator.PostAsync($"/applications/{scene.Funded}/contract", content: null))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<ContractResponse>())!;

        await using (var context = database.CreateContext())
        {
            await context.Applications.Where(x => x.Id == scene.Funded)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ApplicationStatus.Resigned));
        }

        var sign = await scene.Operator.PostAsJsonAsync($"/contracts/{contract.Id}/sign", new SignContractRequest(new DateOnly(2026, 5, 4)));

        Assert.Equal(HttpStatusCode.Conflict, sign.StatusCode);
        await using var check = database.CreateContext();
        Assert.Equal(ContractStatus.Draft, (await check.Contracts.AsNoTracking().SingleAsync(x => x.Id == contract.Id)).Status);
    }
}
