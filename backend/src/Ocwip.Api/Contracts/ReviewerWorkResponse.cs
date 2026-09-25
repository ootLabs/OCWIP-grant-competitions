using System.Text.Json.Serialization;
using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>Where the caller's own merit card for one application stands.</summary>
// Text on the wire, like every enum of this API: an ordinal would
// reinterpret itself the day a member is inserted.
[JsonConverter(typeof(JsonStringEnumConverter<OwnCardStanding>))]
public enum OwnCardStanding
{
    NotStarted,
    Draft,
    Finished,
}

/// <summary>
/// What an expert has to evaluate (T-40): the applications assigned to them,
/// grouped by competition, with the three sums the report puts above the list
/// (requested, recommended by this expert, the competition's pool), so an
/// expert sees at once that they have recommended 140 000 against a pool of
/// 100 000.
/// </summary>
public sealed record ReviewerWorkResponse(IReadOnlyList<ReviewerCompetition> Competitions);

public sealed record ReviewerCompetition(
    Guid CompetitionId,
    string Number,
    string Title,
    decimal? TotalPoolAmount,
    decimal RequestedTotal,
    decimal RecommendedTotal,
    IReadOnlyList<ReviewerApplication> Applications);

/// <param name="RecommendedGrant">What this expert recommends, null until they say.</param>
public sealed record ReviewerApplication(
    Guid ApplicationId,
    string? Number,
    EntityType EntityType,
    string? ProjectTitle,
    decimal? RequestedGrant,
    OwnCardStanding Card,
    Guid? EvaluationId,
    decimal? RecommendedGrant);
