using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;

namespace Ocwip.Api.Services;

/// <summary>
/// Evaluations of applications (T-38). The card is a form definition with an
/// evaluation purpose, so the answers are checked by the same AnswerValidator
/// as an application, on the same two levels: a draft at every save, the
/// whole card when the stage is finished. Scores are never stored; every
/// response reads them from the answers (EvaluationScores).
/// </summary>
internal sealed class EvaluationService : IEvaluationService
{
    private static readonly string[] OneActiveIndexes =
    [
        "ux_evaluations_one_active_formal",
        "ux_evaluations_one_active_merit_per_author",
    ];

    private readonly AppDbContext _context;
    private readonly TimeProvider _time;

    public EvaluationService(AppDbContext context, TimeProvider time)
    {
        _context = context;
        _time = time;
    }

    public async Task<EvaluationResult> StartAsync(
        Guid applicationId,
        EvaluationStage stage,
        Guid callerId,
        CancellationToken cancellationToken)
    {
        // A withdrawn application is not evaluated, and reads as missing
        // rather than as a separate state nobody can act on.
        var application = await _context.Applications
            .Include(x => x.Competition)
            .FirstOrDefaultAsync(x => x.Id == applicationId && x.IsActive, cancellationToken);

        if (application is null)
        {
            return new EvaluationResult(EvaluationOutcome.ApplicationNotFound);
        }

        if (application.Status is ApplicationStatus.Draft)
        {
            return new EvaluationResult(EvaluationOutcome.NotSubmitted);
        }

        if (await ExistingAsync(applicationId, stage, callerId, cancellationToken) is { } existing)
        {
            return new EvaluationResult(EvaluationOutcome.Succeeded, await ResponseAsync(existing, cancellationToken));
        }

        var cardId = stage == EvaluationStage.Formal
            ? application.Competition.FormalCardDefinitionId
            : application.Competition.MeritCardDefinitionId;

        if (cardId is null)
        {
            return new EvaluationResult(EvaluationOutcome.NoCard);
        }

        var evaluation = new Evaluation
        {
            Id = Guid.NewGuid(),
            CompetitionId = application.CompetitionId,
            ApplicationId = applicationId,
            // The version in force NOW, and it stays: a card published while
            // this one is being filled in does not reinterpret its answers.
            FormDefinitionId = cardId.Value,
            Stage = stage,
            AuthorUserId = callerId,
            EnteredByUserId = callerId,
            Answers = JsonDocument.Parse("{}").RootElement.Clone(),
            Status = EvaluationStatus.Draft,
        };

        _context.Evaluations.Add(evaluation);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsSecondActiveCard(exception))
        {
            // Two tabs opened the card in the same moment. The partial unique
            // index let one of them in; the other gets that one back instead
            // of a 500, which is what it asked for in the first place.
            _context.ChangeTracker.Clear();

            var raced = await ExistingAsync(applicationId, stage, callerId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Unique index refused an evaluation of {applicationId} that cannot be found.");

            return new EvaluationResult(EvaluationOutcome.Succeeded, await ResponseAsync(raced, cancellationToken));
        }

        return new EvaluationResult(EvaluationOutcome.Created, await ResponseAsync(evaluation, cancellationToken));
    }

    public async Task<EvaluationResult> SaveAsync(
        Guid evaluationId,
        SaveEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        var evaluation = await ActiveAsync(evaluationId, cancellationToken);

        if (evaluation is null)
        {
            return new EvaluationResult(EvaluationOutcome.NotFound);
        }

        if (evaluation.Status is EvaluationStatus.Finished)
        {
            return new EvaluationResult(EvaluationOutcome.AlreadyFinished);
        }

        if (request.Answers.ValueKind != JsonValueKind.Object)
        {
            return new EvaluationResult(
                EvaluationOutcome.AnswersRejected,
                Errors: new Dictionary<string, string[]>
                {
                    ["answers"] = ["Odpowiedzi muszą być obiektem."],
                });
        }

        var subject = await SubjectAsync(evaluation, cancellationToken);
        var check = AnswerValidator.Validate(
            subject.Card, request.Answers, subject.Bases, AnswerStrictness.Draft, subject.Applicant);

        if (!check.IsValid)
        {
            return new EvaluationResult(EvaluationOutcome.AnswersRejected, Errors: check.ToProblemErrors());
        }

        evaluation.Answers = request.Answers.Clone();
        await _context.SaveChangesAsync(cancellationToken);

        return new EvaluationResult(EvaluationOutcome.Succeeded, Response(evaluation, subject));
    }

