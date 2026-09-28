using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

internal enum EditWindowState
{
    /// <summary>A draft in an open intake, or a returned application before its deadline.</summary>
    Open,

    /// <summary>Submitted, or anything past it: nothing to edit.</summary>
    NotEditable,

    /// <summary>The intake, or the correction's deadline, is over.</summary>
    Closed,
}

/// <summary>
/// Whether an application may be changed now, and what of it (T-103): the one
/// answer autosave, attachment upload and submission share, so a returned
/// application cannot be open in one of them and shut in another.
///
/// A draft follows the intake (CompetitionIntake.For). A returned application
/// follows its return's deadline (CompetitionIntake.ForCorrection) and only the
/// sections the operator unlocked, plus the attachments when the return says
/// so. Everything else is not editable.
/// </summary>
internal sealed record ApplicationEditWindow(
    EditWindowState State,
    string? Message,
    ApplicationReturn? Return)
{
    public bool IsOpen => State is EditWindowState.Open;

    public bool IsCorrection => Return is not null;

    /// <summary>A draft may change anything; a correction only its unlocked sections.</summary>
    public bool Unlocks(string sectionKey) => Return is null || Return.Sections.Contains(sectionKey);

    public bool UnlocksAttachments => Return is null || Return.UnlocksAttachments;

    public static async Task<ApplicationEditWindow> ForAsync(
        AppDbContext context, Application application, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (application.Status is ApplicationStatus.Draft)
        {
            var intake = CompetitionIntake.For(application.Competition, now);
            return intake.AcceptsApplications
                ? new ApplicationEditWindow(EditWindowState.Open, null, null)
                : new ApplicationEditWindow(EditWindowState.Closed, CompetitionIntakeMessage.For(intake), null);
        }

        if (application.Status is not ApplicationStatus.Returned)
        {
            return new ApplicationEditWindow(EditWindowState.NotEditable, null, null);
        }

        var open = await context.ApplicationReturns
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.ApplicationId == application.Id && x.ResolvedAt == null, cancellationToken);

        // Returned without an open return cannot be stored by the product's
        // own paths; if it is, the row is refused, not trusted.
        if (open is null)
        {
            return new ApplicationEditWindow(EditWindowState.NotEditable, null, null);
        }

        var correction = CompetitionIntake.ForCorrection(application.Competition, open.Deadline, now);
        return correction.AcceptsApplications
            ? new ApplicationEditWindow(EditWindowState.Open, null, open)
            : new ApplicationEditWindow(EditWindowState.Closed, CompetitionIntakeMessage.ForCorrection(correction), open);
    }
}
