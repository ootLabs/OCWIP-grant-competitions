using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services.Ranking;

internal enum DeclarationOutcome
{
    Succeeded,
    CompetitionNotFound,
    AlreadyDecided,
    Invalid,
}

internal sealed record DeclarationResult(
    DeclarationOutcome Outcome,
    DeclarationResponse? Declaration = null,
    IDictionary<string, string[]>? Errors = null);

/// <summary>Impartiality declarations of experts (T-40a).</summary>
internal interface IDeclarationService
{
    Task<DeclarationResult> GetAsync(Guid competitionId, Guid reviewerId, CancellationToken cancellationToken);

    Task<DeclarationResult> DecideAsync(
        Guid competitionId, Guid reviewerId, DeclarationDecisionRequest request, CancellationToken cancellationToken);

    /// <summary>Every expert assigned in the competition, with their declaration, for the operator.</summary>
    Task<IReadOnlyList<DeclarationRow>?> ListAsync(Guid competitionId, CancellationToken cancellationToken);
}

internal sealed class DeclarationService(AppDbContext context, TimeProvider time) : IDeclarationService
{
    public async Task<DeclarationResult> GetAsync(Guid competitionId, Guid reviewerId, CancellationToken cancellationToken)
    {
        var row = await ActiveAsync(competitionId, reviewerId, cancellationToken);

        // P4-21: only an expert on this competition's committee, or one who
        // has already decided there, reads its declaration. Anybody else gets
        // the answer an unknown id gets, so the route does not tell an expert
        // which competitions exist, drafts included (AGENTS.md rule 1).
        var concerned = row is not null || await OnCommitteeAsync(competitionId, reviewerId, cancellationToken);

        if (!concerned || !await context.Competitions.AnyAsync(x => x.Id == competitionId, cancellationToken))
        {
            return new DeclarationResult(DeclarationOutcome.CompetitionNotFound);
        }

        return new DeclarationResult(DeclarationOutcome.Succeeded, Response(competitionId, row));
    }

    public async Task<DeclarationResult> DecideAsync(
        Guid competitionId, Guid reviewerId, DeclarationDecisionRequest request, CancellationToken cancellationToken)
    {
        var competition = await context.Competitions
            .FirstOrDefaultAsync(x => x.Id == competitionId && x.IsActive, cancellationToken);

        // S-41: the same rule as reading. A declaration is a committee
        // member's; anybody else learnt from 200 against 404 that the
        // competition exists, and left a declaration behind in it.
        // An expert who already decided here, even one since removed from the
        // committee, gets the same answer as on reading: 409 below, not 404.
        if (competition is null
            || (await ActiveAsync(competitionId, reviewerId, cancellationToken) is null
                && !await OnCommitteeAsync(competitionId, reviewerId, cancellationToken)))
        {
            return new DeclarationResult(DeclarationOutcome.CompetitionNotFound);
        }

        var reason = request.RefusalReason?.Trim();

        if (!request.Accept && string.IsNullOrEmpty(reason))
        {
            return Invalid("Odmowa wymaga podania powodu.");
        }

        if (reason is { Length: > 1000 })
        {
            return Invalid("Powód odmowy może mieć najwyżej 1000 znaków.");
        }

        if (await ActiveAsync(competitionId, reviewerId, cancellationToken) is not null)
        {
            return new DeclarationResult(DeclarationOutcome.AlreadyDecided);
        }

        var row = new ReviewerDeclaration
        {
            CompetitionId = competitionId,
            ReviewerId = reviewerId,
            Accepted = request.Accept,
            RefusalReason = request.Accept ? null : reason,
            DeclarationText = ImpartialityDeclaration.WorkingText,
            DecidedAt = time.GetUtcNow(),
        };

        context.ReviewerDeclarations.Add(row);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "ux_reviewer_declarations_one_active",
        })
        {
            // Two tabs deciding at once: the first decision stands.
            return new DeclarationResult(DeclarationOutcome.AlreadyDecided);
        }

        return new DeclarationResult(DeclarationOutcome.Succeeded, Response(competitionId, row));
    }

    /// <summary>Appointed to the committee, or assigned there (an assignment appoints as well).</summary>
    private async Task<bool> OnCommitteeAsync(Guid competitionId, Guid reviewerId, CancellationToken cancellationToken) =>
        await context.CompetitionExperts.AnyAsync(
            x => x.IsActive && x.UserId == reviewerId && x.CompetitionId == competitionId,
            cancellationToken)
        || await context.ApplicationAssignments.AnyAsync(
            a => a.IsActive && a.ReviewerId == reviewerId && a.Application.CompetitionId == competitionId,
            cancellationToken);

    public async Task<IReadOnlyList<DeclarationRow>?> ListAsync(Guid competitionId, CancellationToken cancellationToken)
    {
        if (!await context.Competitions.AnyAsync(x => x.Id == competitionId, cancellationToken))
        {
            return null;
        }

        // The experts with any active assignment in this competition, and
        // anybody who decided there even if no longer assigned.
        var reviewerIds = await context.ApplicationAssignments.AsNoTracking()
            .Where(a => a.IsActive && a.Application.CompetitionId == competitionId)
            .Select(a => a.ReviewerId)
            .Union(context.ReviewerDeclarations
                .Where(d => d.CompetitionId == competitionId && d.IsActive)
                .Select(d => d.ReviewerId))
            .ToListAsync(cancellationToken);

        var users = await context.Users.AsNoTracking()
            .Where(u => reviewerIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

        var declarations = await context.ReviewerDeclarations.AsNoTracking()
            .Where(d => d.CompetitionId == competitionId && d.IsActive)
            .ToDictionaryAsync(d => d.ReviewerId, cancellationToken);

        return users
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Select(u =>
            {
                declarations.TryGetValue(u.Id, out var d);
                return new DeclarationRow(
                    u.Id,
                    $"{u.FirstName} {u.LastName}".Trim(),
                    u.Email ?? string.Empty,
                    Status(d),
                    d?.RefusalReason,
                    d?.DecidedAt);
            })
            .ToList();
    }

    private Task<ReviewerDeclaration?> ActiveAsync(Guid competitionId, Guid reviewerId, CancellationToken cancellationToken) =>
        context.ReviewerDeclarations.AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.CompetitionId == competitionId && d.ReviewerId == reviewerId && d.IsActive,
                cancellationToken);

    internal static DeclarationStatus Status(ReviewerDeclaration? row) =>
        row is null ? DeclarationStatus.NotDecided
        : row.Accepted ? DeclarationStatus.Accepted
        : DeclarationStatus.Refused;

    private static DeclarationResponse Response(Guid competitionId, ReviewerDeclaration? row) =>
        new(
            competitionId,
            Status(row),
            row?.DeclarationText ?? ImpartialityDeclaration.WorkingText,
            row?.RefusalReason,
            row?.DecidedAt);

    private static DeclarationResult Invalid(string message) =>
        new(DeclarationOutcome.Invalid, Errors: new Dictionary<string, string[]> { ["refusalReason"] = [message] });
}
