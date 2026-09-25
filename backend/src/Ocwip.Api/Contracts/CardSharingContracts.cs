using System.Text.Json;
using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>Whether the cards of a competition are shared with the applicants (T-41b).</summary>
public sealed record CardSharingResponse(DateTimeOffset? SharedAt);

/// <summary>
/// The cards of an applicant's own application once the operator shared them
/// (T-41b). Shared is false and the list empty before that.
/// </summary>
public sealed record ApplicantEvaluationCards(bool Shared, IReadOnlyList<ApplicantEvaluationCard> Cards);

/// <summary>
/// One finished card as the applicant reads it: the card, the answers and the
/// result, and nothing about who evaluated. No evaluation id, no author, no
/// account that entered it; the order of the cards is the order they were
/// finished in, which says nothing about who wrote which.
/// </summary>
public sealed record ApplicantEvaluationCard(
    EvaluationStage Stage,
    EntityType ApplicantType,
    JsonElement CardDefinition,
    JsonElement Answers,
    bool? FormalPassed,
    decimal? MeritScore,
    decimal? StrategicScore,
    decimal? RecommendedGrant);
