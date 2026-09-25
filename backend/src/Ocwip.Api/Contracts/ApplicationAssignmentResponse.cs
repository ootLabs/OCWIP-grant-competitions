namespace Ocwip.Api.Contracts;

/// <summary>
/// One reviewer's assignment to one application (T-37), body of both
/// POST /applications/{id}/assignments and DELETE .../assignments/{reviewerId}.
/// </summary>
public sealed record ApplicationAssignmentResponse(
    Guid Id,
    Guid ApplicationId,
    Guid ReviewerId,
    bool IsActive);
