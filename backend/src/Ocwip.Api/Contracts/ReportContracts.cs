using System.Text.Json;
using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// One report with its form (T-50a). The form travels with the answers, as
/// with an evaluation card: the applicant may not read the operator's form
/// routes, and a form without its document cannot be drawn.
/// </summary>
public sealed record ReportResponse(
    Guid Id,
    Guid ApplicationId,
    Guid CompetitionId,
    string? ApplicationNumber,
    string EntityName,
    EntityType ApplicantType,
    int FormVersion,
    JsonElement FormDefinition,
    JsonElement Answers,
    ReportStatus Status,
    DateTimeOffset? SubmittedAt,
    string? ReturnReason,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset UpdatedAt);

/// <summary>One report on the operator's list of a competition.</summary>
public sealed record ReportListItem(
    Guid Id,
    Guid ApplicationId,
    string? ApplicationNumber,
    string EntityName,
    ReportStatus Status,
    DateTimeOffset? SubmittedAt);

/// <summary>Autosave of a report: the whole set of answers at once.</summary>
public sealed record SaveReportRequest(JsonElement Answers);

/// <summary>Sending a report back: the reason is required, the applicant reads it.</summary>
public sealed record ReturnReportRequest(string? Reason);
