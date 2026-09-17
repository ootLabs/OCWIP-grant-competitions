using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Authorization;
using Ocwip.Api.Configuration;
using Ocwip.Api.Data;
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
    /// The probe T-13.3 needs, and the one shape OwnedResource cannot stand in
    /// for: the caller names a RESOURCE, not its owner.
    ///
    /// OwnedResource takes the entity id straight off the query string, so a
    /// route that trusted the identifier it was handed would pass every test in
    /// AuthorizationLayerTests and still leak. Here the row is read from the
    /// database first and the stored Application is what goes to the
    /// authorization service, which is the order every product route has to
    /// follow: load, then ask, then answer.
    /// </summary>
    public static string ApplicationById(Guid id) => $"/test-probe/applications/{id}";

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

                    endpoints
                        .MapGet("/test-probe/applications/{id:guid}", async (
                            Guid id,
                            HttpContext context,
                            IAuthorizationService authorization,
                            // Nullable [FromServices] for the reason spelled
                            // out in AccountEndpoints.cs: AppDbContext is
                            // registered only when there is a connection
                            // string, and a non nullable parameter makes
                            // minimal APIs resolve the source while building
                            // EVERY route, which takes the whole routing table
                            // down on a host without a database.
                            [FromServices] AppDbContext? data) =>
                        {
                            if (data is null)
                            {
                                return Results.Problem(
                                    statusCode: StatusCodes.Status503ServiceUnavailable);
                            }

                            var application = await data.Applications
                                .AsNoTracking()
                                .SingleOrDefaultAsync(x => x.Id == id);

                            // A row that is not there is a 404, while a row
                            // that is there and is not yours is a 403. The
                            // card asks for the 403 by name, and collapsing
                            // both into one answer would contradict it. The
                            // distinction leaks nothing worth having either:
                            // application identifiers are version 4 GUIDs, so
                            // there is no sequence to walk, and the number that
                            // IS guessable (001, 002) is not what addresses a
                            // row. See docs/architektura.md.
                            if (application is null)
                            {
                                return Results.NotFound();
                            }

                            var result = await authorization.AuthorizeAsync(
                                context.User,
                                application,
                                AuthorizationConfiguration.Names.OwnsResource);

                            // The answers are in the body on purpose. A test
                            // asserting only on a status code cannot tell a
                            // refusal apart from a 403 that shipped the
                            // organisation's data alongside it, and the answers
                            // are where the personal data actually is.
                            return result.Succeeded
                                ? Results.Ok(new ProbeApplication(
                                    application.Id,
                                    application.Number,
                                    application.Answers.GetRawText()))
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

    /// <summary>What a successful read of an application hands back.</summary>
    internal sealed record ProbeApplication(Guid Id, string? Number, string Answers);
}
