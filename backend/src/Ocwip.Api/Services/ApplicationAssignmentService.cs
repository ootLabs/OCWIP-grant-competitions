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
    private readonly IEmailSender _email;
    private readonly IConfiguration _configuration;

    public ApplicationAssignmentService(AppDbContext context, TimeProvider time, IEmailSender email, IConfiguration configuration)
    {
        _context = context;
        _time = time;
        _email = email;
        _configuration = configuration;
    }

    public async Task<ApplicationAssignmentResult> AssignAsync(
        Guid applicationId, Guid reviewerId, CancellationToken cancellationToken)
    {
        // Submitted and active only: a draft is its applicant's work in
        // progress, and an assignment is the whole right to read it
        // (EntityScopedHandler), so an expert never gets one before it is
        // submitted.
        var applicationExists = await _context.Applications
            .AnyAsync(x => x.Id == applicationId && x.IsActive && x.Status != ApplicationStatus.Draft, cancellationToken);

        if (!applicationExists)
        {
            return new ApplicationAssignmentResult(
                ApplicationAssignmentOutcome.ApplicationNotFound);
        }

        if (await ResultsApprovedAsync(applicationId, cancellationToken))
        {
            return new ApplicationAssignmentResult(ApplicationAssignmentOutcome.ResultsApproved);
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

        var isNew = assignment is null || !assignment.IsActive;

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
        // above and is still reported as Succeeded. So is the same pair
        // assigned twice in the same moment: the unique index lets one in,
        // and that one sends the mail.
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            _context.ChangeTracker.Clear();
            isNew = false;
            assignment = await _context.ApplicationAssignments.AsNoTracking().SingleAsync(
                x => x.ApplicationId == applicationId && x.ReviewerId == reviewerId, cancellationToken);
        }

        // T-104: the expert hears about a new assignment, once; repeating it
        // sends nothing.
        if (isNew)
        {
            await NotifyAsync(applicationId, reviewerId, cancellationToken);
        }

        return new ApplicationAssignmentResult(
            ApplicationAssignmentOutcome.Succeeded, ToResponse(assignment));
    }

    private async Task NotifyAsync(Guid applicationId, Guid reviewerId, CancellationToken cancellationToken)
    {
        var to = await _context.Users.AsNoTracking()
            .Where(x => x.Id == reviewerId && x.EmailConfirmed)
            .Select(x => x.Email)
            .SingleOrDefaultAsync(cancellationToken);
        var application = await _context.Applications.AsNoTracking()
            .Where(x => x.Id == applicationId)
            .Select(x => new { x.Number, x.CompetitionId, Title = x.Competition.Title })
            .SingleAsync(cancellationToken);

        if (string.IsNullOrEmpty(to))
        {
            return;
        }

        var baseUrl = (_configuration["EmailVerification:FrontendBaseUrl"] is { Length: > 0 } configured
            ? configured
            : "http://localhost:3000").TrimEnd('/');

        // The number and the competition only: nothing of the application
        // itself travels in a mail.
        var body = $"""
            Przypisano Ci do oceny wniosek {application.Number} w konkursie "{application.Title}".

            Przed pierwszą oceną w tym konkursie potwierdź deklarację bezstronności. Wnioski do oceny: {baseUrl}/panel/reviewer
            """;

        await _email.SendAsync(new EmailMessage(to, $"Nowy wniosek do oceny: {application.Number}", body), cancellationToken);
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
        // second revoke asks for the state the row is already in, so the
        // announcement has nothing to refuse there; only a revoke that would
        // really take an expert off the application is closed (S-05).
        if (assignment.IsActive)
        {
            if (await ResultsApprovedAsync(applicationId, cancellationToken))
            {
                return new ApplicationAssignmentResult(ApplicationAssignmentOutcome.ResultsApproved);
            }

            assignment.IsActive = false;
            assignment.DeactivatedAt = _time.GetUtcNow();

            await _context.SaveChangesAsync(cancellationToken);
        }

        return new ApplicationAssignmentResult(
            ApplicationAssignmentOutcome.Succeeded, ToResponse(assignment));
    }

    /// <summary>
    /// Whether the competition of this application has announced its results
    /// (S-05). Who evaluates is an input of the ranking, which is counted on
    /// every read, so the set of experts is closed when the result is
    /// published, both ways.
    /// </summary>
    private Task<bool> ResultsApprovedAsync(Guid applicationId, CancellationToken cancellationToken) =>
        _context.Applications
            .Where(x => x.Id == applicationId)
            .AnyAsync(x => x.Competition.ResultsApprovedAt != null, cancellationToken);

    private static ApplicationAssignmentResponse ToResponse(
        ApplicationAssignment assignment) =>
        new(
            assignment.Id,
            assignment.ApplicationId,
            assignment.ReviewerId,
            assignment.IsActive);
}
