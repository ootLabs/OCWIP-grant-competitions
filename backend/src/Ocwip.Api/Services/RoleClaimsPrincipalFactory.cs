using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

/// <summary>
/// Puts the role column into the signed in principal.
///
/// Identity's own factory reads roles from AspNetUserRoles, and those tables do
/// not exist here: AppDbContext derives from IdentityUserContext exactly so
/// that they never do, and the role is a column on the account instead
/// (Models/Role.cs). Without this class the principal carries no role claim at
/// all, and every authorization rule written in T-13.2 would quietly deny
/// everything, or worse, be written to read the database on each check.
///
/// The claim type is the standard one, so [Authorize(Roles = ...)],
/// IsInRole and the policies of T-13.2 all keep working unchanged.
/// </summary>
internal sealed class RoleClaimsPrincipalFactory(
    UserManager<User> userManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<User>(userManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        // ToString on the enum, matching how the column is stored
        // (UserConfiguration maps it as text). One spelling for the database,
        // the claim and docs/reguly-biznesowe.md.
        identity.AddClaim(new Claim(Options.ClaimsIdentity.RoleClaimType, user.Role.ToString()));

        return identity;
    }
}
