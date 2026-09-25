using System.Text.Json;
using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// One evaluation card as filled in (T-38), with the card it was filled in on
/// and the result read from its answers. The card travels with the answers
/// because the expert who fills it in may not read the operator's card
/// routes, and a card without its document cannot be drawn.
/// </summary>
/// <param name="CardDefinition">The document of the card version, never a newer one.</param>
/// <param name="FormalPassed">Null until every criterion asked is answered, and on a merit card.</param>
public sealed record EvaluationResponse(
    Guid Id,
    Guid ApplicationId,
    Guid CompetitionId,
    EvaluationStage Stage,
    Guid CardDefinitionId,
    int CardVersionNumber,
    JsonElement CardDefinition,
    Guid? AuthorUserId,
    string? AuthorName,
    Guid EnteredByUserId,
    JsonElement Answers,
    EvaluationStatus Status,
    DateTimeOffset? FinishedAt,
    DateTimeOffset UpdatedAt,
    bool? FormalPassed,
    decimal? MeritScore,
    decimal? StrategicScore,
    decimal? RecommendedGrant);

/// <summary>Autosave of an evaluation: the whole set of answers at once, like SaveApplicationDraftRequest.</summary>
public sealed record SaveEvaluationRequest(JsonElement Answers);
