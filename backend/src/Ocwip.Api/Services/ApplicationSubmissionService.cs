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

        if (application.Status is not ApplicationStatus.Draft)
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

        var now = _time.GetUtcNow();
        var intake = CompetitionIntake.For(application.Competition, now);

        if (!intake.AcceptsApplications)
        {
            return new ApplicationSubmissionResult(
                ApplicationSubmissionOutcome.IntakeClosed,
                Message: CompetitionIntakeMessage.For(intake));
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

        var assignment = await _numbering.AssignAsync(
            application, user.Id, now, cancellationToken);

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
                assignment.IsActive
                    ? ApplicationSubmissionOutcome.AlreadySubmitted
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

        var facts = new ApplicationPdfFacts(
            number,
            application.Competition.Title,
            application.Entity.Name,
            application.Entity.Type,
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
            Data złożenia: {submittedAt.UtcDateTime:yyyy-MM-dd HH:mm} UTC

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
            application.Status == ApplicationStatus.Funded ? application.AwardedGrant : null);
}
