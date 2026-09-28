using System.Text.Json;
using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// The body of POST /applications/{id}/return (T-103): which sections the
/// applicant may change, whether attachments too, what to correct and by when.
/// </summary>
/// <param name="Sections">Keys of sections of the application's form, at least one.</param>
/// <param name="Deadline">A whole minute in the future; the correction closes at it.</param>
public sealed record ApplicationReturnRequest(
    IReadOnlyList<string>? Sections,
    bool UnlocksAttachments,
    string? Message,
    DateTimeOffset? Deadline);

/// <summary>One return, open or resolved.</summary>
public sealed record ApplicationReturnResponse(
    Guid Id,
    IReadOnlyList<string> Sections,
    bool UnlocksAttachments,
    string Message,
    DateTimeOffset Deadline,
    DateTimeOffset ReturnedAt,
    DateTimeOffset? ResolvedAt);

/// <summary>An earlier submitted version, without its answers.</summary>
public sealed record ApplicationVersionSummary(
    int VersionNumber,
    DateTimeOffset SubmittedAt,
    string Checksum,
    DateTimeOffset SupersededAt);

/// <summary>An earlier submitted version with its answers and card, as it was submitted.</summary>
public sealed record ApplicationVersionResponse(
    Guid ApplicationId,
    int VersionNumber,
    Guid FormDefinitionId,
    JsonElement Answers,
    EntityCardData? EntitySnapshot,
    string Checksum,
    DateTimeOffset SubmittedAt,
    DateTimeOffset SupersededAt);

/// <summary>One change of status, as the history table holds it.</summary>
public sealed record ApplicationHistoryEntry(
    ApplicationStatus FromStatus,
    ApplicationStatus ToStatus,
    DateTimeOffset ChangedAt);

/// <summary>
/// GET /applications/{id}/corrections: the returns of an application, its
/// earlier versions and its status history. The open return, if any, is the
/// one without ResolvedAt, and it is what the applicant's screen unlocks.
/// </summary>
public sealed record ApplicationCorrectionsResponse(
    Guid ApplicationId,
    ApplicationStatus Status,
    IReadOnlyList<ApplicationReturnResponse> Returns,
    IReadOnlyList<ApplicationVersionSummary> Versions,
    IReadOnlyList<ApplicationHistoryEntry> History);
