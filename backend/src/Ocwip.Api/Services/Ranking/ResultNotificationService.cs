using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services.Ranking;

/// <summary>The result mails of a competition (T-43): their text, their state and the resumable sending.</summary>
internal interface IResultNotificationService
{
    /// <summary>Null for no such competition.</summary>
    Task<ResultMessagesResponse?> MessagesAsync(Guid competitionId, CancellationToken cancellationToken);

    /// <summary>Null for no such competition; errors keyed by field for a text too long.</summary>
    Task<(ResultMessagesResponse? Messages, IDictionary<string, string[]>? Errors)> SaveMessagesAsync(
        Guid competitionId, ResultMessagesRequest request, CancellationToken cancellationToken);

    Task<ResultNotificationsResponse?> StateAsync(Guid competitionId, CancellationToken cancellationToken);

    /// <summary>
    /// Sends every mail still owed, one by one, and can be run again at any
    /// moment: a row already sent is never taken, and a row another run holds
    /// is left alone until its claim goes stale.
    /// </summary>
    Task<ResultNotificationsResponse?> SendPendingAsync(Guid competitionId, CancellationToken cancellationToken);
}

internal sealed class ResultNotificationService(AppDbContext context, IEmailSender sender, TimeProvider time)
    : IResultNotificationService
{
    internal const int MessageMaxLength = 4000;

    /// <summary>How long a claim holds: longer than one mail takes, short enough to resume after a crash.</summary>
    internal static readonly TimeSpan ClaimLifetime = TimeSpan.FromMinutes(10);

    public async Task<ResultMessagesResponse?> MessagesAsync(Guid competitionId, CancellationToken cancellationToken) =>
        await context.Competitions.AsNoTracking()
            .Where(x => x.Id == competitionId)
            .Select(x => new ResultMessagesResponse(x.ResultEmailFunded, x.ResultEmailReserve, x.ResultEmailRejected))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<(ResultMessagesResponse? Messages, IDictionary<string, string[]>? Errors)> SaveMessagesAsync(
        Guid competitionId, ResultMessagesRequest request, CancellationToken cancellationToken)
    {
        var competition = await context.Competitions.FirstOrDefaultAsync(x => x.Id == competitionId, cancellationToken);
        if (competition is null)
        {
            return (null, null);
        }

        var errors = new Dictionary<string, string[]>();
        string? Text(string field, string? value)
        {
            var text = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (text is { Length: > MessageMaxLength })
            {
                errors[field] = [$"Treść może mieć najwyżej {MessageMaxLength} znaków."];
            }

            return text;
        }

        var funded = Text("funded", request.Funded);
        var reserve = Text("reserve", request.Reserve);
        var rejected = Text("rejected", request.Rejected);

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        competition.ResultEmailFunded = funded;
        competition.ResultEmailReserve = reserve;
        competition.ResultEmailRejected = rejected;
        await context.SaveChangesAsync(cancellationToken);

        return (new ResultMessagesResponse(funded, reserve, rejected), null);
    }

    public async Task<ResultNotificationsResponse?> StateAsync(Guid competitionId, CancellationToken cancellationToken)
    {
        if (!await context.Competitions.AnyAsync(x => x.Id == competitionId, cancellationToken))
        {
            return null;
        }

        var rows = await context.ResultNotifications.AsNoTracking()
            .Where(x => x.CompetitionId == competitionId)
            .Select(x => new { x.SentAt, x.LastError })
            .ToListAsync(cancellationToken);

        return new ResultNotificationsResponse(
            rows.Count,
            rows.Count(x => x.SentAt != null),
            rows.Count(x => x.SentAt == null),
            rows.Count(x => x.SentAt == null && x.LastError != null),
            rows.Max(x => x.SentAt));
    }

    public async Task<ResultNotificationsResponse?> SendPendingAsync(Guid competitionId, CancellationToken cancellationToken)
    {
        var competition = await context.Competitions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == competitionId, cancellationToken);
        if (competition is null)
        {
            return null;
        }

        var owed = await context.ResultNotifications.AsNoTracking()
            .Where(x => x.CompetitionId == competitionId && x.SentAt == null)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in owed)
        {
            var now = time.GetUtcNow();
            var stale = now - ClaimLifetime;

            // Taken or skipped in one statement: two runs at once cannot both
            // take the same row.
            var claimed = await context.ResultNotifications
                .Where(x => x.Id == id && x.SentAt == null && (x.ClaimedAt == null || x.ClaimedAt < stale))
                .ExecuteUpdateAsync(
                    s => s.SetProperty(x => x.ClaimedAt, now).SetProperty(x => x.Attempts, x => x.Attempts + 1),
                    cancellationToken);

            if (claimed != 1)
            {
                continue;
            }

            string? error = null;
            try
            {
                var message = await MessageAsync(id, competition, cancellationToken);
                if (message is null)
                {
                    error = "Konto, które złożyło wniosek, nie ma adresu e-mail.";
                }
                else
                {
                    await sender.SendAsync(message, cancellationToken);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The type only: an exception message may carry the address
                // or the body, and this column is read by the operator.
                error = $"Wysyłka nie powiodła się ({exception.GetType().Name}). Spróbuj ponownie.";
            }

            await context.ResultNotifications
                .Where(x => x.Id == id)
                .ExecuteUpdateAsync(
                    s =>
                    {
                        if (error is null)
                        {
                            s.SetProperty(x => x.SentAt, time.GetUtcNow()).SetProperty(x => x.LastError, (string?)null);
                        }
                        else
                        {
                            // Released at once, so the next run retries it.
                            s.SetProperty(x => x.ClaimedAt, (DateTimeOffset?)null).SetProperty(x => x.LastError, error);
                        }
                    },
                    cancellationToken);
        }

        return await StateAsync(competitionId, cancellationToken);
    }

    private async Task<EmailMessage?> MessageAsync(Guid notificationId, Competition competition, CancellationToken cancellationToken)
    {
        var row = await context.ResultNotifications.AsNoTracking()
            .Where(x => x.Id == notificationId)
            .Select(x => new
            {
                x.Result,
                Application = context.Applications
                    .Where(a => a.Id == x.ApplicationId)
                    .Select(a => new { a.Number, a.AwardedGrant })
                    .First(),
                // The account that submitted it: the one the confirmation of
                // submission went to (T-33).
                Email = context.ApplicationStatusHistory
                    .Where(h => h.ApplicationId == x.ApplicationId && h.ToStatus == ApplicationStatus.Submitted)
                    .OrderByDescending(h => h.ChangedAt)
                    .Select(h => h.ChangedByUser.Email)
                    .FirstOrDefault(),
            })
            .FirstAsync(cancellationToken);

        return string.IsNullOrEmpty(row.Email)
            ? null
            : ResultEmail.Compose(row.Email, competition, row.Result, row.Application.Number, row.Application.AwardedGrant);
    }
}
