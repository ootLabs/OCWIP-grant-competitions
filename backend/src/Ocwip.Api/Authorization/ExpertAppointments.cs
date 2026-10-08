using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Authorization;

/// <summary>
/// The one answer to "may this account act as an expert here" (R-44,
/// report step 5.1). An expert is an appointment to one competition, not a
/// kind of account, so the handlers ask here instead of reading the role.
/// </summary>
internal static class ExpertAppointments
{
    /// <summary>Appointed to at least one competition: the account gets the expert's panel.</summary>
    public static Task<bool> IsExpertAnywhereAsync(AppDbContext context, Guid userId, CancellationToken cancellationToken) =>
        context.CompetitionExperts.AnyAsync(x => x.UserId == userId && x.IsActive, cancellationToken);

    public static Task<bool> IsAppointedAsync(
        AppDbContext context, Guid userId, Guid competitionId, CancellationToken cancellationToken) =>
        context.CompetitionExperts.AnyAsync(
            x => x.UserId == userId && x.CompetitionId == competitionId && x.IsActive, cancellationToken);

    /// <summary>
    /// What lets an expert read one application and its card: appointed to
    /// its competition, assigned to it, and past the impartiality
    /// declaration for that competition (T-37, T-40a).
    ///
    /// And never for an organisation the account acts for, checked here and
    /// not only at assignment: somebody assigned first who joins the card
    /// later (T-93a) would otherwise go on scoring their own organisation.
    /// </summary>
    public static Task<bool> MayEvaluateAsync(
        AppDbContext context, Guid userId, Guid applicationId, CancellationToken cancellationToken) =>
        Evaluable(context, userId).AnyAsync(a => a.ApplicationId == applicationId, cancellationToken);

    /// <summary>The assignments behind MayEvaluateAsync, for a list of them (the expert's own work).</summary>
    public static IQueryable<ApplicationAssignment> Evaluable(AppDbContext context, Guid userId)
    {
        var cards = ResourceOwnership.EntityIdsOf(context, userId);
        return context.ApplicationAssignments.Where(
            a => a.ReviewerId == userId
                && a.IsActive
                && !cards.Contains(a.Application.EntityId)
                && context.CompetitionExperts.Any(
                    e => e.CompetitionId == a.Application.CompetitionId && e.UserId == userId && e.IsActive)
                && context.ReviewerDeclarations.Any(
                    d => d.CompetitionId == a.Application.CompetitionId
                        && d.ReviewerId == userId
                        && d.Accepted
                        && d.IsActive));
    }

    /// <summary>
    /// Whether the account acts for the organisation behind the application
    /// (T-93a): such a person is never its expert (conflict of interest), and
    /// does see what its applicant sees.
    /// </summary>
    public static Task<bool> ActsForApplicantAsync(
        AppDbContext context, Guid userId, Guid applicationId, CancellationToken cancellationToken)
    {
        var cards = ResourceOwnership.EntityIdsOf(context, userId);
        return context.Applications.AnyAsync(x => x.Id == applicationId && cards.Contains(x.EntityId), cancellationToken);
    }

    /// <summary>Who may be appointed at all: an active applicant or expert account, never an operator.</summary>
    public static bool MayBeAppointed(User user) =>
        user.IsActive && user.Role is Role.Applicant or Role.Reviewer;
}
