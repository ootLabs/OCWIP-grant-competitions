namespace Ocwip.Api.Contracts;

/// <summary>
/// POST /competitions/{id}/copy (T-98): what the copy does not take from the
/// source, because it belongs to the new edition. Everything else comes from
/// the source competition.
/// </summary>
/// <param name="Number">The new competition's number, unique like any other.</param>
/// <param name="Title">The new title; the source's when left empty.</param>
public sealed record CompetitionCopyRequest(
    string? Number,
    string? Title,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    bool IsContinuousIntake = false);
