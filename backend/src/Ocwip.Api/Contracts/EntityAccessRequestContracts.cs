using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// "Ta organizacja jest już zarejestrowana": the body of a request to join
/// the card with this NIP (T-93a, report step 2.2). The NIP rather than the
/// card's id, because the NIP is all the person knows.
/// </summary>
public sealed record EntityAccessRequestBody(string Nip);

/// <summary>One of the caller's own requests, as they follow it.</summary>
public sealed record MyEntityAccessRequest(
    Guid Id,
    string EntityName,
    EntityAccessRequestStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? DecidedAt);

/// <summary>
/// A request waiting for the founder. The address is there because the
/// founder has to recognise the person before letting them see every
/// application of the organisation, drafts included.
/// </summary>
public sealed record PendingEntityAccessRequest(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    DateTimeOffset RequestedAt);

/// <summary>
/// The founder's answer, or the operator's. The note is how the operator
/// checked the person outside the system, required from them and refused
/// from the founder.
/// </summary>
public sealed record EntityAccessDecisionBody(bool Approve, string? Note = null);

/// <summary>
/// A request nobody answered for seven days, on the operator's list (report
/// step 2.2): who asks, for which card, and who founded the card, so the
/// operator can phone both before deciding.
/// </summary>
public sealed record EscalatedEntityAccessRequest(
    Guid Id,
    string EntityName,
    string? Nip,
    string RequesterFirstName,
    string RequesterLastName,
    string RequesterEmail,
    string? FounderFirstName,
    string? FounderLastName,
    string? FounderEmail,
    DateTimeOffset RequestedAt);
