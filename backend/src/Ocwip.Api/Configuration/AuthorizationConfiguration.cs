using Microsoft.AspNetCore.Authorization;
using Ocwip.Api.Authorization;
using Ocwip.Api.Models;

namespace Ocwip.Api.Configuration;

/// <summary>
/// Every access rule in the product, in one file (T-13.2).
///
/// The card asks for exactly that, and for a specific reason: rules spread
/// over endpoints cannot be read as a set, so nobody can answer "who can see
/// an application" without grepping, and the answer drifts as endpoints are
/// added. A policy named here and applied by name at the route is a rule that
/// stays in one place while the routes multiply.
/// </summary>
public static class AuthorizationConfiguration
{
    /// <summary>
    /// Policy names. One per role, derived from the enum below rather than
    /// typed out, so <see cref="Names.For"/> is the only spelling of them.
    /// </summary>
    public static class Names
    {
        /// <summary>
        /// The resource policy: may this caller see THIS resource. Backed by
        /// EntityScopedRequirement and its handler.
        /// </summary>
        public const string OwnsResource = "resource.owner";

        /// <summary>Reading one evaluation (T-38), EvaluationAccessHandler.</summary>
        public const string ReadsEvaluation = "evaluation.read";

        /// <summary>Saving or finishing one evaluation (T-38), EvaluationAccessHandler.</summary>
        public const string WritesEvaluation = "evaluation.write";

        public static string For(Role role) => $"role.{role}";
    }

    /// <summary>
    /// <paramref name="hasStore"/> says whether Identity's EF store was
    /// registered, which happens only when there is a connection string (see
    /// Program.cs). The POLICIES need no database and are always built, so
    /// every route keeps its metadata and the pipeline still refuses what it
    /// should. The resource handler does need one, because deciding whose a
    /// resource is means reading the account row, so without a store it is
    /// not registered at all and the resource requirement is simply never
    /// succeeded: no database means no resource access, which is the safe
    /// direction to fail in.
    /// </summary>
    public static IServiceCollection AddOcwipAuthorization(
        this IServiceCollection services,
        bool hasStore)
    {
        services.AddAuthorization(options =>
        {
            // The card's overriding principle, and the only version of it
            // that survives contact with a growing application: an endpoint
            // that declares nothing is refused by the framework rather than
            // served to everyone. Discipline does not scale here, because the
            // failure is invisible - a forgotten [Authorize] looks exactly
            // like a public endpoint until somebody reads the personal data
            // behind it.
            //
            // The cost is that every genuinely public route now has to say so
            // out loud with AllowAnonymous: /register, /login, /logout,
            // /verify-email, /resend-verification, /forgot-password,
            // /reset-password and both health probes do. That is the right
            // way round: a public endpoint is a decision somebody made, and
            // it now reads like one.
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            // A policy per role, built by walking the enum. R-02 in
            // docs/runbook/rozbieznosci.md proposes a fourth role
            // (administrator) and the instruction there is to build the
            // policies so that adding one is a value in the enum rather than
            // a rewrite of the handlers. This loop is that instruction,
            // executed: add the value, get the policy.
            foreach (var role in Enum.GetValues<Role>())
            {
                options.AddPolicy(
                    Names.For(role),
                    policy => policy
                        .RequireAuthenticatedUser()
                        // The role travels as a standard claim, written by
                        // RoleClaimsPrincipalFactory out of the column,
                        // because there is no AspNetRoles table here.
                        .RequireRole(role.ToString()));
            }

            options.AddPolicy(
                Names.OwnsResource,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new EntityScopedRequirement()));

            options.AddPolicy(
                Names.ReadsEvaluation,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new EvaluationAccessRequirement(Write: false)));

            options.AddPolicy(
                Names.WritesEvaluation,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(new EvaluationAccessRequirement(Write: true)));
        });

        if (hasStore)
        {
            services.AddScoped<IAuthorizationHandler, EntityScopedHandler>();
            services.AddScoped<IAuthorizationHandler, EvaluationAccessHandler>();
        }

        return services;
    }
}
