using Ocwip.Api.Models;

namespace Ocwip.Api.Contracts;

/// <summary>
/// The body of a successful POST /login. The session itself travels in the
/// cookie (docs/architektura.md), so nothing here is a credential.
///
/// RedirectPath is the card's "przekierowanie zależne od roli" answered in one
/// place on the server rather than by every screen that happens to call login.
/// See <see cref="Services.LoginLandingPath"/> for why the server decides it.
/// </summary>
public sealed record LoginResponse(
    string Email,
    string FirstName,
    string LastName,
    Role Role,
    string RedirectPath);
