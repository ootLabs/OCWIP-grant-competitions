using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Data;

namespace Ocwip.Api.Services.Jobs;

internal enum JobRunOutcome
{
    /// <summary>This call did the work and recorded it.</summary>
    Done,

    /// <summary>Done before, taken by another run, or left by a process that died: nothing was done.</summary>
    Skipped,

    /// <summary>The work failed and said so; the claim is released for the next tick.</summary>
    Failed,
}

/// <summary>
/// The ledger every background job writes through (T-105,
/// Models/ScheduledJobRun.cs): a run is inserted once per key, claimed by one
/// conditional UPDATE (the pattern of ResultNotificationService, T-43),
/// and completed after its side effect.
///
/// Unlike the result mails, a claim that was never completed is NOT taken
/// again after a while: a result mail is owed and retried by the operator, a
/// reminder is a courtesy, and after a crash between the mail and the record
/// nobody can tell whether it went out. A second reminder is the one outcome
/// the card rules out, so a lost one is the price.
/// </summary>
internal static class JobRuns
{
    /// <summary>A relay that refuses three times will not take it the fourth.</summary>
    public const int MaxAttempts = 3;

    /// <summary>
    /// One key for a pair, when a job owes a thing per subject AND per person
    /// (a reminder about a draft to each member of its card, T-93a). The same
    /// pair always gives the same key, so the ledger still sees one run.
    /// </summary>
    public static Guid SubjectFor(Guid subject, Guid recipient)
    {
        Span<byte> pair = stackalloc byte[32];
        subject.TryWriteBytes(pair[..16]);
        recipient.TryWriteBytes(pair[16..]);
        return new Guid(System.Security.Cryptography.SHA256.HashData(pair)[..16]);
    }

    public static async Task<JobRunOutcome> ExecuteOnceAsync(
        AppDbContext context,
        string job,
        Guid subjectId,
        DateTimeOffset dueAt,
        DateTimeOffset now,
        Func<CancellationToken, Task> work,
        CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO scheduled_job_runs (job, subject_id, due_at, attempts)
            VALUES ({job}, {subjectId}, {dueAt}, 0)
            ON CONFLICT (job, subject_id, due_at) DO NOTHING
            """,
            cancellationToken);

        var claimed = await context.ScheduledJobRuns
            .Where(x => x.Job == job && x.SubjectId == subjectId && x.DueAt == dueAt
                && x.CompletedAt == null && x.ClaimedAt == null && x.Attempts < MaxAttempts)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.ClaimedAt, now).SetProperty(x => x.Attempts, x => x.Attempts + 1),
                cancellationToken);

        if (claimed != 1)
        {
            return JobRunOutcome.Skipped;
        }

        try
        {
            await work(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Released: the work said it failed, so nothing went out.
            await context.ScheduledJobRuns
                .Where(x => x.Job == job && x.SubjectId == subjectId && x.DueAt == dueAt)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.ClaimedAt, (DateTimeOffset?)null)
                        .SetProperty(x => x.LastError, exception.GetType().Name),
                    cancellationToken);
            return JobRunOutcome.Failed;
        }

        await context.ScheduledJobRuns
            .Where(x => x.Job == job && x.SubjectId == subjectId && x.DueAt == dueAt)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.CompletedAt, now).SetProperty(x => x.LastError, (string?)null),
                cancellationToken);
        return JobRunOutcome.Done;
    }
}
