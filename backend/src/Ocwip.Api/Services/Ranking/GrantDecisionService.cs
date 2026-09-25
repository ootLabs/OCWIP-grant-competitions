using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services.Ranking;

internal enum GrantDecisionOutcome
{
    Succeeded,
    NotFound,

    /// <summary>The results are approved; amounts are frozen from then on.</summary>
    ResultsApproved,

    Invalid,

    /// <summary>Some application still waits for its evaluation, so its result cannot be written.</summary>
    EvaluationUnfinished,
}

internal sealed record GrantDecisionResult(
    GrantDecisionOutcome Outcome,
    GrantDecisionResponse? Decision = null,
    ResultsApprovalResponse? Approval = null,
    IDictionary<string, string[]>? Errors = null,
    int Unfinished = 0);

/// <summary>Grant decisions on the ranking list and the approval of the results (T-42).</summary>
internal interface IGrantDecisionService
{
    Task<GrantDecisionResult> DecideAsync(Guid applicationId, GrantDecisionRequest request, CancellationToken cancellationToken);

    Task<GrantDecisionResult> ApproveAsync(Guid competitionId, Guid operatorId, CancellationToken cancellationToken);
}

internal sealed class GrantDecisionService(AppDbContext context, IRankingService ranking, TimeProvider time)
    : IGrantDecisionService
{
    internal const int NoteMaxLength = 2000;

    public async Task<GrantDecisionResult> DecideAsync(
        Guid applicationId, GrantDecisionRequest request, CancellationToken cancellationToken)
    {
        var application = await context.Applications
            .AsNoTracking()
            .Include(x => x.Competition)
            .FirstOrDefaultAsync(
                x => x.Id == applicationId && x.IsActive && x.Status != ApplicationStatus.Draft,
                cancellationToken);

        if (application is null)
        {
            return new GrantDecisionResult(GrantDecisionOutcome.NotFound);
        }

        if (application.Competition.ResultsApprovedAt is not null)
        {
            return new GrantDecisionResult(GrantDecisionOutcome.ResultsApproved);
        }

        var errors = new Dictionary<string, string[]>();
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        if (request.AwardedGrant is { } amount && (amount <= 0m || decimal.Round(amount, 2) != amount))
        {
            errors["awardedGrant"] = ["Kwota przyznana musi być dodatnia, z dokładnością do grosza. Brak dotacji to puste pole."];
        }

        if (note is { Length: > NoteMaxLength })
        {
            errors["note"] = [$"Uwaga może mieć najwyżej {NoteMaxLength} znaków."];
        }

        if (errors.Count > 0)
        {
            return new GrantDecisionResult(GrantDecisionOutcome.Invalid, Errors: errors);
        }

        // Not through SaveChanges: that would move UpdatedAt, and UpdatedAt is
        // part of the checksum of the submitted application (D15), printed on
        // the applicant's confirmation. A decision is not a change of what
        // was submitted. The approval check sits in the same statement, so a
        // decision cannot slip in after the results were approved.
        var changed = await context.Applications
            // Status too: the approval rewrites every row it touches, so a
            // decision racing it finds the row no longer Submitted and stops.
            .Where(x => x.Id == applicationId
                && x.Status == ApplicationStatus.Submitted
                && x.Competition.ResultsApprovedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.AwardedGrant, request.AwardedGrant)
                    .SetProperty(x => x.DecisionNote, note),
                cancellationToken);

        if (changed != 1)
        {
            return new GrantDecisionResult(GrantDecisionOutcome.ResultsApproved);
        }

        return new GrantDecisionResult(
            GrantDecisionOutcome.Succeeded,
            new GrantDecisionResponse(application.Id, request.AwardedGrant, note));
    }

    public async Task<GrantDecisionResult> ApproveAsync(
        Guid competitionId, Guid operatorId, CancellationToken cancellationToken)
    {
        var list = await ranking.GetRankingAsync(competitionId, cancellationToken);

        if (list.Outcome is RankingOutcome.CompetitionNotFound || list.Ranking is null)
        {
            return new GrantDecisionResult(GrantDecisionOutcome.NotFound);
        }

        if (list.Ranking.ResultsApprovedAt is not null)
        {
            return new GrantDecisionResult(GrantDecisionOutcome.ResultsApproved);
        }

        // A result is written only where the evaluation has ended: a negative
        // formal card, or a positive one with every merit card it needs. An
        // application still being evaluated would otherwise be "rejected" for
        // no reason but timing.
        var unfinished = list.Ranking.Rows.Count(row => !Finished(row));

        if (unfinished > 0)
        {
            return new GrantDecisionResult(GrantDecisionOutcome.EvaluationUnfinished, Unfinished: unfinished);
        }

        var now = time.GetUtcNow();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        // Claimed first, conditionally: two operators approving in the same
        // moment write the statuses once, the second gets ResultsApproved.
        var claimed = await context.Competitions
            .Where(x => x.Id == competitionId && x.IsActive && x.ResultsApprovedAt == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.ResultsApprovedAt, now)
                    .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow),
                cancellationToken);

        if (claimed != 1)
        {
            return new GrantDecisionResult(GrantDecisionOutcome.ResultsApproved);
        }

        // The amount is read by the database at the moment of each UPDATE,
        // not from the ranking read above: a decision saved in between still
        // decides. Funded first; the reserve and the rejected only where no
        // amount is set.
        var ids = list.Ranking.Rows.Select(row => row.ApplicationId).ToList();
        var reserve = list.Ranking.Rows.Where(row => Result(row with { AwardedGrant = null }) == ApplicationStatus.Reserve)
            .Select(row => row.ApplicationId)
            .ToList();
        var pending = context.Applications.Where(x => ids.Contains(x.Id) && x.Status == ApplicationStatus.Submitted);

        await pending.Where(x => x.AwardedGrant != null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ApplicationStatus.Funded), cancellationToken);
        await pending.Where(x => reserve.Contains(x.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ApplicationStatus.Reserve), cancellationToken);
        await pending
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ApplicationStatus.Rejected), cancellationToken);

        var results = await context.Applications.AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Status, cancellationToken);

        foreach (var (applicationId, to) in results)
        {
            context.ApplicationStatusHistory.Add(new ApplicationStatusHistory
            {
                Id = Guid.NewGuid(),
                ApplicationId = applicationId,
                FromStatus = ApplicationStatus.Submitted,
                ToStatus = to,
                ChangedAt = now,
                ChangedByUserId = operatorId,
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new GrantDecisionResult(
            GrantDecisionOutcome.Succeeded,
            Approval: new ResultsApprovalResponse(
                now,
                results.Values.Count(x => x == ApplicationStatus.Funded),
                results.Values.Count(x => x == ApplicationStatus.Reserve),
                results.Values.Count(x => x == ApplicationStatus.Rejected)));
    }

    private static bool Finished(RankingRow row) =>
        row.Formal == FormalStanding.Failed
        || (row.Formal == FormalStanding.Passed && row.MeritCardsFinished >= row.MeritCardsRequired);

    /// <summary>
    /// An amount funds (report); otherwise a place on the list above the
    /// threshold is the reserve list (ZR-09); everything else is rejected.
    /// </summary>
    internal static ApplicationStatus Result(RankingRow row) =>
        row.AwardedGrant is not null ? ApplicationStatus.Funded
        : row.Rank is not null && row.PassesThreshold != false ? ApplicationStatus.Reserve
        : ApplicationStatus.Rejected;
}
