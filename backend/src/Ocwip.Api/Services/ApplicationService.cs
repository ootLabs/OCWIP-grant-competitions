using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;

namespace Ocwip.Api.Services;

/// <summary>
/// Draft applications (T-29): starting one, autosaving it, reading it and
/// marking it inactive.
///
/// Every write here goes through T-21's rule before touching the row: whether
/// a draft may be created or saved is CompetitionIntake's question to answer,
/// never a comparison of dates written again in this file.
/// </summary>
internal sealed class ApplicationService : IApplicationService
{
    /// <summary>An answer set nobody has touched yet. Object, not array: the
    /// check constraint accepts either, but the form contract's own root is an
    /// object (docs/kontrakt-formularza.md), and starting from the other shape
    /// would be a lie about what is coming.</summary>
    private static readonly JsonElement EmptyAnswers =
        JsonDocument.Parse("{}").RootElement.Clone();

    private readonly AppDbContext _context;
    private readonly TimeProvider _time;
    private readonly UserManager<User> _userManager;

    public ApplicationService(
        AppDbContext context,
        TimeProvider time,
        UserManager<User> userManager)
    {
        _context = context;
        _time = time;
        _userManager = userManager;
    }

    public async Task<ApplicationResult> CreateDraftAsync(
        Guid competitionId,
        ClaimsPrincipal caller,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(caller);

        if (user?.EntityId is not { } entityId)
        {
            return new ApplicationResult(ApplicationOutcome.NoEntity);
        }

        var competition = await _context.Competitions
            .FirstOrDefaultAsync(x => x.Id == competitionId, cancellationToken);

        if (competition is null)
        {
            return new ApplicationResult(ApplicationOutcome.CompetitionNotFound);
        }

        var intake = CompetitionIntake.For(competition, _time.GetUtcNow());

        // Covers an inactive competition and a draft competition too, not
        // only a closed one: CompetitionIntake already treats both as
        // Unavailable, and repeating that comparison here would be a second
        // rule pretending to be T-21's.
        if (!intake.AcceptsApplications)
        {
            return new ApplicationResult(
                ApplicationOutcome.IntakeClosed,
                Message: CompetitionIntakeMessage.For(intake));
        }

        if (competition.FormDefinitionId is not { } formDefinitionId)
        {
            return new ApplicationResult(ApplicationOutcome.NoFormDefinition);
        }

        var application = new Application
        {
            CompetitionId = competitionId,
            EntityId = entityId,
            FormDefinitionId = formDefinitionId,
            Status = ApplicationStatus.Draft,
            Answers = EmptyAnswers,
        };

        _context.Applications.Add(application);
        await SaveAndReloadAsync(application, cancellationToken);

        return Success(application);
    }

    public async Task<ApplicationResult> SaveDraftAsync(
        Guid id,
        JsonElement answers,
        CancellationToken cancellationToken)
    {
        // Narrower than the check constraint (ApplicationConfiguration),
        // which also takes an array: the answers are keyed by field, and
        // T-30 checks every key. Checked first so a missing or malformed
        // body answers 400 naming the problem instead of failing inside the
        // Npgsql serializer with a message that names no field at all.
        if (answers.ValueKind is not JsonValueKind.Object)
        {
            return new ApplicationResult(ApplicationOutcome.InvalidAnswers);
        }

        var application = await _context.Applications
            .Include(x => x.Competition)
            .Include(x => x.FormDefinition)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (application is null)
        {
            return new ApplicationResult(ApplicationOutcome.NotFound);
        }

        if (application.Status is not ApplicationStatus.Draft)
        {
            return new ApplicationResult(ApplicationOutcome.AlreadySubmitted);
        }

        // The applicant asked for this row to disappear from their list
        // (DeactivateAsync). Reading it back stays fine, the card is explicit
        // that a deactivated draft "zostaje widoczna", but a save reaching it
        // afterwards, a queued autosave from a tab left open past the delete,
        // would otherwise resurrect content its owner just asked to hide.
        if (!application.IsActive)
        {
            return new ApplicationResult(ApplicationOutcome.Inactive);
        }

        var intake = CompetitionIntake.For(application.Competition, _time.GetUtcNow());

        if (!intake.AcceptsApplications)
        {
            return new ApplicationResult(
                ApplicationOutcome.IntakeClosed,
                Message: CompetitionIntakeMessage.For(intake));
        }

        // Against the version this application was started on, never the
        // competition's newest one: a draft filled in against version 3 must
        // not start failing because the operator published version 4.
        var check = AnswerValidator.Validate(
            FormDocumentFor(application.FormDefinition),
            answers,
            AnswerLimits.BasesFor(application.Competition),
            AnswerStrictness.Draft);

        if (!check.IsValid)
        {
            return new ApplicationResult(
                ApplicationOutcome.AnswersRejected,
                Errors: check.ToProblemErrors());
        }

        // Last write wins, by design: the card asks for a save after every
        // filled field, not a merge of two browser tabs editing the same
        // draft at once. Reconciling concurrent edits is a real feature, not
        // a gap in this one, and belongs to whichever card first needs it.
        application.Answers = answers.Clone();
        await SaveAndReloadAsync(application, cancellationToken);

        return Success(application);
    }

