using System.Text.Json;
using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// One application, draft or submitted, as its owner or an operator sees it
/// (T-29).
/// </summary>
/// <param name="Checksum">
/// D15: three groups of four lowercase hex characters, for example
/// "0a55-22c2-b414". Recomputed on every read from the id, the last saved
/// instant and the answers themselves (see Models/ApplicationChecksum.cs), so
/// it always matches what is actually stored instead of a value written once
/// and left to go stale.
/// </param>
/// <param name="LastSavedAt">
/// When this version of the answers was written, so the front can show
/// "zapisano o 14:32" without keeping a second clock of its own.
/// </param>
public sealed record ApplicationResponse(
    Guid Id,
    Guid CompetitionId,
    Guid FormDefinitionId,
    ApplicationStatus Status,
    JsonElement Answers,
    string? Number,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset LastSavedAt,
    string Checksum,
    bool IsActive,
    decimal? AwardedGrant = null);
