using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

/// <summary>
/// Identity's security stamp check, plus one rule it has no way to know about:
/// a deactivated account is signed out.
///
/// Why it lives in the cookie pipeline rather than in an endpoint. We never
/// hard delete (security rule 5), so switching an account off is the only
/// "delete" there is, and a cookie issued before that moment stays
/// cryptographically perfect afterwards. A check written into one endpoint
/// protects that endpoint and nothing else, and T-13.2 is about to add a lot of
/// endpoints. Here it runs for every request that carries a session, so the
/// next endpoint inherits it without anybody remembering to ask.
///
/// Deactivation happens today by a statement against the database
/// (docs/model-danych.md), which is exactly the path that cannot be relied on
/// to rotate a security stamp on its way through.
/// </summary>
internal sealed class ActiveAccountStampValidator(
    IOptions<SecurityStampValidatorOptions> options,
    SignInManager<User> signInManager,
    ILoggerFactory logger)
    : SecurityStampValidator<User>(options, signInManager, logger)
{
    public override async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        await base.ValidateAsync(context);

        // The stamp check already rejected this one: rotated by a logout or by
        // a password change. Nothing left to read the account for.
        if (context.Principal is null)
        {
            return;
        }

        var user = await SignInManager.UserManager.GetUserAsync(context.Principal);

        if (user is null || !user.IsActive)
        {
            context.RejectPrincipal();
            await SignInManager.SignOutAsync();
        }
    }
}
