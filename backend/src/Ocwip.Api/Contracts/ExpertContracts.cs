namespace Ocwip.Api.Contracts;

/// <summary>
/// One member of a competition's committee as the operator sees it (R-44,
/// report step 5.1).
/// </summary>
/// <param name="InvitationPending">The account was made by an invitation and its owner has not set a password yet.</param>
/// <param name="Assigned">Applications of this competition currently assigned to them.</param>
public sealed record CompetitionExpertResponse(
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    DateTimeOffset AppointedAt,
    bool InvitationPending,
    int Assigned);

/// <summary>
/// Appoint by address. An address with no account needs the person's name,
/// and then the person is invited: an account is made and a link to set the
/// password goes to that address.
/// </summary>
public sealed record AppointExpertRequest(string Email, string? FirstName = null, string? LastName = null);
