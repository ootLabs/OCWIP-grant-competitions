using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Ocwip.Api.Models;

namespace Ocwip.Api.Authorization;

/// <summary>
/// Who may see one Podmiot's resource (T-13.2). The whole role table for
/// resource access is this one switch, so the rule can be read in one go
/// rather than reconstructed from conditions spread over endpoints.
/// </summary>
internal sealed class EntityScopedHandler(UserManager<User> userManager)
    : AuthorizationHandler<EntityScopedRequirement, IEntityScoped>
{
    // The handler is registered scoped, so one instance serves one request and
    // this cache lives exactly that long. It matters as soon as an endpoint
    // authorizes a LIST: the requirement is evaluated once per resource, and
    // without this each element would repeat the same SELECT over users. The
    // answer cannot go stale inside a request, because the row cannot change
    // between two elements of the same response.
    private User? _caller;
    private bool _resolved;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        EntityScopedRequirement requirement,
        IEntityScoped resource)
    {
        // Nothing is granted by falling through: this handler only ever calls
        // Succeed, and a requirement nobody succeeds is a requirement that
        // failed. That is the framework's half of "a missing rule means no
        // access", the other half being the fallback policy in
        // AuthorizationConfiguration.
        var user = await CallerAsync(context);

        // ActiveAccountStampValidator already refuses a deactivated account on
        // every request, so this is a second lock on the same door. It stays
        // because it is free next to the read above, and because the day
        // somebody authorizes a resource outside the cookie pipeline is the
        // day the first lock stops being there at all.
        if (user is null || !user.IsActive)
        {
            return;
        }

        switch (user.Role)
        {
            // The client put it plainly: the operator sees everything. There
            // is no ownership question to ask, which is also why this arm
            // comes first.
            case Role.Operator:
                context.Succeed(requirement);
                return;

            // One method, see ResourceOwnership: after R-01 this stops being
            // a comparison and becomes a membership lookup, and this switch
            // does not change.
            case Role.Applicant:
                if (ResourceOwnership.BelongsTo(user, resource))
                {
                    context.Succeed(requirement);
                }

                return;

            // Deliberately refused, not forgotten. A reviewer may see the
            // applications ASSIGNED to them, and nothing assigns anything
            // yet: that mechanism is T-37. Until it exists the honest answer
            // is no, because the alternative is a reviewer seeing every
            // application in the system in the window between this card and
            // that one.
            case Role.Reviewer:
                return;

            // No default arm that grants anything. A role added to the enum
            // (R-02 proposes an administrator) lands here and is refused
            // until somebody writes its rule, which is the safe direction to
            // fail in and is asserted by a test over Enum.GetValues.
            default:
                return;
        }
    }

    private async Task<User?> CallerAsync(AuthorizationHandlerContext context)
    {
        if (!_resolved)
        {
            _caller = await userManager.GetUserAsync(context.User);
            _resolved = true;
        }

        return _caller;
    }
}
