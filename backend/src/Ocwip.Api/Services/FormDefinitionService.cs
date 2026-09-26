using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;

namespace Ocwip.Api.Services;

/// <summary>
/// Versioning of the form definition (T-25).
///
/// One sentence holds the whole file together: publishing ADDS a row. Nothing
/// here updates a stored document, and that is not an omission. An application
/// points at a version, not at a competition, so overwriting the document
/// under it would silently rewrite what an applicant was shown, and with a
/// five year retention period there would be no way to reconstruct the form a
/// submitted application was filled against.
/// </summary>
internal sealed class FormDefinitionService : IFormDefinitionService
{
    /// <summary>
    /// The name EF gives the unique index from FormDefinitionConfiguration.
    /// Matched by name and not by message text, so a different unique
    /// violation is never reported as a version conflict.
    /// </summary>
    internal const string VersionIndex =
        "ix_form_definitions_competition_id_purpose_version_number";

    private readonly AppDbContext _context;

    public FormDefinitionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<FormDefinitionResult> PublishAsync(
        Guid competitionId,
        FormPurpose purpose,
        FormDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var competition = await _context.Competitions
            .FirstOrDefaultAsync(x => x.Id == competitionId, cancellationToken);

        if (competition is null)
        {
            return new FormDefinitionResult(
                FormDefinitionOutcome.CompetitionNotFound);
        }

        if (!competition.IsActive)
        {
            return new FormDefinitionResult(FormDefinitionOutcome.Inactive);
        }

        // The gate of T-24, on the only write path that reaches the column.
        // A missing body arrives here as ValueKind.Undefined and is refused by
        // the same rule as a malformed one, which is the point: saving it
        // instead would throw inside the Npgsql serializer with a message that
        // names no field at all (see FormDefinitionConfiguration).
        var validation = FormSchemaValidator.Validate(request.Definition, purpose);

        if (!validation.IsValid)
        {
            return new FormDefinitionResult(
                FormDefinitionOutcome.InvalidDefinition,
                Errors: validation.Errors);
        }

        var definition = new FormDefinition
        {
            // Generated here rather than by the column default, because the
            // competition has to be pointed at this row in the SAME save: the
            // foreign key is composite on (id, form_definition_id) and reading
            // a database generated key back would mean a second round trip
            // with a window in the middle where the version exists and nothing
            // is in force.
            Id = Guid.NewGuid(),
            CompetitionId = competitionId,
            Purpose = purpose,
            VersionNumber = await NextVersionNumberAsync(
                competitionId, purpose, cancellationToken),
            Definition = request.Definition.Clone(),
        };

        _context.FormDefinitions.Add(definition);

        // The new version becomes the one in force. Applications are not
        // touched, and that separation is the card: what a competition hands
        // to the NEXT applicant changes, what an applicant already has does
        // not.
        //
        // Which pointer moves depends on the purpose (T-38): publishing a
        // merit card leaves the application form in force where it was.
        switch (purpose)
        {
            case FormPurpose.FormalEvaluation:
                competition.FormalCardDefinitionId = definition.Id;
                break;
            case FormPurpose.MeritEvaluation:
                competition.MeritCardDefinitionId = definition.Id;
                break;
            case FormPurpose.Report:
                competition.ReportFormDefinitionId = definition.Id;
                break;
            default:
                competition.FormDefinitionId = definition.Id;
                break;
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsVersionTaken(exception))
        {
            // The max above is a SELECT and a SELECT loses the race against a
            // second operator publishing in the same moment. The unique index
            // answered instead, and the caller gets a 409 it can retry rather
            // than a 500.
            return new FormDefinitionResult(FormDefinitionOutcome.VersionTaken);
        }

        return new FormDefinitionResult(
            FormDefinitionOutcome.Succeeded,
            Response(definition, InForce(competition, purpose)));
    }

    public async Task<IReadOnlyList<FormDefinitionSummaryResponse>?> ListAsync(
        Guid competitionId,
        FormPurpose purpose,
        CancellationToken cancellationToken)
    {
        var competition = await _context.Competitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == competitionId, cancellationToken);

        if (competition is null)
        {
            return null;
        }

        // Ordered by version number rather than by the timestamp: the number
        // is what the operator reads, and two rows written in the same second
        // would otherwise come back in whatever order the plan produced.
        var current = InForce(competition, purpose);

        var versions = await _context.FormDefinitions
            .AsNoTracking()
            .Where(x => x.CompetitionId == competitionId && x.Purpose == purpose)
            .OrderBy(x => x.VersionNumber)
            // Without this projection the whole document of every version
            // travels to build a list that shows none of them.
            .Select(x => new FormDefinitionSummaryResponse(
                x.Id,
                x.CompetitionId,
                x.VersionNumber,
                current == x.Id,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return versions;
    }

    public async Task<FormDefinitionResult> GetAsync(
        Guid competitionId,
        FormPurpose purpose,
        int versionNumber,
        CancellationToken cancellationToken)
    {
        var competition = await _context.Competitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == competitionId, cancellationToken);

        if (competition is null)
        {
            return new FormDefinitionResult(
                FormDefinitionOutcome.CompetitionNotFound);
        }

        var definition = await _context.FormDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.CompetitionId == competitionId
                    && x.Purpose == purpose
                    && x.VersionNumber == versionNumber,
                cancellationToken);

        return definition is null
            ? new FormDefinitionResult(FormDefinitionOutcome.NotFound)
            : new FormDefinitionResult(
                FormDefinitionOutcome.Succeeded,
                Response(definition, InForce(competition, purpose)));
    }

    /// <summary>The version in force for this purpose.</summary>
    internal static Guid? InForce(Competition competition, FormPurpose purpose) =>
        purpose switch
        {
            FormPurpose.FormalEvaluation => competition.FormalCardDefinitionId,
            FormPurpose.MeritEvaluation => competition.MeritCardDefinitionId,
            FormPurpose.Report => competition.ReportFormDefinitionId,
            _ => competition.FormDefinitionId,
        };

    /// <summary>
    /// One past the highest number ever used in this competition, counting
    /// versions marked inactive. Numbers are never reused: a submitted
    /// application names the version it was filled against, and a second
    /// version 3 would make that name ambiguous for the whole retention
    /// period.
    /// </summary>
    private async Task<int> NextVersionNumberAsync(
        Guid competitionId,
        FormPurpose purpose,
        CancellationToken cancellationToken)
    {
        var highest = await _context.FormDefinitions
            .Where(x => x.CompetitionId == competitionId && x.Purpose == purpose)
            // Nullable on purpose: Max over no rows throws on int, and the
            // first version of a form is the normal case, not the edge one.
            .MaxAsync(x => (int?)x.VersionNumber, cancellationToken);

        return (highest ?? 0) + 1;
    }

    private static bool IsVersionTaken(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres
        && postgres.SqlState == PostgresErrorCodes.UniqueViolation
        && postgres.ConstraintName == VersionIndex;

    private static FormDefinitionResponse Response(
        FormDefinition definition,
        Guid? currentFormDefinitionId) =>
        new(
            definition.Id,
            definition.CompetitionId,
            definition.VersionNumber,
            definition.Definition,
            currentFormDefinitionId == definition.Id,
            definition.CreatedAt);
}
