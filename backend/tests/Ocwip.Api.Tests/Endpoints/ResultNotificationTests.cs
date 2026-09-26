using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;
using static Ocwip.Api.Tests.Endpoints.EvaluationScene;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// T-43: every result gets its mail, the refusal too; a run that fails
/// halfway is resumed without anybody getting the same mail twice; the
/// applicant sees the awarded amount only once funded.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ResultNotificationTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public ResultNotificationTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task A_run_that_fails_halfway_is_resumed_without_a_second_mail()
    {
        var mails = new FailingOnceEmailSender();
        var (host, clock) = CompetitionTestHost.Create(
            _factory, _database, services => services.AddSingleton<IEmailSender>(mails));
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var (fundedApplicant, funded, fundedEmail) = await SubmittedAsync(host, _database, competition.Id);
        var (_, reserve, reserveEmail) = await SubmittedAsync(host, _database, competition.Id);
        var (_, rejected, rejectedEmail) = await SubmittedAsync(host, _database, competition.Id);

        var operatorClient = await CompetitionTestHost.SignedInAs(host, Role.Operator);
        await PrepareAsync(operatorClient, competition.Id);
        await FormalAsync(operatorClient, funded, passed: true);
        await FormalAsync(operatorClient, reserve, passed: true);
        await FormalAsync(operatorClient, rejected, passed: false);
        var (expert, expertId) = await SeedReviewerAsync(host);
        await AcceptDeclarationAsync(expert, competition.Id);
        await ScoreAsync(operatorClient, expert, expertId, funded, 18);
        await ScoreAsync(operatorClient, expert, expertId, reserve, 12);
        (await operatorClient.PutAsJsonAsync(
            $"/applications/{funded}/grant-decision", new GrantDecisionRequest(6500m, null))).EnsureSuccessStatusCode();

        var messages = $"/competitions/{competition.Id}/result-messages";
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await operatorClient.PutAsJsonAsync(messages, new ResultMessagesRequest(new string('x', 4001), null, null))).StatusCode);
        (await operatorClient.PutAsJsonAsync(
            messages, new ResultMessagesRequest("Gratulujemy od całego zespołu OCWIP!", null, null))).EnsureSuccessStatusCode();

        Assert.Null((await fundedApplicant.GetFromJsonAsync<ApplicationResponse>($"/applications/{funded}"))!.AwardedGrant);

        (await operatorClient.PostAsync($"/competitions/{competition.Id}/results/approve", content: null))
            .EnsureSuccessStatusCode();

        var state = $"/competitions/{competition.Id}/result-notifications";
        Assert.Equal((3, 0, 3), Counts((await operatorClient.GetFromJsonAsync<ResultNotificationsResponse>(state))!));

        var send = $"/competitions/{competition.Id}/result-notifications/send";
        Assert.Equal(HttpStatusCode.Forbidden, (await expert.PostAsync(send, content: null)).StatusCode);

        // The first mail fails: the run goes on with the rest and says so.
        var first = (await (await operatorClient.PostAsync(send, content: null))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<ResultNotificationsResponse>())!;
        Assert.Equal((3, 2, 1), Counts(first));
        Assert.Equal(1, first.Failed);

        var second = (await (await operatorClient.PostAsync(send, content: null))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<ResultNotificationsResponse>())!;
        Assert.Equal((3, 3, 0), Counts(second));
        (await operatorClient.PostAsync(send, content: null)).EnsureSuccessStatusCode();

        // Exactly one mail per applicant, however many runs.
        Assert.Equal(
            new[] { fundedEmail, reserveEmail, rejectedEmail }.Order(),
            mails.Sent.Select(x => x.To).Order());

        var fundedMail = mails.Sent.Single(x => x.To == fundedEmail);
        Assert.Contains("Gratulujemy od całego zespołu OCWIP!", fundedMail.Body);
        Assert.Contains("6500,00 zł", fundedMail.Body);
        var rejectedMail = mails.Sent.Single(x => x.To == rejectedEmail);
        Assert.Contains("nie otrzymał dofinansowania", rejectedMail.Body);
        Assert.DoesNotContain("6500", rejectedMail.Body);

        Assert.Equal(6500m, (await fundedApplicant.GetFromJsonAsync<ApplicationResponse>($"/applications/{funded}"))!.AwardedGrant);
    }

    private static (int Total, int Sent, int Pending) Counts(ResultNotificationsResponse state) =>
        (state.Total, state.Sent, state.Pending);

    /// <summary>
    /// Throws on the first result mail it is given, then records like
    /// RecordingEmailSender. Only result mails: the confirmations of
    /// submission go through the same sender and are not what this is about.
    /// </summary>
    private sealed class FailingOnceEmailSender : IEmailSender
    {
        private readonly RecordingEmailSender _inner = new();
        private int _results;

        public IEnumerable<EmailMessage> Sent =>
            _inner.Sent.Where(x => x.Subject.StartsWith("Wynik konkursu", StringComparison.Ordinal));

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
            message.Subject.StartsWith("Wynik konkursu", StringComparison.Ordinal)
                && Interlocked.Increment(ref _results) == 1
                ? throw new IOException("SMTP connection reset")
                : _inner.SendAsync(message, cancellationToken);
    }
}