    public async Task<EvaluationResult> FinishAsync(Guid evaluationId, CancellationToken cancellationToken)
    {
        var evaluation = await ActiveAsync(evaluationId, cancellationToken);

        if (evaluation is null)
        {
            return new EvaluationResult(EvaluationOutcome.NotFound);
        }

        if (evaluation.Status is EvaluationStatus.Finished)
        {
            return new EvaluationResult(EvaluationOutcome.AlreadyFinished);
        }

        // The whole card, against what is stored: finishing is "zapisz i
        // zakończ etap" after the last save, not a second way to send answers.
        var subject = await SubjectAsync(evaluation, cancellationToken);
        var check = AnswerValidator.Validate(
            subject.Card, evaluation.Answers, subject.Bases, AnswerStrictness.Submission, subject.Applicant);

        if (!check.IsValid)
        {
            return new EvaluationResult(EvaluationOutcome.AnswersRejected, Errors: check.ToProblemErrors());
        }

        evaluation.Status = EvaluationStatus.Finished;
        evaluation.FinishedAt = _time.GetUtcNow();
        await _context.SaveChangesAsync(cancellationToken);

        return new EvaluationResult(EvaluationOutcome.Succeeded, Response(evaluation, subject));
    }

    public async Task<EvaluationResult> GetAsync(Guid evaluationId, CancellationToken cancellationToken)
    {
        var evaluation = await ActiveAsync(evaluationId, cancellationToken);

        return evaluation is null
            ? new EvaluationResult(EvaluationOutcome.NotFound)
            : new EvaluationResult(EvaluationOutcome.Succeeded, await ResponseAsync(evaluation, cancellationToken));
    }

    public Task<Evaluation?> FindForAuthorizationAsync(Guid evaluationId, CancellationToken cancellationToken) =>
        _context.Evaluations
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == evaluationId && x.IsActive, cancellationToken);

    private Task<Evaluation?> ActiveAsync(Guid evaluationId, CancellationToken cancellationToken) =>
        _context.Evaluations.SingleOrDefaultAsync(x => x.Id == evaluationId && x.IsActive, cancellationToken);

    /// <summary>
    /// The formal card is one per application whoever opens it; a merit card
    /// is one per application AND expert. Mirrors the two partial unique
    /// indexes in EvaluationConfiguration.
    /// </summary>
    private Task<Evaluation?> ExistingAsync(
        Guid applicationId,
        EvaluationStage stage,
        Guid callerId,
        CancellationToken cancellationToken) =>
        _context.Evaluations.FirstOrDefaultAsync(
            x => x.ApplicationId == applicationId
                && x.Stage == stage
                && x.IsActive
                && (stage == EvaluationStage.Formal || x.AuthorUserId == callerId),
            cancellationToken);

    /// <summary>What checking and scoring one evaluation needs besides its answers.</summary>
    private sealed record Subject(
        FormDefinition CardRow,
        FormDocument Card,
        EntityType Applicant,
        IReadOnlyDictionary<string, decimal?> Bases);

    private async Task<Subject> SubjectAsync(Evaluation evaluation, CancellationToken cancellationToken)
    {
        var card = await _context.FormDefinitions
            .AsNoTracking()
            .SingleAsync(x => x.Id == evaluation.FormDefinitionId, cancellationToken);

        var application = await _context.Applications
            .AsNoTracking()
            .Include(x => x.Entity)
            .Include(x => x.Competition)
            .SingleAsync(x => x.Id == evaluation.ApplicationId, cancellationToken);

        // Every stored card passed the contract gate for its purpose on the
        // way in (T-25, T-38), so a refusal here is a row changed behind the
        // service's back: an error, not answers checked against nothing.
        var document = FormSchemaValidator.Validate(card.Definition, card.Purpose).Document
            ?? throw new InvalidOperationException(
                $"Stored evaluation card {card.Id} does not pass the form contract.");

        return new Subject(card, document, application.Entity.Type, AnswerLimits.BasesFor(application.Competition));
    }

    private async Task<EvaluationResponse> ResponseAsync(Evaluation evaluation, CancellationToken cancellationToken) =>
        Response(evaluation, await SubjectAsync(evaluation, cancellationToken));

    private static EvaluationResponse Response(Evaluation evaluation, Subject subject)
    {
        var scores = EvaluationScores.Read(subject.Card, evaluation.Answers, subject.Applicant);

        return new EvaluationResponse(
            evaluation.Id,
            evaluation.ApplicationId,
            evaluation.CompetitionId,
            evaluation.Stage,
            subject.CardRow.Id,
            subject.CardRow.VersionNumber,
            subject.CardRow.Definition,
            evaluation.AuthorUserId,
            evaluation.AuthorName,
            evaluation.EnteredByUserId,
            evaluation.Answers,
            evaluation.Status,
            evaluation.FinishedAt,
            evaluation.UpdatedAt,
            scores.FormalPassed,
            scores.MeritScore,
            scores.StrategicScore,
            scores.RecommendedGrant);
    }

    private static bool IsSecondActiveCard(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres
        && postgres.SqlState == PostgresErrorCodes.UniqueViolation
        && OneActiveIndexes.Contains(postgres.ConstraintName);
}
