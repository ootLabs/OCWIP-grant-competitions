using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Authorization;

/// <summary>
/// Who may see one Podmiot's resource (T-13.2). The whole role table for
/// resource access is this one switch, so the rule can be read in one go
/// rather than reconstructed from conditions spread over endpoints.
/// </summary>
internal sealed class EntityScopedHandler(UserManager<User> userManager, AppDbContext dbContext)
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

    // The cards the caller acts for, read once per request for the same
    // reason as the caller: a list is authorized element by element.
    private IReadOnlySet<Guid>? _entityIds;

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

            // One method, see ResourceOwnership: membership of the card
            // (T-93a), so a co-worker in the same organisation sees the same
            // drafts and nobody outside it sees any.
            // An applicant account can also be an expert in another
            // competition (R-44): its own organisation's resources first,
            // then the expert's way in below, never instead of it.
            case Role.Applicant:
                _entityIds ??= await ResourceOwnership.EntityIdsOfAsync(dbContext, user.Id, CancellationToken.None);

                if (ResourceOwnership.BelongsTo(_entityIds, resource))
                {
                    context.Succeed(requirement);
                    return;
                }

                goto case Role.Reviewer;

            // An expert sees exactly the applications an operator assigned
            // to them (T-37), in a competition they are appointed to (R-44),
            // after the impartiality declaration for it (T-40a, report
            // decision 11): ExpertAppointments.MayEvaluateAsync, read here
            // and nowhere else, so the rule cannot drift from an endpoint
            // that forgets to check it.
            //
            // Anything that is not an Application (the T-13.2 test probe's
            // synthetic resource, for instance) has no assignment table to
            // consult and stays refused: an expert's access is scoped to
            // applications, not to Podmiot resources in general.
            case Role.Reviewer:
                //
                // An attachment goes with its application (T-40, "podgląd
                // pełnego wniosku wraz z załącznikami"): the same assignment
                // row, read through the attachment's own application id.
                var applicationId = resource switch
                {
                    Application application => application.Id,
                    Attachment attachment => attachment.ApplicationId,
                    _ => (Guid?)null,
                };

                if (applicationId is { } id
                    && await ExpertAppointments.MayEvaluateAsync(dbContext, user.Id, id, CancellationToken.None))
                {
                    context.Succeed(requirement);
                }

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
