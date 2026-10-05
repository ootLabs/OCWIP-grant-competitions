using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;

namespace Ocwip.Api.Services.Reports;

/// <summary>
/// A report as the API hands it out (T-50a), with the settlement counted
/// from its budget on every read (T-50b).
/// </summary>
internal static class ReportReader
{
    public static async Task<ReportResponse?> ResponseAsync(AppDbContext context, Guid reportId, CancellationToken cancellationToken)
    {
        var row = await context.Reports.AsNoTracking()
            .Where(x => x.Id == reportId && x.IsActive)
            .Select(x => new
            {
                Response = new ReportResponse(
                    x.Id,
                    x.ApplicationId,
                    x.CompetitionId,
                    x.Application.Number,
                    EntityName: string.Empty,
                    x.Application.ApplicantType ?? x.Application.Entity.Type,
                    x.FormDefinition.VersionNumber,
                    x.FormDefinition.Definition,
                    x.Answers,
                    x.Status,
                    x.SubmittedAt,
                    x.ReturnReason,
                    x.AcceptedAt,
                    x.UpdatedAt,
                    null),
                x.FormDefinition,
                x.Application.AwardedGrant,
                x.CostReview,
                // The frozen name (S-06), put together below: the live card
                // has no stage gate, so the screen the operator settles a
                // grant from must not follow it.
                x.Application.EntitySnapshot,
                LiveName = x.Application.Entity.Name,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        var settlement = ReportSettlement.Compute(
            Document(row.FormDefinition),
            row.Response.Answers,
            row.Response.ApplicantType,
            row.AwardedGrant,
            ReportSettlement.Read(row.CostReview));

        return row.Response with
        {
            EntityName = EntityCards.EntitySnapshots.NameOf(row.EntitySnapshot, row.LiveName),
            Settlement = settlement,
        };
    }

    /// <summary>Every stored form passed the contract gate for its purpose on the way in (T-25).</summary>
    public static FormDocument Document(FormDefinition definition) =>
        FormSchemaValidator.Validate(definition.Definition, definition.Purpose).Document
        ?? throw new InvalidOperationException($"Stored form {definition.Id} does not pass the form contract.");
}
