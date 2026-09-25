using Ocwip.Api.Contracts;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

/// <summary>How a request about an evaluation ended, one value per status code.</summary>
internal enum EvaluationOutcome
{
    Succeeded,

    /// <summary>A new draft was started, as opposed to an existing one returned.</summary>
    Created,

    ApplicationNotFound,

    /// <summary>A draft application is not evaluated: nothing was handed in.</summary>
    NotSubmitted,

    /// <summary>The competition has not published the card for this stage.</summary>
    NoCard,

    NotFound,

    /// <summary>"Zapisz i zakończ etap" already happened; P4 on B-02 asks who may reopen.</summary>
    AlreadyFinished,

    /// <summary>The answers do not fit the card, with the keys of the fields.</summary>
    AnswersRejected,
}

internal sealed record EvaluationResult(
    EvaluationOutcome Outcome,
    EvaluationResponse? Evaluation = null,
    IDictionary<string, string[]>? Errors = null);

/// <summary>
/// Evaluations of applications (T-38): start a card, save it as a draft,
/// finish the stage, read it back. Who may do which is not decided here but
/// in the authorization layer (EvaluationAccessHandler and the route
/// policies); this service assumes the caller already passed.
/// </summary>
internal interface IEvaluationService
{
    /// <summary>
    /// The caller's own active card for this application and stage, started
    /// if there is none yet. A formal card is one per application whoever
    /// opens it, because the operator's staff fill it in as one team.
    /// </summary>
    Task<EvaluationResult> StartAsync(
        Guid applicationId,
        EvaluationStage stage,
        Guid callerId,
        CancellationToken cancellationToken);

    Task<EvaluationResult> SaveAsync(
        Guid evaluationId,
        SaveEvaluationRequest request,
        CancellationToken cancellationToken);

    Task<EvaluationResult> FinishAsync(Guid evaluationId, CancellationToken cancellationToken);

    Task<EvaluationResult> GetAsync(Guid evaluationId, CancellationToken cancellationToken);

    /// <summary>The row the authorization handler decides on, or null.</summary>
    Task<Evaluation?> FindForAuthorizationAsync(Guid evaluationId, CancellationToken cancellationToken);
}
