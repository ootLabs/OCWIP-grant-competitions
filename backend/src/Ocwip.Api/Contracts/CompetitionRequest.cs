using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// The body an operator sends to create or to edit a competition (T-20).
///
/// One record for both, because the two are the same set of settings and a
/// separate update type would drift from this one field by field. What may
/// change and when is a question about the STATE of the competition, not about
/// the shape of the request, and it is answered in CompetitionService.
///
/// Deliberately not carrying the status: a competition never changes state by
/// having a different value sent to it, only through the transition table.
/// The status of a fresh competition is always Draft.
///
/// The parameters from steps 1.2 to 1.6 of the announcement wizard arrived in
/// T-20a and are all optional, because the wizard does not block moving
/// between its steps: a competition is written over several sittings and
/// completeness is a question asked at publication, not at every save.
///
/// The planned publication date of step 1.1 is still not here. Two mechanisms
/// for publishing cannot stand side by side without deciding which one wins,
/// which is R-27 in docs/runbook/rozbieznosci.md.
/// </summary>
public sealed record CompetitionRequest(
    string Number,
    string Title,
    string? Description,
    DateTimeOffset StartDate,
    DateTimeOffset? EndDate,
    bool IsContinuousIntake,
    decimal MaxGrantAmount,
    Guid? FormDefinitionId,
    // Step 1.2
    string? ExpectedResults = null,
    string? RulesUrl = null,
    // Step 1.1 and 1.6, the two messages after a submission
    string? SubmissionNotice = null,
    string? SubmissionEmailBody = null,
    // Step 1.3
    bool RequiresPaperSubmission = false,
    DateTimeOffset? PaperSubmissionDeadline = null,
    string? PaperSubmissionAddress = null,
    // Step 1.4
    DateOnly? ProjectStartDate = null,
    DateOnly? ProjectEndDate = null,
    decimal? TotalPoolAmount = null,
    decimal? MinGrantAmount = null,
    decimal? MaxIndirectCostPercent = null,
    decimal? MaxInstitutionalDevelopmentPercent = null,
    PercentageBasis PercentageBasis = PercentageBasis.GrantAmount,
    decimal? MaxAverageAnnualRevenue = null,
    DateOnly? PersonalDataProcessedUntil = null,
    IReadOnlyList<CostCategory>? CostCategories = null,
    // Step 1.5
    long? MaxAttachmentSizeInBytes = null,
    long? MaxApplicationSizeInBytes = null,
    IReadOnlyList<CompetitionAttachmentRequest>? Attachments = null,
    // Step 1.6
    IReadOnlyList<Guid>? ContactUserIds = null);
