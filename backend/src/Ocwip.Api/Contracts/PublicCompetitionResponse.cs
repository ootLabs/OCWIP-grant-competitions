using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// A competition as a guest sees it, with no account (T-20, feeding T-23).
///
/// A separate record from CompetitionResponse rather than the same one with
/// fields left empty. Reusing it would mean the public endpoint answers with a
/// type that HAS an audit trail and a list of operator moves, and keeping
/// those out would then be the job of whoever writes the next mapping. A type
/// that cannot carry them cannot leak them.
/// </summary>
public sealed record PublicCompetitionResponse(
    Guid Id,
    string Number,
    string Title,
    string? Description,
    CompetitionStatus Status,
    DateTimeOffset StartDate,
    DateTimeOffset? EndDate,
    bool IsContinuousIntake,
    decimal MaxGrantAmount,
    // What a guest needs in order to decide whether to apply and what to
    // prepare (T-23 draws the public page from exactly this). The two message
    // bodies shown after a submission are NOT here: they are addressed to
    // somebody who has already applied, and an announcement page is not the
    // place to publish the wording of an email.
    string? ExpectedResults,
    string? RulesUrl,
    bool RequiresPaperSubmission,
    DateTimeOffset? PaperSubmissionDeadline,
    string? PaperSubmissionAddress,
    DateOnly? ProjectStartDate,
    DateOnly? ProjectEndDate,
    decimal? TotalPoolAmount,
    decimal? MinGrantAmount,
    decimal? MaxIndirectCostPercent,
    decimal? MaxInstitutionalDevelopmentPercent,
    PercentageBasis PercentageBasis,
    decimal? MaxAverageAnnualRevenue,
    DateOnly? PersonalDataProcessedUntil,
    IReadOnlyList<CostCategory> CostCategories,
    long MaxAttachmentSizeInBytes,
    long MaxApplicationSizeInBytes,
    IReadOnlyList<CompetitionAttachmentResponse> Attachments,
    IReadOnlyList<CompetitionContactResponse> Contacts);