    public async Task<ApplicationResult> GetAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var application = await _context.Applications
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return application is null
            ? new ApplicationResult(ApplicationOutcome.NotFound)
            : Success(application);
    }

    public async Task<ApplicationFormResult> GetFormDefinitionAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var application = await _context.Applications
            .AsNoTracking()
            .Include(x => x.FormDefinition)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return application is null
            ? new ApplicationFormResult(ApplicationOutcome.NotFound)
            : new ApplicationFormResult(
                ApplicationOutcome.Succeeded,
                new ApplicationFormResponse(
                    application.FormDefinition.VersionNumber,
                    application.FormDefinition.Definition));
    }

    public async Task<ApplicationResult> DeactivateAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var application = await _context.Applications
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (application is null)
        {
            return new ApplicationResult(ApplicationOutcome.NotFound);
        }

        if (application.Status is not ApplicationStatus.Draft)
        {
            return new ApplicationResult(ApplicationOutcome.AlreadySubmitted);
        }

        // Idempotent, same reasoning as CompetitionService.DeactivateAsync: a
        // second call asks for the state the row is already in, and answering
        // with a failure there only teaches the caller to treat a request
        // that already succeeded once as broken.
        if (application.IsActive)
        {
            application.IsActive = false;
            application.DeactivatedAt = _time.GetUtcNow();

            await SaveAndReloadAsync(application, cancellationToken);
        }

        return Success(application);
    }

    /// <summary>
    /// The parsed form a stored definition stands for. Every stored
    /// definition passed the contract gate on the way in (T-24), so a
    /// refusal here is a row changed behind the application's back, and it
    /// surfaces as an error rather than as answers checked against nothing.
    /// </summary>
    private static FormDocument FormDocumentFor(FormDefinition definition) =>
        FormSchemaValidator.Validate(definition.Definition).Document
        ?? throw new InvalidOperationException(
            $"Stored form definition {definition.Id} does not pass the form contract.");

    public Task<Application?> FindForAuthorizationAsync(
        Guid id, CancellationToken cancellationToken) =>
        _context.Applications
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    /// <summary>
    /// Saves, then reloads the row from the database before anything reads it
    /// back. Without the reload, the checksum this call's own response
    /// carries would disagree with the one a later GET recomputes:
    /// `timestamptz` round-trips through PostgreSQL at microsecond precision,
    /// one digit short of a .NET tick, so the in-memory value just written and
    /// the value a fresh read returns can differ in their last digit. See
    /// ApplicationChecksumTests for the jsonb half of the same problem.
    /// </summary>
    private async Task SaveAndReloadAsync(
        Application application, CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
        await _context.Entry(application).ReloadAsync(cancellationToken);
    }

    private ApplicationResult Success(Application application) =>
        new(ApplicationOutcome.Succeeded, ToResponse(application));

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
            application.IsActive);
}
