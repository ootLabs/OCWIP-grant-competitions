using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services.Jobs;

/// <summary>
/// T-93a, report step 2.2: a request to join a card that nobody answered for
/// seven days goes to OCWIP. The report says "administrator"; there is no
/// such role (R-02), so it goes to every active operator, once per request
/// and operator, and they decide on the "Prośby o dostęp" screen.
///
/// The mail names nobody: who asks and for which organisation is on the
/// screen behind a login, not in a mailbox.
/// </summary>
internal sealed class EntityAccessEscalationJob(
    AppDbContext context, TimeProvider time, IEmailSender email, IConfiguration configuration) : IBackgroundJob
{
    public const string JobName = "entity-access-escalation";

    public string Name => JobName;

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var cutoff = now - EntityAccessRequest.EscalationAge;

        var overdue = await context.EntityAccessRequests.AsNoTracking()
            .Where(x => x.Status == EntityAccessRequestStatus.Pending && x.CreatedAt <= cutoff)
            .Select(x => new { x.Id, x.CreatedAt })
            .ToListAsync(cancellationToken);

        if (overdue.Count == 0)
        {
            return 0;
        }

        var operators = await context.Users.AsNoTracking()
            .Where(x => x.Role == Role.Operator && x.IsActive && x.EmailConfirmed && x.Email != null)
            .Select(x => new { x.Id, x.Email })
            .ToListAsync(cancellationToken);

        var done = 0;
        foreach (var request in overdue)
        {
            var dueAt = request.CreatedAt + EntityAccessRequest.EscalationAge;

            foreach (var account in operators)
            {
                var outcome = await JobRuns.ExecuteOnceAsync(
                    context, JobName, JobRuns.SubjectFor(request.Id, account.Id), dueAt, now,
                    token => email.SendAsync(Message(account.Email!), token),
                    cancellationToken);

                if (outcome is JobRunOutcome.Done)
                {
                    done++;
                }
            }
        }

        return done;
    }

    private EmailMessage Message(string to)
    {
        var baseUrl = (configuration["EmailVerification:FrontendBaseUrl"] is { Length: > 0 } configured
            ? configured
            : "http://localhost:3000").TrimEnd('/');

        var body = $"""
            Prośba o dostęp do karty organizacji czeka na odpowiedź od 7 dni. Osoba, która założyła kartę, jej nie rozpatrzyła.

            Przed zatwierdzeniem trzeba sprawdzić tę osobę poza systemem, na przykład telefonicznie albo w odpisie z rejestru. Sposób sprawdzenia zapisuje się przy decyzji.

            Prośby o dostęp: {baseUrl}/panel/operator/access-requests
            """;

        return new EmailMessage(to, "Prośba o dostęp do karty organizacji czeka 7 dni", body);
    }
}
