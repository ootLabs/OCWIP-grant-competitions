using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// The body of GET /me: who the cookie says you are.
///
/// This is how a client learns that a session is still alive, which is the
/// other half of "wylogowanie unieważnia sesję po stronie serwera" - without a
/// protected endpoint, a revoked session looks exactly like a live one.
/// Deliberately thin: an address, a name and a role, and no personal data that
/// a screen has not asked for.
/// </summary>
public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    Role Role);
