using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Data.Configurations;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;

namespace Ocwip.Api.Services;

internal enum ApplicationReturnOutcome
{
    Succeeded,
    Created,
    NotFound,
    Invalid,

    /// <summary>Not a submitted application now: a draft, already returned, or past its result.</summary>
    NotReturnable,

    /// <summary>The competition is not taking applications or under evaluation, or its results are approved.</summary>
    WrongStage,
}

internal sealed record ApplicationReturnResult(
    ApplicationReturnOutcome Outcome,
    ApplicationReturnResponse? Return = null,
    ApplicationCorrectionsResponse? Corrections = null,
    ApplicationVersionResponse? Version = null,
    IDictionary<string, string[]>? Errors = null);

internal interface IApplicationReturnService
{
    Task<ApplicationReturnResult> ReturnAsync(Guid applicationId, Guid operatorId, ApplicationReturnRequest request, CancellationToken cancellationToken);

    Task<ApplicationReturnResult> CorrectionsAsync(Guid applicationId, CancellationToken cancellationToken);

    Task<ApplicationReturnResult> VersionAsync(Guid applicationId, int versionNumber, CancellationToken cancellationToken);
}

/// <summary>
/// "Zwrot do poprawy" (T-103, R-03, RD10). The operator sends a submitted
/// application back with the sections to correct, a note and a deadline; the
/// applicant corrects those sections (ApplicationEditWindow, LockedSections)
/// and submits again (ApplicationNumberAssigner.ResubmitAsync).
///
/// Returning, in one transaction:
/// - the version being sent back is copied into application_versions with the
///   checksum its confirmation carries, so it stays readable after the
///   answers change;
/// - the status goes Submitted to Returned by a conditional UPDATE, so of two
///   operators returning at once only one does;
/// - the evaluations so far are deactivated, not removed: the corrected
///   application is evaluated again, and the old cards stay on record;
/// - the return and the history row are written.
/// The mail to the applicant goes after the commit.
///
/// Allowed while the intake is open and during the evaluation, the default of
/// PK-H ("na obu"), and never after the results are approved.
/// </summary>
internal sealed class ApplicationReturnService(AppDbContext context, TimeProvider time, IEmailSender email) : IApplicationReturnService
{
    private static readonly CompetitionStatus[] ReturnableStages =
        [CompetitionStatus.OpenForApplications, CompetitionStatus.Closed, CompetitionStatus.UnderReview];

    public async Task<ApplicationReturnResult> ReturnAsync(
        Guid applicationId, Guid operatorId, ApplicationReturnRequest request, CancellationToken cancellationToken)
    {
        var application = await context.Applications
            .Include(x => x.Competition)
            .Include(x => x.FormDefinition)
            .SingleOrDefaultAsync(x => x.Id == applicationId && x.IsActive, cancellationToken);

        if (application is null)
        {
            return new ApplicationReturnResult(ApplicationReturnOutcome.NotFound);
        }

        if (application.Status is not ApplicationStatus.Submitted)
        {
            return new ApplicationReturnResult(ApplicationReturnOutcome.NotReturnable);
        }

        var now = time.GetUtcNow();
        if (application.Competition.ResultsApprovedAt is not null
            || !ReturnableStages.Contains(CompetitionLifecycle.Effective(application.Competition, now)))
        {
            return new ApplicationReturnResult(ApplicationReturnOutcome.WrongStage);
        }

        var form = FormSchemaValidator.Validate(application.FormDefinition.Definition).Document
            ?? throw new InvalidOperationException($"Stored form definition {application.FormDefinitionId} does not pass the form contract.");

        var (sections, message, deadline, errors) = Check(form, request, now);
        if (errors.Count > 0)
        {
            return new ApplicationReturnResult(ApplicationReturnOutcome.Invalid, Errors: errors);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var claimed = await context.Applications
            .Where(x => x.Id == applicationId && x.IsActive && x.Status == ApplicationStatus.Submitted)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ApplicationStatus.Returned), cancellationToken);

