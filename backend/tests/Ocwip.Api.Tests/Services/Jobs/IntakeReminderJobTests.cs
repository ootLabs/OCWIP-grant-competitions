using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Services;
using Ocwip.Api.Services.Jobs;
using Ocwip.Api.Tests.Data;
using Ocwip.Api.Tests.Endpoints;
using Ocwip.Api.Tests.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Endpoints.ApplicationTestHost;

namespace Ocwip.Api.Tests.Services.Jobs;

/// <summary>
/// R-09 through the background jobs (T-105): one reminder three days before
/// the intake closes, to a started and unsubmitted application only, at most
/// once however often the scheduler looks, and not a second one after a
/// crash in the middle of sending.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class IntakeReminderJobTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    : IClassFixture<OcwipWebApplicationFactory>
{
    private static readonly DateTimeOffset Due = CompetitionTestHost.End - IntakeReminderJob.Ahead;

    private sealed record Scene(
        WebApplicationFactory<Program> Host, FixedTimeProvider Clock, RecordingEmailSender Emails,
        Guid DraftId, string DraftEmail, string SubmittedEmail);

    private async Task<Scene> SceneAsync(IEmailSender? sender = null)
    {
        var emails = new RecordingEmailSender();
        var (host, clock) = CompetitionTestHost.Create(factory, database, s => s.AddSingleton(sender ?? emails));
        var competition = await PublishedCompetitionWithFormAsync(host);
        clock.Now = CompetitionTestHost.Start.AddDays(1);

        var (started, _, draftEmail) = await SeedApplicantAsync(host, database);
        var draft = await CreateAsync(started, competition.Id);

        var (done, _, submittedEmail) = await SeedApplicantAsync(host, database);
        var submitted = await CreateAsync(done, competition.Id);
        await SaveAsync(done, submitted.Id, FormDefinitionSamples.Parse("""{"opis":"Gotowe"}"""));
        (await done.PostAsync($"/applications/{submitted.Id}/submit", content: null)).EnsureSuccessStatusCode();

        return new Scene(host, clock, emails, draft.Id, draftEmail, submittedEmail);
    }

    private static async Task<int> RunAsync(WebApplicationFactory<Program> host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var job = scope.ServiceProvider.GetServices<IBackgroundJob>().OfType<IntakeReminderJob>().Single();
        return await job.RunAsync(CancellationToken.None);
    }

    private List<string> RemindersTo(Scene scene) =>
        [.. scene.Emails.Sent.Where(x => x.Subject.StartsWith("Przypomnienie", StringComparison.Ordinal)).Select(x => x.To)];

    [RequiresDatabaseFact]
    public async Task One_reminder_goes_to_each_started_unsubmitted_application_three_days_ahead()
    {
        var scene = await SceneAsync();

        scene.Clock.Now = Due.AddMinutes(-1);
        Assert.Equal(0, await RunAsync(scene.Host));
        Assert.Empty(RemindersTo(scene));

        scene.Clock.Now = Due;
        Assert.True(await RunAsync(scene.Host) >= 1);
        Assert.Contains(scene.DraftEmail, RemindersTo(scene));
        Assert.DoesNotContain(scene.SubmittedEmail, RemindersTo(scene));

        // However often the scheduler looks afterwards: once.
        scene.Clock.Now = Due.AddHours(5);
        await RunAsync(scene.Host);
        Assert.Single(RemindersTo(scene), scene.DraftEmail);

        var mail = scene.Emails.Sent.Single(x => x.To == scene.DraftEmail && x.Subject.StartsWith("Przypomnienie", StringComparison.Ordinal));
        Assert.Contains($"/panel/applicant/applications/{scene.DraftId}", mail.Body);

        // After the intake nothing is due any more.
        scene.Clock.Now = CompetitionTestHost.End;
        Assert.Equal(0, await RunAsync(scene.Host));
    }

    [RequiresDatabaseFact]
    public async Task A_run_claimed_by_a_process_that_died_is_not_sent_again()
    {
        var scene = await SceneAsync();
        await using (var context = database.CreateContext())
        {
            // What a restart in the middle of sending leaves: claimed, never completed.
            context.ScheduledJobRuns.Add(new ScheduledJobRun
            {
                Job = IntakeReminderJob.JobName, SubjectId = scene.DraftId, DueAt = Due, ClaimedAt = Due, Attempts = 1,
            });
            await context.SaveChangesAsync();
        }

        scene.Clock.Now = Due.AddMinutes(10);
        await RunAsync(scene.Host);

        Assert.DoesNotContain(scene.DraftEmail, RemindersTo(scene));
    }

    [RequiresDatabaseFact]
    public async Task A_refused_send_is_released_with_its_error_and_retried()
    {
        var failing = new FailingOnceEmailSender();
        var scene = await SceneAsync(failing);

        scene.Clock.Now = Due;
        await RunAsync(scene.Host);

        await using (var context = database.CreateContext())
        {
            var run = await context.ScheduledJobRuns.AsNoTracking().SingleAsync(x => x.SubjectId == scene.DraftId);
            Assert.Null(run.ClaimedAt);
            Assert.Null(run.CompletedAt);
            Assert.Equal(nameof(InvalidOperationException), run.LastError);
        }

        scene.Clock.Now = Due.AddMinutes(1);
        await RunAsync(scene.Host);

        Assert.Single(failing.Sent, x => x.To == scene.DraftEmail && x.Subject.StartsWith("Przypomnienie", StringComparison.Ordinal));
    }

    /// <summary>Refuses the first reminder, then records like RecordingEmailSender.</summary>
    private sealed class FailingOnceEmailSender : IEmailSender
    {
        private bool _failed;
        private readonly List<EmailMessage> _sent = [];

        public IReadOnlyList<EmailMessage> Sent => _sent;

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            if (!_failed && message.Subject.StartsWith("Przypomnienie", StringComparison.Ordinal))
            {
                _failed = true;
                throw new InvalidOperationException("relay refused");
            }

            _sent.Add(message);
            return Task.CompletedTask;
        }
    }
}
