using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services.Reports;

/// <summary>
/// The operator's side of a submitted report (T-50b): the review of its
/// costs and the acceptance that settles the application.
/// </summary>
internal sealed partial class ReportService
{
    public async Task<ReportResult> AcceptAsync(Guid reportId, Guid operatorId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // Taken by a conditional UPDATE, so two operators accepting at the
        // same moment settle the application once, with one history row.
        var now = time.GetUtcNow();
        var taken = await context.Reports
            .Where(x => x.Id == reportId && x.IsActive && x.Status == ReportStatus.Submitted)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.Status, ReportStatus.Accepted)
                    .SetProperty(x => x.AcceptedAt, now)
                    .SetProperty(x => x.UpdatedAt, now),
                cancellationToken);

        if (taken == 0)
        {
            return await context.Reports.AnyAsync(x => x.Id == reportId && x.IsActive, cancellationToken)
                ? new ReportResult(ReportOutcome.WrongState)
                : new ReportResult(ReportOutcome.NotFound);
        }

        var applicationId = await context.Reports
            .Where(x => x.Id == reportId)
            .Select(x => x.ApplicationId)
            .SingleAsync(cancellationToken);

        context.ReportStatusHistory.Add(new ReportStatusHistory
        {
            Id = Guid.NewGuid(),
            ReportId = reportId,
            FromStatus = ReportStatus.Submitted,
            ToStatus = ReportStatus.Accepted,
            ChangedAt = now,
            ChangedByUserId = operatorId,
        });

        // "Rozliczony" (T-50b), outside SaveChanges for the checksum reason
        // in GrantDecisionService: UpdatedAt is part of it.
        var from = await context.Applications
            .Where(x => x.Id == applicationId)
            .Select(x => x.Status)
            .SingleAsync(cancellationToken);

        if (from is ApplicationStatus.Funded or ApplicationStatus.ContractSigned
            && await context.Applications
                .Where(x => x.Id == applicationId && x.Status == from)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ApplicationStatus.Settled), cancellationToken) == 1)
        {
            context.ApplicationStatusHistory.Add(new ApplicationStatusHistory
            {
                Id = Guid.NewGuid(),
                ApplicationId = applicationId,
                FromStatus = from,
                ToStatus = ApplicationStatus.Settled,
                ChangedAt = now,
                ChangedByUserId = operatorId,
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ReportResult(ReportOutcome.Succeeded, await ReportReader.ResponseAsync(context, reportId, cancellationToken));
    }

    public async Task<ReportResult> ReviewCostsAsync(
        Guid reportId, IReadOnlyList<CostReviewItem> items, CancellationToken cancellationToken)
    {
        var (report, form, applicant) = await LoadAsync(reportId, cancellationToken);
        if (report is null)
        {
            return new ReportResult(ReportOutcome.NotFound);
        }

        if (report.Status is not ReportStatus.Submitted)
        {
            return new ReportResult(ReportOutcome.WrongState);
        }

        var (review, errors) = ReportSettlement.Check(form!, report.Answers, applicant, items);
        if (errors is not null)
        {
            return new ReportResult(ReportOutcome.Invalid, Errors: errors);
        }

        report.CostReview = ReportSettlement.Write(review!);
        await context.SaveChangesAsync(cancellationToken);
        return new ReportResult(ReportOutcome.Succeeded, await ReportReader.ResponseAsync(context, report.Id, cancellationToken));
    }
}
