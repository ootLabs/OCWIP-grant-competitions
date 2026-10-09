using Ocwip.Api.Contracts;

namespace Ocwip.Api.Services;

/// <summary>
/// How a request to assign or unassign a reviewer ended. One value per thing
/// the caller can do about it, which is one status code per value at the
/// endpoint, the same shape as ApplicationOutcome.
/// </summary>
internal enum ApplicationAssignmentOutcome
{
    Succeeded,

    /// <summary>
    /// The results are approved (S-05). An expert added now would open a card
    /// whose points change the published ranking; one taken away now leaves
    /// their finished card counting anyway (S-29 b), so neither is a thing to
    /// do after the announcement.
    /// </summary>
    ResultsApproved,

    /// <summary>No application with this id.</summary>
    ApplicationNotFound,

    /// <summary>
    /// No account with this id carrying <see cref="Models.Role.Reviewer"/>
    /// that is still active. Covers three different reasons a caller cannot
    /// tell apart from outside: the id does not exist, the account exists
    /// under a different role, or the reviewer's account was deactivated.
    /// Collapsing them is the same choice AccountService makes for a
    /// duplicate email: the difference is nobody else's business.
    /// </summary>
    ReviewerNotFound,

    /// <summary>
    /// Unassigning a pair that was never assigned in the first place, so
    /// there is no row to revoke. Distinct from revoking an already revoked
    /// row, which is idempotent and answers Succeeded, the same way
    /// ApplicationService.DeactivateAsync treats a second delete.
    /// </summary>
    NotAssigned,

    /// <summary>
    /// The expert acts for the organisation that filed the application
    /// (T-93a, R-44): they may not evaluate it.
    /// </summary>
    ConflictOfInterest,
}

internal sealed record ApplicationAssignmentResult(
    ApplicationAssignmentOutcome Outcome,
    ApplicationAssignmentResponse? Assignment = null);

/// <summary>
/// Assigning a reviewer to an application and revoking that assignment
/// (T-37). The rule that actually restricts what a reviewer can SEE lives in
/// Authorization/EntityScopedHandler.cs, which reads the same table this
/// service writes; this interface only ever changes who is assigned, it
/// never itself decides who may look at an application.
/// </summary>
internal interface IApplicationAssignmentService
{
    /// <summary>
    /// Assigns a reviewer to an application. Idempotent: assigning a pair
    /// that is already active changes nothing and still answers Succeeded,
    /// and assigning a pair that was previously revoked reactivates the same
    /// row rather than inserting a second one for it.
    /// </summary>
    Task<ApplicationAssignmentResult> AssignAsync(
        Guid applicationId, Guid reviewerId, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes a reviewer's assignment to an application. Never a hard
    /// delete (AGENTS.md rule 5): the row stays, marked inactive. Idempotent
    /// for an already revoked pair, refused with NotAssigned for a pair that
    /// was never assigned.
    /// </summary>
    Task<ApplicationAssignmentResult> UnassignAsync(
        Guid applicationId, Guid reviewerId, CancellationToken cancellationToken);
}
