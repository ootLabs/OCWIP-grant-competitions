using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// The body of GET /me: who the cookie says you are.
///
/// This is how a client learns that a session is still alive, which is the
/// other half of "wylogowanie unieważnia sesję po stronie serwera" - without a
/// protected endpoint, a revoked session looks exactly like a live one.
/// Deliberately thin: an address, a name, a role and the entity the account
/// acts as, and no personal data that a screen has not asked for.
/// </summary>
public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    Role Role,
    /// <summary>
    /// The name of the Podmiot this account files applications as, null for an
    /// operator and a reviewer, who work for OCWIP and apply for nothing.
    ///
    /// Here rather than behind a second endpoint because the applicant's panel
    /// header names the entity on every screen (T-15.2), so a separate request
    /// would be a second round trip on every page load for one string. The name
    /// of an organisation is not personal data the way an address is, and the
    /// account only ever learns its own.
    /// </summary>
    string? EntityName);
