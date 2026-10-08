using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Ocwip.Api.Authorization;
using Ocwip.Api.Data;
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
///
/// An applicant appointed to a competition's committee (R-44) also gets the
/// Reviewer claim, so the expert's routes and panel open for them without a
/// second account. The security stamp is validated on every request
/// (AuthenticationConfiguration), so an appointment or its withdrawal shows
/// on the next request. The claim only opens the door to the expert's routes;
/// which applications are behind it is still ExpertAppointments' answer.
/// </summary>
internal sealed class RoleClaimsPrincipalFactory(
    UserManager<User> userManager,
    IOptions<IdentityOptions> options,
    IServiceProvider services)
    : UserClaimsPrincipalFactory<User>(userManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        // ToString on the enum, matching how the column is stored
        // (UserConfiguration maps it as text). One spelling for the database,
        // the claim and docs/reguly-biznesowe.md.
        identity.AddClaim(new Claim(Options.ClaimsIdentity.RoleClaimType, user.Role.ToString()));

        // A host without a database (some tests) has no appointments to read.
        if (user.Role is Role.Applicant
            && services.GetService<AppDbContext>() is { } context
            && await ExpertAppointments.IsExpertAnywhereAsync(context, user.Id, CancellationToken.None))
        {
            identity.AddClaim(new Claim(Options.ClaimsIdentity.RoleClaimType, nameof(Role.Reviewer)));
        }

        return identity;
    }
}
