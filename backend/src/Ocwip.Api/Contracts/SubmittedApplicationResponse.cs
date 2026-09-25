using System.Text.Json;
using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// One submitted offer as the operator opens it from the list (T-35):
/// everything needed to show it without a second round trip, including the
/// form version it was filled in on, because a later version of the form
/// may have moved or removed the very fields this offer answered.
/// </summary>
/// <param name="Definition">The form document of <paramref name="FormVersion"/>.</param>
/// <param name="Attachments">The active files only: a replaced file stays
/// in the database (AGENTS.md rule 5) but is no longer part of the offer.</param>
public sealed record SubmittedApplicationResponse(
    Guid Id,
    Guid CompetitionId,
    string CompetitionTitle,
    string Number,
    string EntityName,
    EntityType EntityType,
    ApplicationStatus Status,
    DateTimeOffset SubmittedAt,
    string Checksum,
    int FormVersion,
    JsonElement Definition,
    JsonElement Answers,
    IReadOnlyList<AttachmentResponse> Attachments);