        if (claimed != 1)
        {
            return new ApplicationReturnResult(ApplicationReturnOutcome.NotReturnable);
        }

        var versionNumber = await context.ApplicationVersions.CountAsync(x => x.ApplicationId == applicationId, cancellationToken) + 1;
        context.ApplicationVersions.Add(new ApplicationVersion
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            VersionNumber = versionNumber,
            FormDefinitionId = application.FormDefinitionId,
            Answers = SensitiveAnswers.Protect(
                application.Answers, SensitiveAnswers.Keys(form), ApplicationVersionConfiguration.AnswersPurpose),
            EntitySnapshot = application.EntitySnapshot,
            // What the applicant's confirmation of this version says (D15).
            Checksum = ApplicationChecksum.Compute(application.Id, application.UpdatedAt, application.Answers),
            SubmittedAt = application.SubmittedAt!.Value,
            SupersededAt = now,
        });

        var returned = new ApplicationReturn
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            Sections = sections,
            UnlocksAttachments = request.UnlocksAttachments,
            Message = message,
            Deadline = deadline,
            ReturnedByUserId = operatorId,
            ReturnedAt = now,
        };
        context.ApplicationReturns.Add(returned);

        context.ApplicationStatusHistory.Add(new ApplicationStatusHistory
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            FromStatus = ApplicationStatus.Submitted,
            ToStatus = ApplicationStatus.Returned,
            ChangedAt = now,
            ChangedByUserId = operatorId,
        });

        await context.Evaluations
            .Where(x => x.ApplicationId == applicationId && x.IsActive)
            .ExecuteUpdateAsync(
                s => s.SetProperty(x => x.IsActive, false).SetProperty(x => x.DeactivatedAt, now),
                cancellationToken);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await NotifyAsync(application, form, returned, cancellationToken);

        return new ApplicationReturnResult(ApplicationReturnOutcome.Created, ToResponse(returned));
    }

    public async Task<ApplicationReturnResult> CorrectionsAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var status = await context.Applications.AsNoTracking()
            .Where(x => x.Id == applicationId)
            .Select(x => (ApplicationStatus?)x.Status)
            .SingleOrDefaultAsync(cancellationToken);

        if (status is null)
        {
            return new ApplicationReturnResult(ApplicationReturnOutcome.NotFound);
        }

        var returns = await context.ApplicationReturns.AsNoTracking()
            .Where(x => x.ApplicationId == applicationId)
            .OrderBy(x => x.ReturnedAt)
            .ToListAsync(cancellationToken);

        var versions = await context.ApplicationVersions.AsNoTracking()
            .Where(x => x.ApplicationId == applicationId)
            .OrderBy(x => x.VersionNumber)
            .Select(x => new ApplicationVersionSummary(x.VersionNumber, x.SubmittedAt, x.Checksum, x.SupersededAt))
            .ToListAsync(cancellationToken);

        var history = await context.ApplicationStatusHistory.AsNoTracking()
            .Where(x => x.ApplicationId == applicationId)
            .OrderBy(x => x.ChangedAt)
            .Select(x => new ApplicationHistoryEntry(x.FromStatus, x.ToStatus, x.ChangedAt))
            .ToListAsync(cancellationToken);

        return new ApplicationReturnResult(
            ApplicationReturnOutcome.Succeeded,
            Corrections: new ApplicationCorrectionsResponse(
                applicationId, status.Value, [.. returns.Select(ToResponse)], versions, history));
    }

    public async Task<ApplicationReturnResult> VersionAsync(Guid applicationId, int versionNumber, CancellationToken cancellationToken)
    {
        var version = await context.ApplicationVersions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ApplicationId == applicationId && x.VersionNumber == versionNumber, cancellationToken);

        return version is null
            ? new ApplicationReturnResult(ApplicationReturnOutcome.NotFound)
            : new ApplicationReturnResult(
                ApplicationReturnOutcome.Succeeded,
                Version: new ApplicationVersionResponse(
                    version.ApplicationId,
                    version.VersionNumber,
                    version.FormDefinitionId,
                    version.Answers,
                    EntityCards.EntitySnapshots.Read(version.EntitySnapshot),
                    version.Checksum,
                    version.SubmittedAt,
                    version.SupersededAt));
    }

    private static (List<string> Sections, string Message, DateTimeOffset Deadline, Dictionary<string, string[]> Errors) Check(
        FormDocument form, ApplicationReturnRequest request, DateTimeOffset now)
    {
        var errors = new Dictionary<string, string[]>();
        var known = form.Sections.Select(x => x.Key).ToList();
        var asked = (request.Sections ?? []).Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);

        if (asked.Count == 0)
        {
            errors["sections"] = ["Wskaż co najmniej jedną sekcję do poprawy."];
        }
        else if (asked.FirstOrDefault(x => !known.Contains(x)) is { } unknown)
        {
            errors["sections"] = [$"Formularz tego wniosku nie ma sekcji \"{unknown}\"."];
        }

        var message = request.Message?.Trim() ?? string.Empty;
        if (message.Length == 0)
        {
            errors["message"] = ["Opisz, co trzeba poprawić."];
        }
        else if (message.Length > ApplicationReturnConfiguration.MessageMaxLength)
        {
            errors["message"] = [$"Opis może mieć najwyżej {ApplicationReturnConfiguration.MessageMaxLength} znaków."];
        }

        var deadline = request.Deadline ?? default;
        if (request.Deadline is null)
        {
            errors["deadline"] = ["Podaj termin poprawy."];
        }
        else if (deadline.UtcTicks % TimeSpan.TicksPerMinute != 0)
        {
            errors["deadline"] = ["Termin poprawy podaje się z dokładnością do minuty."];
        }
        else if (deadline <= now)
        {
            errors["deadline"] = ["Termin poprawy musi być w przyszłości."];
        }

        // In form order, whatever order the screen sent them in.
        return ([.. known.Where(asked.Contains)], message, deadline.ToUniversalTime(), errors);
    }

    private async Task NotifyAsync(
        Application application, FormDocument form, ApplicationReturn returned, CancellationToken cancellationToken)
    {
        // The account that submitted it, the one the confirmation went to.
        var to = await context.ApplicationStatusHistory.AsNoTracking()
            .Where(x => x.ApplicationId == application.Id && x.ToStatus == ApplicationStatus.Submitted)
            .OrderByDescending(x => x.ChangedAt)
            .Select(x => x.ChangedByUser.Email)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrEmpty(to))
        {
            return;
        }

        var titles = form.Sections.Where(x => returned.Sections.Contains(x.Key)).Select(x => $"- {x.Title}");
        var attachments = returned.UnlocksAttachments ? "\nMożesz też dodać albo podmienić załączniki.\n" : string.Empty;

        var body = $"""
            Wniosek {application.Number} w konkursie "{application.Competition.Title}" został zwrócony do poprawy.

            Co poprawić:
            {returned.Message}

            Sekcje, które możesz zmienić:
            {string.Join("\n", titles)}
            {attachments}
            Poprawiony wniosek złóż ponownie do {CompetitionIntakeMessage.Moment(returned.Deadline)}. Po tym terminie poprawki nie można już złożyć.
            """;

        await email.SendAsync(new EmailMessage(to, $"Wniosek {application.Number} zwrócony do poprawy", body), cancellationToken);
    }

    private static ApplicationReturnResponse ToResponse(ApplicationReturn x) =>
        new(x.Id, x.Sections, x.UnlocksAttachments, x.Message, x.Deadline, x.ReturnedAt, x.ResolvedAt);
}
