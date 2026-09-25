using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;

namespace Ocwip.Api.Services;

/// <inheritdoc cref="IApplicationListService"/>
internal sealed class ApplicationListService : IApplicationListService
{
    private readonly AppDbContext _context;

    public ApplicationListService(AppDbContext context) => _context = context;

    public async Task<ApplicationListResponse?> ListAsync(
        Guid competitionId, CancellationToken cancellationToken)
    {
        var competition = await _context.Competitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == competitionId, cancellationToken);

        if (competition is null)
        {
            return null;
        }

        var applications = await Submitted(competitionId)
            .Include(x => x.Entity)
            .Include(x => x.FormDefinition)
            // By length first: the number is zero padded to three digits
            // (ApplicationNumberAssigner), so "1000" would sort before "999".
            .OrderBy(x => x.Number!.Length)
            .ThenBy(x => x.Number)
            .ToListAsync(cancellationToken);

        // One parse per form version, not per application: a competition of
        // 120 offers usually has one or two versions between them.
        var documents = new Dictionary<Guid, FormDocument?>();

        var items = applications
            .Select(application =>
            {
                if (!documents.TryGetValue(application.FormDefinitionId, out var document))
                {
                    document = FormSchemaValidator
                        .Validate(application.FormDefinition.Definition).Document;
                    documents[application.FormDefinitionId] = document;
                }

                // A stored definition always passed the gate when it was
                // published; one that no longer does (the contract grew
                // stricter since) still has to list its offers, only without
                // the values the roles would have read.
                var values = document is null
                    ? new ApplicationRoleValues(null, null, null)
                    : ApplicationRoleValues.Read(document, application.Answers);

                return new ApplicationListItem(
                    application.Id,
                    application.Number!,
                    application.Entity.Name,
                    application.Entity.Type,
                    values.ProjectTitle,
                    values.TotalCost,
                    values.RequestedGrant,
                    application.Status,
                    application.SubmittedAt!.Value);
            })
            .ToList();

        var requested = items.Sum(item => item.RequestedGrant ?? 0m);

        return new ApplicationListResponse(
            competition.Id,
            competition.Number,
            competition.Title,
            competition.TotalPoolAmount,
            requested,
            competition.TotalPoolAmount - requested,
            items);
    }

    public async Task<SubmittedApplicationResponse?> GetSubmittedAsync(
        Guid competitionId, Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await Submitted(competitionId)
            .Include(x => x.Entity)
            .Include(x => x.FormDefinition)
            .Include(x => x.Competition)
            .FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken);

        if (application is null)
        {
            return null;
        }

        var attachments = await _context.Attachments
            .AsNoTracking()
            .Where(x => x.ApplicationId == applicationId && x.IsActive)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new AttachmentResponse(
                x.Id, x.ApplicationId, x.FileName, x.ContentType, x.SizeInBytes, x.CreatedAt))
            .ToListAsync(cancellationToken);

        return new SubmittedApplicationResponse(
            application.Id,
            application.CompetitionId,
            application.Competition.Title,
            application.Number!,
            application.Entity.Name,
            application.Entity.Type,
            application.Status,
            application.SubmittedAt!.Value,
            ApplicationChecksum.Compute(
                application.Id, application.UpdatedAt, application.Answers),
            application.FormDefinition.VersionNumber,
            application.FormDefinition.Definition,
            application.Answers,
            attachments);
    }

    /// <summary>
    /// Everything the operator has received in a competition. "Not a draft"
    /// rather than "Submitted", so the states the review module adds later
    /// (R-03, M5) stay on the list without this line being found and
    /// changed; a deactivated row is one its applicant withdrew.
    /// </summary>
    private IQueryable<Application> Submitted(Guid competitionId) =>
        _context.Applications
            .AsNoTracking()
            .Where(x => x.CompetitionId == competitionId
                && x.Status != ApplicationStatus.Draft
                && x.IsActive);
}
