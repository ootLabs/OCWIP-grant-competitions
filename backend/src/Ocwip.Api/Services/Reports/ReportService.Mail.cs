using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;

namespace Ocwip.Api.Services.Reports;

/// <summary>
/// What the applicant is told about their report (T-50a, T-50b). Returning a
/// report and accepting it both put the next move on the applicant's side, so
/// both are mailed, the way returning an application is
/// (ApplicationReturnService.NotifyAsync): a state change nobody is told
/// about is one the applicant learns of only by opening the panel, and a
/// report is opened once every few months.
///
/// Sent after the commit, so a mail relay that is down cannot roll back a
/// decision the operator already made.
/// </summary>
internal sealed partial class ReportService
{
    private async Task NotifyReturnedAsync(Guid reportId, string reason, CancellationToken cancellationToken)
    {
        if (await AddressedAsync(reportId, cancellationToken) is not { } sent)
        {
            return;
        }

        var body = $"""
            Sprawozdanie z wniosku {sent.Number} w konkursie "{sent.Competition}" zostało zwrócone do poprawy.

            Co poprawić:
            {reason}

            Poprawione sprawozdanie trzeba złożyć ponownie: poprawka bez ponownego złożenia się nie liczy.
            """;

        await email.SendAsync(
            new EmailMessage(sent.Email, $"Sprawozdanie z wniosku {sent.Number} zwrócone do poprawy", body),
            cancellationToken);
    }

    private async Task NotifyAcceptedAsync(
        Guid reportId, ReportSettlementResponse? settlement, CancellationToken cancellationToken)
    {
        if (await AddressedAsync(reportId, cancellationToken) is not { } sent)
        {
            return;
        }

        // The refund is the one number the applicant has to act on, so it is
        // in the mail rather than only on the screen. No refund: nothing is
        // said about it, instead of "do zwrotu: 0,00 zł".
        var refund = settlement?.Refund is { } amount && amount > 0m
            ? $"\n\nDo zwrotu zostaje {PolishNumbers.Amount(amount)}. Rozliczenie pozycja po pozycji jest w Generatorze konkursów, przy sprawozdaniu."
            : string.Empty;

        var body = $"""
            Sprawozdanie z wniosku {sent.Number} w konkursie "{sent.Competition}" zostało przyjęte, a dotacja rozliczona.{refund}
            """;

        await email.SendAsync(
            new EmailMessage(sent.Email, $"Sprawozdanie z wniosku {sent.Number} przyjęte", body),
            cancellationToken);
    }

    /// <summary>
    /// The account that submitted the report, the application number and the
    /// competition; null when nothing can be addressed, which leaves the
    /// decision recorded and sends nothing.
    /// </summary>
    private async Task<(string Email, string? Number, string Competition)?> AddressedAsync(
        Guid reportId, CancellationToken cancellationToken)
    {
        var report = await context.Reports.AsNoTracking()
            .Where(x => x.Id == reportId)
            .Select(x => new { x.Application.Number, Competition = x.Application.Competition.Title })
            .FirstOrDefaultAsync(cancellationToken);

        if (report is null)
        {
            return null;
        }

        // Joined by hand: ReportStatusHistory carries the account's id, not a
        // navigation to it.
        var address = await (from history in context.ReportStatusHistory.AsNoTracking()
                             join user in context.Users on history.ChangedByUserId equals user.Id
                             where history.ReportId == reportId && history.ToStatus == ReportStatus.Submitted
                             orderby history.ChangedAt descending
                             select user.Email)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrEmpty(address) ? null : (address, report.Number, report.Competition);
    }
}
