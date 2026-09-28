using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;
using Ocwip.Api.Services.Ranking;

namespace Ocwip.Api.Services.Jobs;

/// <summary>
/// The second consumer of the background jobs (T-109): once the 14 days from
/// the publication of the results have passed, the competition's contact
/// people (or, without any, every operator) get one mail listing the funded
/// applications still without a signed contract. The mail only reminds; the
/// resignation is the operator's (ResignationService).
///
/// One run per recipient and competition, so a refused mail to one person
/// never sends a second one to another on the retry. The subject id is
/// derived from both ids, since a run is keyed by one.
/// </summary>
internal sealed class ContractDeadlineJob(
    AppDbContext context, TimeProvider time, IEmailSender email, IConfiguration configuration) : IBackgroundJob
{
    public const string JobName = "contract-deadline";

    public string Name => JobName;

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var since = now - ResignationService.ContractWindow;

        var competitions = await context.Competitions.AsNoTracking()
            .Where(x => x.IsActive && x.ResultsApprovedAt != null && x.ResultsApprovedAt <= since)
            .Where(x => context.Applications.Any(a => a.CompetitionId == x.Id && a.IsActive && a.Status == ApplicationStatus.Funded))
            .ToListAsync(cancellationToken);

        var done = 0;
        foreach (var competition in competitions)
        {
            var dueAt = ResignationService.DeadlineOf(competition)!.Value;
            var unsigned = await context.Applications.AsNoTracking()
                .Where(x => x.CompetitionId == competition.Id && x.IsActive && x.Status == ApplicationStatus.Funded)
                .OrderBy(x => x.Number)
                .Select(x => new { x.Number, x.Entity.Name })
                .ToListAsync(cancellationToken);

            foreach (var (userId, to) in await RecipientsAsync(competition.Id, cancellationToken))
            {
                var body = $"""
                    W konkursie "{competition.Title}" minęło 14 dni od ogłoszenia wyników ({CompetitionIntakeMessage.Moment(dueAt)}), a te dofinansowane wnioski nie mają podpisanej umowy:

                    {string.Join("\n", unsigned.Select(x => $"- {x.Number}: {x.Name}"))}

                    Zgodnie z regulaminem niepodpisana umowa oznacza rezygnację. Potwierdź ją przy wniosku i przyznaj środki kolejnemu wnioskowi z listy rezerwowej: {BaseUrl()}/panel/operator/evaluation/{competition.Id}
                    """;

                var outcome = await JobRuns.ExecuteOnceAsync(
                    context, JobName, SubjectOf(competition.Id, userId), dueAt, now,
                    token => email.SendAsync(new EmailMessage(to, $"Umowy niepodpisane w terminie: {competition.Title}", body), token),
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
