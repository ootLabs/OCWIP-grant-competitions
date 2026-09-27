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
    DateTimeOffset UpdatedAt,
    ReportSettlementResponse? Settlement);

/// <summary>
/// The settlement of the grant (T-50b), counted on every read from the
/// report's budget and the operator's review, never stored: the applicant
/// may still change the budget until the report is submitted. Null when the
/// report form marks no budget ("reportBudget").
/// </summary>
/// <param name="BudgetKey">The key of the budget table in the answers.</param>
/// <param name="AwardedGrant">The grant awarded when the results were approved (T-42).</param>
/// <param name="GrantSpent">Sum of the "grantSpent" column.</param>
/// <param name="Refused">Sum of the costs the operator did not accept.</param>
/// <param name="Accepted">GrantSpent minus Refused.</param>
/// <param name="Refund">What the applicant pays back: the grant minus the accepted spending, never below zero.</param>
public sealed record ReportSettlementResponse(
    string BudgetKey,
    decimal? AwardedGrant,
    decimal GrantSpent,
    decimal Refused,
    decimal Accepted,
    decimal? Refund,
    IReadOnlyList<ReportCostRow> Rows);

/// <summary>One row of the budget: its grant spending and, when the operator refused some of it, how much and why.</summary>
public sealed record ReportCostRow(int Row, decimal Spent, decimal Refused, string? Reason);

/// <summary>One refused cost: the row of the budget (from 0), the amount and the reason.</summary>
public sealed record CostReviewItem(int Row, decimal Refused, string? Reason);

/// <summary>The operator's whole review at once; a row left out is accepted in full.</summary>
public sealed record ReviewCostsRequest(IReadOnlyList<CostReviewItem>? Items);

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
