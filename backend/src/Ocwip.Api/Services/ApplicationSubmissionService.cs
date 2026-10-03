using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;
using Ocwip.Api.Services.Pdf;

namespace Ocwip.Api.Services;

/// <summary>
/// Submitting a draft application and its confirmation PDF (T-33).
///
/// Every write here goes through T-21's rule before touching the row, the
/// same discipline ApplicationService and AttachmentService already follow
/// (R-29): whether a submission may happen is CompetitionIntake's question,
/// never a comparison of dates written again in this file.
///
/// Assigning the number and flipping the status is delegated to
/// ApplicationNumberAssigner rather than done here: that is the one piece of
/// this card with real concurrency discipline to get right (an advisory
/// lock plus a fresh re-read of the row inside it), and it deserves to be
/// readable on its own rather than folded into this orchestration.
/// </summary>
internal sealed class ApplicationSubmissionService : IApplicationSubmissionService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _time;
    private readonly UserManager<User> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly ApplicationNumberAssigner _numbering;

    public ApplicationSubmissionService(
        AppDbContext context,
        TimeProvider time,
        UserManager<User> userManager,
        IEmailSender emailSender)
    {
        _context = context;
        _time = time;
        _userManager = userManager;
        _emailSender = emailSender;
        _numbering = new ApplicationNumberAssigner(context);
    }

    public async Task<ApplicationSubmissionResult> SubmitAsync(
        Guid id,
        ClaimsPrincipal caller,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(caller);

        if (user is null)
        {
            return new ApplicationSubmissionResult(
                ApplicationSubmissionOutcome.AccountNotFound);
        }

        var application = await _context.Applications
            .Include(x => x.Competition)
            .Include(x => x.FormDefinition)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (application is null)
        {
            return new ApplicationSubmissionResult(
                ApplicationSubmissionOutcome.NotFound);
        }

        // A draft in its intake, or a returned application in its correction
        // window (T-103), decided where autosave and attachments decide it.
        var now = _time.GetUtcNow();
        var window = await ApplicationEditWindow.ForAsync(_context, application, now, cancellationToken);

        if (window.State is EditWindowState.NotEditable)
        {
            return new ApplicationSubmissionResult(
                ApplicationSubmissionOutcome.AlreadySubmitted);
        }

        if (!application.IsActive)
        {
            return new ApplicationSubmissionResult(
                ApplicationSubmissionOutcome.Inactive);
        }

        // Defensive, not a case the caller can provoke today. The composite
        // foreign key (ApplicationConfiguration) makes this pair impossible
        // to store, but that guarantee is the database's alone, not EF's:
        // setting the Competition and FormDefinition navigations to two
        // competitions at once does not throw, it quietly realigns
        // CompetitionId (docs/runbook/M4-wnioski.md, "drugi otwarty punkt").
        // Nothing in today's write paths can actually reach this, because
        // CompetitionId and FormDefinitionId are set once at creation
        // (ApplicationService.CreateDraftAsync) and no endpoint touches
        // either afterwards, so a mismatch here means the row was changed
        // from outside the product's own write paths, and it is refused
        // loudly rather than trusted.
        if (application.FormDefinition.CompetitionId != application.CompetitionId)
        {
            throw new InvalidOperationException(
                $"Application {application.Id} points at a form definition " +
                "that belongs to a different competition than the " +
                "application itself.");
        }

        if (window.State is EditWindowState.Closed)
        {
            return new ApplicationSubmissionResult(
                ApplicationSubmissionOutcome.IntakeClosed,
                Message: window.Message);
        }

        // Submission level, not draft level (T-30): every visible required
        // field, every range, every limit, not only the shapes a draft
        // already had to obey.
        var check = AnswerValidator.Validate(
            FormDocumentFor(application.FormDefinition),
            application.Answers,
            AnswerLimits.BasesFor(application.Competition),
            AnswerStrictness.Submission);

        if (!check.IsValid)
        {
            return new ApplicationSubmissionResult(
                ApplicationSubmissionOutcome.AnswersRejected,
                Errors: check.ToProblemErrors());
        }

        // The card becomes part of what is submitted (T-93, pola.md "Kopia
        // danych w złożonym wniosku"), so it has to pass the same rules as
        // every write of the card. Taken here and saved by the same
        // SaveChanges that numbers the application: a submission without
        // its copy, or a copy without a submission, cannot be stored.
        var entity = await _context.Entities
            .AsNoTracking()
            .SingleAsync(x => x.Id == application.EntityId, cancellationToken);

        // The copy is the checked card, not the row: the row can still hold
        // what the rules drop, such as the old contact_information of an
        // informal group (a natural person's) renamed into email by T-93.
        //
        // A correction (T-103) keeps the copy taken at the first submission:
        // a return unlocks sections of the form, never the card, so what the
        // operator did not send back does not change with the new version.
        var card = window.Return is null
            ? EntityCards.EntityCardValidator.Validate(EntityCards.EntitySnapshots.ToData(entity))
            : null;
        if (card is { IsValid: false })
        {
            return new ApplicationSubmissionResult(ApplicationSubmissionOutcome.EntityIncomplete);
        }

        var inKrs = entity.Register is EntityRegister.Krs;

        // Every required attachment of the competition answered by at least
        // one active file (T-101, R-33); a register extract "required outside
        // KRS" only when the card names another register or none. Named one
        // by one like any other
        // refusal of the submission (D12): the applicant sees what is missing,
        // not that something is.
        var missing = await _context.CompetitionAttachments
            .AsNoTracking()
            .Where(requirement => requirement.CompetitionId == application.CompetitionId
                && requirement.IsActive
                && (requirement.Requirement == AttachmentRequirement.Required
                    || (requirement.Requirement == AttachmentRequirement.RequiredOutsideKrs && !inKrs))
                && !_context.Attachments.Any(file => file.ApplicationId == application.Id
                    && file.IsActive
                    && file.CompetitionAttachmentId == requirement.Id))
            .OrderBy(requirement => requirement.Position)
            .Select(requirement => requirement.Title)
            .ToListAsync(cancellationToken);

        if (missing.Count > 0)
        {
            return new ApplicationSubmissionResult(
                ApplicationSubmissionOutcome.AnswersRejected,
                Errors: new Dictionary<string, string[]>
                {
                    ["attachments"] = [.. missing.Select(title => $"Brakuje wymaganego załącznika: {title}.")],
                });
        }

        if (card is not null)
        {
            application.EntitySnapshot = EntityCards.EntitySnapshots.Capture(card.Card!);
        }

        var kind = ApplicantKinds.Resolve(
            FormDocumentFor(application.FormDefinition), application.Answers, entity.Type);
        if (kind.Kind is not { } applicantType)
        {
            return new ApplicationSubmissionResult(
                ApplicationSubmissionOutcome.AnswersRejected,
                Errors: new Dictionary<string, string[]> { [kind.FieldKey!] = [kind.Problem!] });
        }

        application.ApplicantType = applicantType;

        // A correction keeps its number: the same application, a new version.
        var assignment = window.Return is { } open
            ? await _numbering.ResubmitAsync(application, open.Id, user.Id, now, cancellationToken)
            : await _numbering.AssignAsync(application, user.Id, now, cancellationToken);

        if (!assignment.Assigned)
        {
            // The row moved out from under this request while it was
            // waiting for the competition's advisory lock: a second submit
            // of the very same application (a double click, a retried
            // request) or a deactivation racing it. Every check above ran
            // against a copy of the row that is now stale, so this reports
            // exactly what ApplicationNumberAssigner found FRESH, inside the
            // lock, rather than repeat the now outdated checks above.
            return new ApplicationSubmissionResult(
                assignment.ChangedMeanwhile ? ApplicationSubmissionOutcome.ChangedMeanwhile
                : assignment.IsActive ? ApplicationSubmissionOutcome.AlreadySubmitted
                : ApplicationSubmissionOutcome.Inactive);
        }

        await SendConfirmationEmailAsync(application, user, cancellationToken);

        return new ApplicationSubmissionResult(
            ApplicationSubmissionOutcome.Succeeded, ToResponse(application));
    }

    public async Task<ApplicationConfirmationPdfResult> GetConfirmationPdfAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var application = await _context.Applications
            .Include(x => x.Competition)
            .Include(x => x.FormDefinition)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (application is null)
        {
            return new ApplicationConfirmationPdfResult(
                ApplicationSubmissionOutcome.NotFound);
        }

        // Any status past the draft: a funded or rejected application was
        // submitted all the same, and its confirmation does not expire.
        if (application.Status is ApplicationStatus.Draft
            || application.SubmittedAt is not { } submittedAt
            || application.Number is not { } number)
        {
            return new ApplicationConfirmationPdfResult(
                ApplicationSubmissionOutcome.NotSubmitted);
        }

        var checksum = ApplicationChecksum.Compute(
            application.Id, application.UpdatedAt, application.Answers);

        var content = ApplicationConfirmationPdfBuilder.Build(
            number,
            application.Competition.Title,
            application.FormDefinition.VersionNumber,
            submittedAt,
            checksum);

        return new ApplicationConfirmationPdfResult(
            ApplicationSubmissionOutcome.Succeeded,
            content,
            $"potwierdzenie-{number}.pdf");
    }

    /// <summary>
    /// The whole submitted application as a PDF (T-44), drawn from the form
    /// version it was filled in on. Same states as the confirmation: a draft
    /// has nothing to print yet.
    /// </summary>
    public async Task<ApplicationConfirmationPdfResult> GetApplicationPdfAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var application = await _context.Applications
            .Include(x => x.Competition)
            .Include(x => x.FormDefinition)
            .Include(x => x.Entity)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (application is null)
        {
            return new ApplicationConfirmationPdfResult(ApplicationSubmissionOutcome.NotFound);
        }

        if (application.Status is ApplicationStatus.Draft
            || application.SubmittedAt is not { } submittedAt
            || application.Number is not { } number)
        {
            return new ApplicationConfirmationPdfResult(ApplicationSubmissionOutcome.NotSubmitted);
        }

        // The copy from the moment of submission (T-93), not the live card:
        // a Podmiot renamed since then still printed its old name on the
        // offer the organiser holds. A row submitted before T-93 has none.
        var card = EntityCards.EntitySnapshots.Read(application.EntitySnapshot);

        var facts = new ApplicationPdfFacts(
            number,
            application.Competition.Title,
            card?.Name ?? application.Entity.Name,
            application.KindOfApplicant,
            application.FormDefinition.VersionNumber,
            submittedAt,
            ApplicationChecksum.Compute(application.Id, application.UpdatedAt, application.Answers));

        return new ApplicationConfirmationPdfResult(
            ApplicationSubmissionOutcome.Succeeded,
            ApplicationPdfBuilder.Build(facts, FormDocumentFor(application.FormDefinition), application.Answers),
            $"wniosek-{number.Replace('/', '-')}.pdf");
    }

    /// <summary>
    /// Sent only from here, never from ApplicationService's autosave path:
    /// the card is explicit that a draft save must never trigger this mail.
    /// Uses the operator authored text from step 1.6 of the wizard
    /// (Competition.SubmissionEmailBody), which nothing has sent until this
    /// card, plus a system generated footer naming the number, the
    /// competition and the instant, so the mail stands on its own even when
    /// the operator left the field empty.
    /// </summary>
    private async Task SendConfirmationEmailAsync(
        Application application, User user, CancellationToken cancellationToken)
    {
        // Defensive: registration requires an address (T-12.1), so a
        // submitting account without one would mean the account was created
        // outside that path.
        if (string.IsNullOrEmpty(user.Email))
        {
            return;
        }

        var operatorText = string.IsNullOrWhiteSpace(
            application.Competition.SubmissionEmailBody)
            ? "Dziękujemy za złożenie oferty."
            : application.Competition.SubmissionEmailBody!.Trim();

        var submittedAt = application.SubmittedAt!.Value;

        var body = $"""
            {operatorText}

            Numer wniosku: {application.Number}
            Konkurs: {application.Competition.Title}
            Data złożenia: {ReaderTime.Moment(submittedAt)} {ReaderTime.Label}

            Potwierdzenie w formacie PDF możesz pobrać z systemu.
            """;

        await _emailSender.SendAsync(
            new EmailMessage(user.Email, "Potwierdzenie złożenia oferty", body),
            cancellationToken);
    }

    /// <summary>
    /// The parsed form a stored definition stands for, same helper as
    /// ApplicationService.FormDocumentFor and for the same reason: every
    /// stored definition passed the contract gate on the way in (T-24), so a
    /// refusal here means the row changed behind the application's back.
    /// </summary>
    private static FormDocument FormDocumentFor(FormDefinition definition) =>
        FormSchemaValidator.Validate(definition.Definition).Document
        ?? throw new InvalidOperationException(
            $"Stored form definition {definition.Id} does not pass the form contract.");

    private static ApplicationResponse ToResponse(Application application) =>
        new(
            application.Id,
            application.CompetitionId,
            application.FormDefinitionId,
            application.Status,
            application.Answers,
            application.Number,
            application.SubmittedAt,
            application.UpdatedAt,
            ApplicationChecksum.Compute(
                application.Id, application.UpdatedAt, application.Answers),
            application.IsActive,
            // Only once funded, which is only after approval (T-42): a draft
            // decision never reaches the applicant.
            ApplicationStatuses.IsGranted(application.Status) ? application.AwardedGrant : null,
            EntityCards.EntitySnapshots.Read(application.EntitySnapshot));
}
