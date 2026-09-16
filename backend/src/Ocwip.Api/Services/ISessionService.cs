using System.Security.Claims;
using Ocwip.Api.Contracts;

namespace Ocwip.Api.Services;

/// <summary>
/// What a failed sign in is allowed to say out loud.
///
/// Two failure values, and which one a caller gets is the whole security design
/// of this card. Security rule 3 forbids letting an outsider tell a free
/// address from a taken one, and the card also wants an unconfirmed account to
/// be told so in plain language. Those look contradictory and are not:
/// <see cref="EmailNotConfirmed"/> is only ever returned AFTER the password has
/// been verified, so the only person who can see it is the person who already
/// knows the password. Everything else, unknown address, wrong password,
/// deactivated account, collapses into <see cref="InvalidCredentials"/> with
/// one message.
/// </summary>
internal enum LoginOutcome
{
    Succeeded,

    /// <summary>
    /// One value for every reason the caller may not learn: no such account,
    /// wrong password, account deactivated. There is no branch to get wrong.
    /// </summary>
    InvalidCredentials,

    /// <summary>
    /// The password was right and the address was never confirmed. Reachable
    /// only past a correct password, see the note on this enum.
    /// </summary>
    EmailNotConfirmed,

    /// <summary>
    /// T-12.5: too many wrong passwords in a row locked this account
    /// temporarily. Unlike every value above, this one CAN tell an outsider
    /// that the address has an account, because reaching it already took five
    /// wrong passwords against that one address; the card asks for a readable
    /// message here on purpose, and the IP based limiter (RequireRateLimiting
    /// on /login) is what keeps that five attempt probe expensive to repeat
    /// across many addresses.
    /// </summary>
    LockedOut,
}

internal sealed record LoginResult(
    LoginOutcome Outcome,
    LoginResponse? Session,
    DateTimeOffset? LockedOutUntil = null)
{
    public static LoginResult InvalidCredentials { get; } =
        new(LoginOutcome.InvalidCredentials, null);

    public static LoginResult EmailNotConfirmed { get; } =
        new(LoginOutcome.EmailNotConfirmed, null);

    public static LoginResult Succeeded(LoginResponse session) =>
        new(LoginOutcome.Succeeded, session);

    public static LoginResult LockedOut(DateTimeOffset until) =>
        new(LoginOutcome.LockedOut, null, until);
}

internal interface ISessionService
{
    /// <summary>
    /// Verifies the credentials and, on success, issues the session cookie on
    /// the current response.
    /// </summary>
    Task<LoginResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends the session SERVER SIDE, not just in this browser. See the
    /// implementation for why that is more than deleting a cookie.
    /// </summary>
    Task LogoutAsync(ClaimsPrincipal principal);

    /// <summary>
    /// The account behind a valid cookie, or null when the row is gone or no
    /// longer active.
    /// </summary>
    Task<CurrentUserResponse?> CurrentUserAsync(ClaimsPrincipal principal);
}
