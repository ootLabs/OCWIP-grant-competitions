using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;
using Ocwip.Api.Services.Ranking;

namespace Ocwip.Api.Services.Jobs;

/// <summary>
/// The second consumer of the background jobs (T-109): once 14 days have
/// passed since an application was funded (the approval, or a promotion from
/// the reserve list, which gets its own window), the competition's contact
/// people (or, without any, every operator) get one mail listing the funded
/// applications of that deadline still without a signed contract. The mail
/// only reminds; the resignation is the operator's (ResignationService).
///
/// One run per recipient, competition and deadline, so a refused mail to one
/// person never sends a second one to another on the retry, and a promotion
/// with a later deadline gets a reminder of its own. The subject id is
/// derived from both ids, since a run is keyed by one.
///
/// Only deadlines of the last RecentWindow: without it every old competition
/// would be scanned on every tick for good, and a contact or operator added
/// months later would get a reminder about a deadline long settled.
/// </summary>
internal sealed class ContractDeadlineJob(
    AppDbContext context, TimeProvider time, IEmailSender email, IConfiguration configuration) : IBackgroundJob
{
    public const string JobName = "contract-deadline";

    public static readonly TimeSpan RecentWindow = TimeSpan.FromDays(7);

    public string Name => JobName;

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var fundedAfter = now - ResignationService.ContractWindow - RecentWindow;

        // Funded in the window that makes the deadline recent; the latest
        // move to Funded is the one that counts (ResignationService.FundedAtAsync).
        var funded = await context.Applications.AsNoTracking()
            .Where(x => x.IsActive && x.Status == ApplicationStatus.Funded
                && x.Competition.IsActive && x.Competition.ResultsApprovedAt != null
                && context.ApplicationStatusHistory.Any(h => h.ApplicationId == x.Id
                    && h.ToStatus == ApplicationStatus.Funded && h.ChangedAt > fundedAfter))
            .Select(x => new { x.Id, x.CompetitionId, x.Number, x.Entity.Name })
            .ToListAsync(cancellationToken);

        var fundedAt = await ResignationService.FundedAtAsync(context, [.. funded.Select(x => x.Id)], cancellationToken);
        var due = funded
            .Select(x => (Application: x, DueAt: fundedAt[x.Id] + ResignationService.ContractWindow))
            .Where(x => x.DueAt <= now && x.DueAt > now - RecentWindow)
            .GroupBy(x => (x.Application.CompetitionId, x.DueAt))
            .ToList();

        var competitionIds = due.Select(x => x.Key.CompetitionId).Distinct().ToList();
        var titles = await context.Competitions.AsNoTracking()
            .Where(x => competitionIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Title, cancellationToken);

        var done = 0;
        foreach (var group in due.OrderBy(x => x.Key.DueAt))
        {
            var (competitionId, dueAt) = group.Key;
            var unsigned = group.Select(x => x.Application).OrderBy(x => x.Number).ToList();

            foreach (var (userId, to) in await RecipientsAsync(competitionId, cancellationToken))
            {
                var body = $"""
                    W konkursie "{titles[competitionId]}" minęło 14 dni od przyznania dotacji ({CompetitionIntakeMessage.Moment(dueAt)}), a te dofinansowane wnioski nie mają podpisanej umowy:

                    {string.Join("\n", unsigned.Select(x => $"- {x.Number}: {x.Name}"))}

                    Zgodnie z regulaminem niepodpisana umowa oznacza rezygnację. Potwierdź ją przy wniosku i przyznaj środki kolejnemu wnioskowi z listy rezerwowej: {BaseUrl()}/panel/operator/evaluation/{competitionId}
                    """;

                var outcome = await JobRuns.ExecuteOnceAsync(
                    context, JobName, SubjectOf(competitionId, userId), dueAt, now,
                    token => email.SendAsync(new EmailMessage(to, $"Umowy niepodpisane w terminie: {titles[competitionId]}", body), token),
                    cancellationToken);

                if (outcome is JobRunOutcome.Done)
                {
                    done++;
                }
            }
        }

        return done;
    }

    /// <summary>The competition's contact people; every active operator when it names none.</summary>
    private async Task<List<(Guid UserId, string Email)>> RecipientsAsync(Guid competitionId, CancellationToken cancellationToken)
    {
        var contacts = await context.Set<CompetitionContact>().AsNoTracking()
            .Where(x => x.CompetitionId == competitionId && x.IsActive && x.User.IsActive && x.User.EmailConfirmed && x.User.Email != null)
            .Select(x => new { x.UserId, x.User.Email })
            .ToListAsync(cancellationToken);

        var people = contacts.Count > 0
            ? contacts
            : await context.Users.AsNoTracking()
                .Where(x => x.Role == Role.Operator && x.IsActive && x.EmailConfirmed && x.Email != null)
                .Select(x => new { UserId = x.Id, x.Email })
                .ToListAsync(cancellationToken);

        return [.. people.Select(x => (x.UserId, x.Email!))];
    }

    internal static Guid SubjectOf(Guid competitionId, Guid userId)
    {
        Span<byte> both = stackalloc byte[32];
        competitionId.TryWriteBytes(both[..16]);
        userId.TryWriteBytes(both[16..]);
        return new Guid(SHA256.HashData(both)[..16]);
    }

    private string BaseUrl() =>
        (configuration["EmailVerification:FrontendBaseUrl"] is { Length: > 0 } configured ? configured : "http://localhost:3000")
            .TrimEnd('/');
}
