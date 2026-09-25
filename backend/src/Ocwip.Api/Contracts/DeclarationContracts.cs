using System.Text.Json.Serialization;

namespace Ocwip.Api.Contracts;

/// <summary>Where an expert's impartiality declaration for one competition stands (T-40a).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DeclarationStatus>))]
public enum DeclarationStatus
{
    NotDecided,
    Accepted,
    Refused,
}

/// <param name="Text">What the expert is asked to declare, or what they declared when they decided.</param>
public sealed record DeclarationResponse(
    Guid CompetitionId,
    DeclarationStatus Status,
    string Text,
    string? RefusalReason,
    DateTimeOffset? DecidedAt);

/// <param name="RefusalReason">Required when Accept is false (report: "odmowa wymaga powodu").</param>
public sealed record DeclarationDecisionRequest(bool Accept, string? RefusalReason);

/// <summary>One expert's declaration as the operator sees it (report, T-41: "stan oświadczenia").</summary>
public sealed record DeclarationRow(
    Guid ReviewerId,
    string ReviewerName,
    string Email,
    DeclarationStatus Status,
    string? RefusalReason,
    DateTimeOffset? DecidedAt);
