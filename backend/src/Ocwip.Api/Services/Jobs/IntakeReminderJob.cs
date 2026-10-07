using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services.Jobs;

/// <summary>One job the scheduler runs on every tick; idempotent through JobRuns.</summary>
internal interface IBackgroundJob
{
    string Name { get; }

    /// <summary>How many runs this call did.</summary>
    Task<int> RunAsync(CancellationToken cancellationToken);
}

/// <summary>
/// R-09 (T-105): one reminder, three days before the intake closes, to
/// whoever started an application in it and has not submitted it. A
/// continuous intake has no closing moment and gets none; a submitted or
/// withdrawn application gets none either.
///
/// The due moment is the closing time less three days, so moving the closing
/// date makes a new run: the applicant hears about the date that holds.
/// The wording is fixed for now: the competition has no field for its own
/// (R-09 says "treść ustawiana przy konkursie", left open in rozbieznosci.md).
/// </summary>
internal sealed class IntakeReminderJob(
    AppDbContext context, TimeProvider time, IEmailSender email, IConfiguration configuration) : IBackgroundJob
{
    public const string JobName = "intake-reminder";
    public static readonly TimeSpan Ahead = TimeSpan.FromDays(3);

    public string Name => JobName;

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var soon = now + Ahead;

        var candidates = await context.Competitions.AsNoTracking()
            .Where(x => x.IsActive && !x.IsContinuousIntake && x.EndDate != null && x.EndDate > now && x.EndDate <= soon)
            .ToListAsync(cancellationToken);

        // Open by the lifecycle, not only by the dates: a competition closed
        // by hand before its date takes no applications and needs no reminder.
        var open = candidates.Where(x => CompetitionIntake.For(x, now).AcceptsApplications).ToList();

        var done = 0;
        foreach (var competition in open)
        {
            var dueAt = competition.EndDate!.Value - Ahead;
            var drafts = await context.Applications.AsNoTracking()
                .Where(x => x.CompetitionId == competition.Id && x.IsActive && x.Status == ApplicationStatus.Draft)
                .Select(x => new { x.Id, x.EntityId })
                .ToListAsync(cancellationToken);

            foreach (var draft in drafts)
            {
                // Everybody with access to the card (T-93a): any of them may
                // finish the draft. One run per draft and person, so a relay
                // refusing one address neither stops the others nor sends
                // anybody a second copy on the retry.
                var cards = context.EntityMembers.Where(m => m.EntityId == draft.EntityId && m.IsActive);
                var recipients = await context.Users.AsNoTracking()
                    .Where(x => cards.Any(m => m.UserId == x.Id) && x.IsActive && x.EmailConfirmed && x.Email != null)
                    .Select(x => new { x.Id, x.Email })
                    .ToListAsync(cancellationToken);

                foreach (var recipient in recipients)
                {
                    var outcome = await JobRuns.ExecuteOnceAsync(
                        context, JobName, JobRuns.SubjectFor(draft.Id, recipient.Id), dueAt, now,
                        token => email.SendAsync(Message(recipient.Email!, competition, draft.Id), token),
                        cancellationToken);

                    if (outcome is JobRunOutcome.Done)
                    {
                        done++;
                    }
                }
            }
        }

        return done;
    }

    private EmailMessage Message(string to, Competition competition, Guid applicationId)
    {
        var baseUrl = (configuration["EmailVerification:FrontendBaseUrl"] is { Length: > 0 } configured
            ? configured
            : "http://localhost:3000").TrimEnd('/');

        var body = $"""
            Nabór w konkursie "{competition.Title}" kończy się {CompetitionIntakeMessage.Moment(competition.EndDate!.Value)}.

            Twój wniosek w tym konkursie nie jest jeszcze złożony. Wersja robocza nie jest wnioskiem: po zamknięciu naboru nie będzie jej można złożyć.

            Wniosek: {baseUrl}/panel/applicant/applications/{applicationId}
            """;

        // The closing moment, not "in 3 days": a closing date set or moved to
        // less than three days ahead, or a scheduler that was down, sends
        // the reminder later, and the subject still has to be true.
        return new EmailMessage(
            to, $"Przypomnienie: nabór \"{competition.Title}\" kończy się {CompetitionIntakeMessage.Moment(competition.EndDate!.Value)}", body);
    }
}
