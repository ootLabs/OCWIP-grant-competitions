using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

/// <summary>
/// Signing in, signing out, and reading who is signed in (T-12.3).
///
/// Separate from AccountService on purpose: that class is the only write path
/// that creates an account and says so at the top of the file. Sessions are a
/// different lifetime and a different set of rules.
/// </summary>
internal sealed class SessionService(
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    ILogger<SessionService> logger)
    : ISessionService
{
    /// <summary>
    /// Verified against when the address belongs to nobody, purely so that the
    /// two cases take the same time. Without it, a missing account answers
    /// after one index lookup and an existing one after a deliberately slow
    /// key derivation, and the difference is large enough to read over the
    /// network: the endpoint would then enumerate accounts by stopwatch while
    /// every message stayed identical.
    ///
    /// Its own hasher rather than the injected one, because the value is
    /// computed once per process and must not depend on a scope. The work
    /// factor is the framework default, which is the one the real hashes in the
    /// database were written with.
    /// </summary>
    private static readonly Lazy<string> DecoyHash = new(() =>
        new PasswordHasher<User>().HashPassword(
            new User(), "nieistniejace-konto-nieistniejace-haslo"));

    /// <summary>
    /// One key derivation against a hash belonging to nobody, thrown away.
    /// Every path that answers <see cref="LoginResult.InvalidCredentials"/>
    /// WITHOUT having checked a real password has to call this, otherwise it
    /// answers sooner than the paths that did, and the stopwatch tells apart
    /// what every message here is careful not to. See <see cref="DecoyHash"/>.
    /// </summary>
    private static void BurnPasswordCheck(string password) =>
        new PasswordHasher<User>().VerifyHashedPassword(
            new User(), DecoyHash.Value, password);

    public async Task<LoginResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Neither field is guaranteed to be there. A positional record whose
        // JSON member is missing gets the CLR default, so `{"password":"x"}`
        // arrives with a null address and used to reach Trim() as a 500 whose
        // body carried a stack trace naming this file. The same answer as a
        // wrong password, not a validation error: login has exactly one failure
        // shape on purpose, and a second one is a second thing to compare
        // against. Blank counts as missing, because a form that posts an empty
        // box is the common way this arrives.
        if (string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrEmpty(request.Password))
        {
            return LoginResult.InvalidCredentials;
        }

        var user = await userManager.FindByEmailAsync(request.Email.Trim());

        if (user is null)
        {
            // Burn the same amount of time a real check would, then answer the
            // one message. See DecoyHash.
            BurnPasswordCheck(request.Password);

            // No address in the message, on purpose: a log full of attempted
            // addresses is a log full of the same PII the rest of this file
            // works to keep off the wire. Volume alone is enough to see a
            // scan in progress.
            logger.LogWarning("Login failed: unknown address");

            return LoginResult.InvalidCredentials;
        }

        // CheckPasswordSignInAsync, not PasswordSignInAsync, and the order is
        // the point. PasswordSignInAsync runs its pre checks, including
        // "is this address confirmed", BEFORE it looks at the password, so with
        // SignIn.RequireConfirmedEmail turned on it answers NotAllowed for an
        // unconfirmed account no matter what was typed. That single difference
        // tells an outsider which addresses have accounts here, which is the
        // one thing security rule 3 forbids. Confirmation is therefore checked
        // below, after the password has proved who is asking.
        //
        // lockoutOnFailure true (T-12.5): a wrong password here counts against
        // IdentityOptions.Lockout.MaxFailedAccessAttempts. CheckPasswordSignInAsync
        // checks the lockout state BEFORE the password (PreSignInCheck), so a
        // locked account gets LockedOut back below whether the password just
        // typed was right or wrong, which is what keeps a correct password
        // from silently unlocking early.
        var password = await signInManager.CheckPasswordSignInAsync(
            user, request.Password, lockoutOnFailure: true);

        if (password.IsLockedOut)
        {
            // The lockout answer is allowed to say that this address has an
            // account (see LoginOutcome.LockedOut), and a DEACTIVATED account
            // is the one case where it still may not: security rule 5 says we
            // never hard delete, so "this account is gone" has to keep looking
            // like "this account never existed", and a 429 after five attempts
            // on a deactivated address would turn soft delete into a way to
            // enumerate former users. Nothing is lost by hiding it: a
            // deactivated account cannot sign in whether it is locked or not.
            //
            // The burn is the other half of hiding it, and it is easy to miss.
            // A locked account never reaches the password hash at all, because
            // CheckPasswordSignInAsync checks the lockout first (PreSignInCheck),
            // so this path would answer after one index lookup while an unknown
            // address still pays for a full key derivation. Same body, same
            // status, and a difference of tens of milliseconds that says
            // "deactivated account" out loud to anyone holding a stopwatch.
            if (!user.IsActive)
            {
                BurnPasswordCheck(request.Password);

                return LoginResult.InvalidCredentials;
            }

            logger.LogWarning(
                "Login blocked for user {UserId}: account locked out", user.Id);

            var until = await userManager.GetLockoutEndDateAsync(user);

            // GetLockoutEndDateAsync only returns null when LockoutEnabled is
            // false, which cannot happen here: IsLockedOut just returned true,
            // and that requires both LockoutEnabled and a future LockoutEnd.
            return LoginResult.LockedOut(until!.Value);
        }

        if (!password.Succeeded)
        {
            // Never with the password (security rule 4): what failed is the
            // check, not what was typed.
            logger.LogWarning(
                "Login failed for user {UserId}: wrong password", user.Id);

            return LoginResult.InvalidCredentials;
        }

        // A deactivated account is a wrong password as far as the answer goes.
        // We do not hard delete (security rule 5), so "this account no longer
        // exists" and "this account never existed" have to look the same from
        // outside, otherwise soft delete becomes a way to enumerate former
        // users. The person affected reaches OCWIP, not a self service screen:
        // reactivation is still an open point in docs/model-danych.md.
        if (!user.IsActive)
        {
            return LoginResult.InvalidCredentials;
        }

        if (!user.EmailConfirmed)
        {
            return LoginResult.EmailNotConfirmed;
        }

        // isPersistent false: the cookie dies with the browser session as well
        // as with ExpireTimeSpan. A "remember me" box on a machine in a library
        // is a bad default, and nobody asked for one.
        await signInManager.SignInAsync(user, isPersistent: false);

        return LoginResult.Succeeded(new LoginResponse(
            user.Email!,
            user.FirstName,
            user.LastName,
            user.Role,
            LoginLandingPath.Resolve(user.Role, request.ReturnUrl)));
    }

    public async Task LogoutAsync(ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);

        if (user is not null)
        {
            // The half that makes this a SERVER side logout. SignOutAsync below
            // only asks the browser to drop the cookie, and a cookie that was
            // copied off a shared machine never sees that request: it stays
            // valid until it expires on its own. Rotating the security stamp
            // invalidates every cookie ever issued to this account, and the
            // validator checks the stamp on every request
            // (AuthenticationConfiguration sets ValidationInterval to zero), so
            // the copy stops working immediately.
            //
            // Signing out here therefore signs out everywhere, and that is the
            // intended reading of the card: the person at the library computer
            // cannot know which other sessions are open, so the safe answer is
            // to end all of them.
            await userManager.UpdateSecurityStampAsync(user);
        }

        await signInManager.SignOutAsync();
    }

    public async Task<CurrentUserResponse?> CurrentUserAsync(ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);

        // Deactivated between two requests, or the row is gone: the cookie is
        // still cryptographically fine and must still stop working.
        if (user is null || !user.IsActive)
        {
            return null;
        }

        return new CurrentUserResponse(
            user.Id, user.Email!, user.FirstName, user.LastName, user.Role);
    }
}
