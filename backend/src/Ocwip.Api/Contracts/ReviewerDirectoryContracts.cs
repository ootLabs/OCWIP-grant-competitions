namespace Ocwip.Api.Contracts;

/// <summary>An expert account the operator can assign applications to (T-41).</summary>
public sealed record ReviewerSummary(Guid Id, string Name, string Email);

/// <summary>One active assignment in a competition, for the operator's evaluation screen (T-41).</summary>
public sealed record CompetitionAssignment(Guid ApplicationId, Guid ReviewerId);
