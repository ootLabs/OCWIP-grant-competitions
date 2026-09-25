namespace Ocwip.Api.Contracts;

/// <summary>
/// Body of POST /applications/{id}/assignments (T-37): which reviewer to
/// assign. The application comes from the route, not from here, the same
/// split ApplicationEndpoints.cs uses for the competition id on draft
/// creation.
/// </summary>
public sealed record AssignReviewerRequest(Guid ReviewerId);
