using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;
using Ocwip.Api.Services.Ranking;

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
            // By length first: the number is zero padded to three digits
            // (ApplicationNumberAssigner), so "1000" would sort before "999".
            .OrderBy(x => x.Number!.Length)
            .ThenBy(x => x.Number)
            .ToListAsync(cancellationToken);

        // One read and one parse per form version, not per application: a
        // competition of 120 offers usually has one or two versions between
        // them, and an Include would carry the whole definition on every row.
        var versionIds = applications.Select(x => x.FormDefinitionId).Distinct().ToList();

        var documents = await _context.FormDefinitions
            .AsNoTracking()
            .Where(x => versionIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => FormSchemaValidator.Validate(x.Definition).Document,
                cancellationToken);

        // The formal evaluations of the whole competition in one read, and
        // their card versions in one more: the result belongs on this list
        // (T-38), and a query per row would be 120 of them.
        var formal = await _context.Evaluations.AsNoTracking()
            .Where(x => x.CompetitionId == competitionId
                && x.IsActive
                && x.Stage == EvaluationStage.Formal)
            .ToListAsync(cancellationToken);

        var cardIds = formal.Select(x => x.FormDefinitionId).Distinct().ToList();

        var cards = await _context.FormDefinitions
            .AsNoTracking()
            .Where(x => cardIds.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id,
                x => FormSchemaValidator.Validate(x.Definition, x.Purpose).Document,
                cancellationToken);

        var formalByApplication = formal.ToDictionary(x => x.ApplicationId);

        var items = applications
            .Select(application =>
            {
                documents.TryGetValue(application.FormDefinitionId, out var document);

                // A stored definition always passed the gate when it was
                // published; one that no longer does (the contract grew
                // stricter since) still has to list its offers, only without
                // the values the roles would have read. A version missing
                // from the read above takes the same path rather than
                // throwing: the foreign key rules it out, and a list of 120
                // offers is not worth losing whole to one unreadable row.
                var values = document is null
                    ? new ApplicationRoleValues(null, null, null)
                    : ApplicationRoleValues.Read(document, application.Answers);

                return new ApplicationListItem(
                    application.Id,
                    application.Number!,
                    EntityCards.EntitySnapshots.NameOf(application.EntitySnapshot, application.Entity.Name),
                    application.ApplicantType ?? application.Entity.Type,
                    values.ProjectTitle,
                    values.TotalCost,
                    values.RequestedGrant,
                    application.Status,
                    application.SubmittedAt!.Value,
                    FormalStandingReader.Of(
                        cards,
                        formalByApplication.GetValueOrDefault(application.Id),
                        application.KindOfApplicant));
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

        // The copy from the moment of submission (T-93), where there is one.
        var card = EntityCards.EntitySnapshots.Read(application.EntitySnapshot);

        return new SubmittedApplicationResponse(
            application.Id,
            application.CompetitionId,
            application.Competition.Title,
            application.Number!,
            EntityCards.EntitySnapshots.NameOf(card, application.Entity.Name),
            application.KindOfApplicant,
            application.Status,
            application.SubmittedAt!.Value,
            ApplicationChecksum.Compute(
                application.Id, application.UpdatedAt, application.Answers),
            application.FormDefinition.VersionNumber,
            application.FormDefinition.Definition,
            application.Answers,
            attachments,
            card);
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
