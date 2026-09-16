using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Authorization;
using Ocwip.Api.Configuration;
using Ocwip.Api.Models;

namespace Ocwip.Api.Tests.Authorization;

/// <summary>
/// Routes that exist only inside these tests, so the policies can be proved
/// end to end without inventing product endpoints this card does not own.
///
/// The alternative was to wait for the first competition or application
/// endpoint and test the policies through it, which would mean shipping the
/// authorization layer with nothing but unit tests behind it, and would tie
/// the proof of "403, not 500 and not an empty page" to a card that has not
/// been written yet. These routes go through the REAL pipeline: the real
/// cookie handler, the real policies, the real handler.
/// </summary>
internal static class PolicyProbeEndpoints
{
    public const string OperatorOnly = "/test-probe/operator-only";
    public const string NoRuleAtAll = "/test-probe/no-rule";
    public const string OwnedResource = "/test-probe/owned-resource";

    /// <summary>
    /// Added through a startup filter so the routes join the application's own
    /// pipeline, after UseAuthentication and UseAuthorization, rather than a
    /// copy of it built for the test.
    /// </summary>
    public static void AddPolicyProbes(this IServiceCollection services) =>
        services.AddSingleton<IStartupFilter, Filter>();

    private sealed class Filter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            builder =>
            {
                next(builder);

                builder.UseEndpoints(endpoints =>
                {
                    endpoints
                        .MapGet(OperatorOnly, () => Results.Ok("operator"))
                        .RequireAuthorization(
                            AuthorizationConfiguration.Names.For(Role.Operator));

                    // Declares NOTHING. This is the endpoint somebody forgets
                    // to protect, and the fallback policy is what has to
                    // catch it.
                    endpoints.MapGet(NoRuleAtAll, () => Results.Ok("oops"));

                    // The resource path: the handler decides, per caller and
                    // per resource, using the entity id passed in the query.
                    endpoints
                        .MapGet(OwnedResource, async (
                            Guid entityId,
                            HttpContext context,
                            IAuthorizationService authorization) =>
                        {
                            var result = await authorization.AuthorizeAsync(
                                context.User,
                                new ProbeResource(entityId),
                                AuthorizationConfiguration.Names.OwnsResource);

                            return result.Succeeded
                                ? Results.Ok("resource")
                                : Results.Problem(
                                    "Nie masz dostępu do tych danych.",
                                    statusCode: StatusCodes.Status403Forbidden);
                        })
                        .RequireAuthorization();
                });
            };
    }

    /// <summary>
    /// Stands in for an Application or an Entity. The handler is written
    /// against IEntityScoped precisely so that it never needs to know which.
    /// </summary>
    private sealed record ProbeResource(Guid EntityId) : IEntityScoped;
}
