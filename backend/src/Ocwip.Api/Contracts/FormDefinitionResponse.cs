using System.Text.Json;

namespace Ocwip.Api.Contracts;

/// <summary>
/// One published version of the form, with the document in it (T-25). This is
/// what the renderer of T-28 and the preview of T-27 read.
/// </summary>
/// <param name="IsCurrent">
/// Whether this is the version the competition currently hands to a new
/// application. Said out loud rather than left to be worked out by comparing
/// identifiers, because "which one is in force" is the question the publishing
/// screen asks, and a screen computing it from two responses computes it
/// differently than the next screen does.
/// </param>
public sealed record FormDefinitionResponse(
    Guid Id,
    Guid CompetitionId,
    int VersionNumber,
    JsonElement Definition,
    bool IsCurrent,
    DateTimeOffset CreatedAt);

/// <summary>
/// A version as it appears in the list of versions of a competition: the same
/// row without the document.
///
/// Separate type rather than a nullable field on the one above, because the
/// document is the big part: a competition edited through a season carries a
/// handful of versions of several hundred kilobytes each, and the list screen
/// needs none of it.
/// </summary>
public sealed record FormDefinitionSummaryResponse(
    Guid Id,
    Guid CompetitionId,
    int VersionNumber,
    bool IsCurrent,
    DateTimeOffset CreatedAt);
