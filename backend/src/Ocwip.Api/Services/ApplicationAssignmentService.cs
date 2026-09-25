using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

/// <summary>
/// Assigning and unassigning reviewers (T-37). See
/// IApplicationAssignmentService for what this does and does not decide.
/// </summary>
internal sealed class ApplicationAssignmentService : IApplicationAssignmentService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _time;

    public ApplicationAssignmentService(AppDbContext context, TimeProvider time)
    {
        _context = context;
        _time = time;
    }

    public async Task<ApplicationAssignmentResult> AssignAsync(
        Guid applicationId, Guid reviewerId, CancellationToken cancellationToken)
    {
        var applicationExists = await _context.Applications
            .AnyAsync(x => x.Id == applicationId, cancellationToken);

        if (!applicationExists)
        {
            return new ApplicationAssignmentResult(
                ApplicationAssignmentOutcome.ApplicationNotFound);
        }

        // Only an active Reviewer account may occupy this column. Collapsing
        // "no such account", "not a reviewer" and "deactivated" into one
        // outcome is deliberate, see ApplicationAssignmentOutcome.ReviewerNotFound.
        var reviewerExists = await _context.Users.AnyAsync(
            x => x.Id == reviewerId && x.Role == Role.Reviewer && x.IsActive,
            cancellationToken);

        if (!reviewerExists)
        {
            return new ApplicationAssignmentResult(
                ApplicationAssignmentOutcome.ReviewerNotFound);
        }

        var assignment = await _context.ApplicationAssignments.SingleOrDefaultAsync(
            x => x.ApplicationId == applicationId && x.ReviewerId == reviewerId,
            cancellationToken);

        if (assignment is null)
        {
            assignment = new ApplicationAssignment
            {
                ApplicationId = applicationId,
                ReviewerId = reviewerId,
            };

            _context.ApplicationAssignments.Add(assignment);
        }
        else if (!assignment.IsActive)
        {
            // Reassigning after a revoke reactivates the one row for this
            // pair, see ApplicationAssignment.IsActive.
            assignment.IsActive = true;
            assignment.DeactivatedAt = null;
        }

        // Idempotent: an already active pair falls through both branches
        // above and is still reported as Succeeded.
        await _context.SaveChangesAsync(cancellationToken);

        return new ApplicationAssignmentResult(
            ApplicationAssignmentOutcome.Succeeded, ToResponse(assignment));
    }

    public async Task<ApplicationAssignmentResult> UnassignAsync(
        Guid applicationId, Guid reviewerId, CancellationToken cancellationToken)
    {
        var assignment = await _context.ApplicationAssignments.SingleOrDefaultAsync(
            x => x.ApplicationId == applicationId && x.ReviewerId == reviewerId,
            cancellationToken);

        if (assignment is null)
        {
            return new ApplicationAssignmentResult(
                ApplicationAssignmentOutcome.NotAssigned);
        }

        // Idempotent, same reasoning as ApplicationService.DeactivateAsync: a
        // second revoke asks for the state the row is already in.
        if (assignment.IsActive)
        {
            assignment.IsActive = false;
            assignment.DeactivatedAt = _time.GetUtcNow();

            await _context.SaveChangesAsync(cancellationToken);
        }

        return new ApplicationAssignmentResult(
            ApplicationAssignmentOutcome.Succeeded, ToResponse(assignment));
    }

    private static ApplicationAssignmentResponse ToResponse(
        ApplicationAssignment assignment) =>
        new(
            assignment.Id,
            assignment.ApplicationId,
            assignment.ReviewerId,
            assignment.IsActive);
}
