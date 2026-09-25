namespace Ocwip.Api.Contracts;

/// <summary>
/// The operator's decision on one application (T-42): the awarded amount, null
/// for none, and a note. Both whole, like every autosave in this API.
/// </summary>
public sealed record GrantDecisionRequest(decimal? AwardedGrant, string? Note);

public sealed record GrantDecisionResponse(Guid ApplicationId, decimal? AwardedGrant, string? Note);

/// <summary>What approving the results did, per result status.</summary>
public sealed record ResultsApprovalResponse(DateTimeOffset ApprovedAt, int Funded, int Reserve, int Rejected);
