using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ocwip.Api.Configuration;

/// <summary>
/// How a session is carried, and only that (T-12.3).
///
/// The shape was settled in T-17 and docs/architektura.md: an HttpOnly cookie,
/// not a token in a header. This file implements that decision and writes down
/// the four places where Identity's defaults are wrong for an API rather than
/// for a Razor site.
///
/// Rate limiting and account lockout are NOT here. They are T-12.5, and the
/// lockout columns already exist in the schema, so that card sets numbers.
/// </summary>
public static class AuthenticationConfiguration
{
    /// <summary>
    /// Eight hours. Long enough that an operator working through a day of
    /// applications is not thrown out mid review, short enough that a session
    /// left open on a shared computer in a library does not survive until the
    /// next morning. Sliding, so the clock measures inactivity rather than the
    /// length of the working day.
    /// </summary>
    public const int DefaultSessionLifetimeHours = 8;

    public const string CookieName = "ocwip.session";

    /// <summary>
    /// <paramref name="hasStore"/> says whether Identity's EF store was
    /// registered, which happens only when there is a connection string (see
    /// Program.cs). Without it the security stamp cannot be validated, because
    /// validating it means reading the account row. The cookie handler is still
    /// registered in that case, so that the pipeline builds and every protected
    /// endpoint answers 401 rather than taking routing down with it.
    /// </summary>
    public static IServiceCollection AddOcwipAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment,
        bool hasStore)
    {
        var lifetimeHours = configuration.GetValue<int?>("Auth:SessionLifetimeHours")
            ?? DefaultSessionLifetimeHours;

        // Secure cookies are the rule and the default. Development is the one
        // exception, and it is not a preference: the stack runs on plain http
        // at localhost, a Secure cookie is dropped by the browser there, and a
        // dropped session cookie means nothing works locally at all. Spelled as
        // a configuration value so a deployment can be explicit either way.
        var secure = configuration.GetValue<bool?>("Auth:SecureCookie")
            ?? !isDevelopment;

        // Lax, not Strict: Strict withholds the cookie on the FIRST request
        // that follows an external link, so an applicant arriving from the mail
        // announcing the results lands signed out on a page that knows it, and
        // the report's rule about coming back to the competition page breaks on
        // the one journey it was written for. Configurable because the front
        // and the API may end up on different sites, which needs None, and None
        // needs Secure.
        var sameSite = configuration.GetValue<string?>("Auth:CookieSameSite") is { } configured
            && Enum.TryParse<SameSiteMode>(configured, ignoreCase: true, out var parsed)
                ? parsed
                : SameSiteMode.Lax;

        services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            // All four Identity cookie schemes, not just the application one.
            // SignInManager.SignOutAsync signs out of three of them by name,
            // and a scheme that was never registered throws instead of being
            // skipped, which would turn logout into a 500.
            .AddIdentityCookies(builder => builder.ApplicationCookie?.Configure(options =>
            {
                options.Cookie.Name = CookieName;
                // HttpOnly is Identity's default and is restated here because
                // it is an acceptance criterion: script must not be able to
                // read the session, which is the whole reason this is a cookie
                // and not a token in localStorage.
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = secure
                    ? CookieSecurePolicy.Always
                    : CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = sameSite;

                options.ExpireTimeSpan = TimeSpan.FromHours(lifetimeHours);
                options.SlidingExpiration = true;

                // This is an API. Identity's default answer to an
                // unauthenticated request is 302 to /Account/Login, a page that
                // does not exist here, so a fetch would read 200 and a login
                // form instead of 401. ProblemDetails, because that is the
                // error format of this API (docs/architektura.md), and in
                // Polish, because the front shows it to a person.
                options.Events.OnRedirectToLogin = context =>
                    Problem(context, StatusCodes.Status401Unauthorized,
                        "Zaloguj się, żeby zobaczyć tę stronę.");

                options.Events.OnRedirectToAccessDenied = context =>
                    Problem(context, StatusCodes.Status403Forbidden,
                        "Nie masz dostępu do tej strony.");
            }));

        if (hasStore)
        {
            // Zero, not the framework's thirty minutes. Thirty minutes means a
            // logout leaves the session usable for up to half an hour from a
            // copy of the cookie, which is exactly the failure the card
            // describes: shared computers in libraries and in organisations.
            // The cost is one SELECT on the account per request, paid so that
            // "wyloguj" means logged out now and not soon.
            services.Configure<SecurityStampValidatorOptions>(
                options => options.ValidationInterval = TimeSpan.Zero);

            // AddIdentityCore registers neither of these and AddIdentityCookies
            // asks for both by interface: the application cookie validates the
            // stamp on every request, and the remember-me cookie, which we
            // never issue but which is registered above, asks for the two
            // factor one. A missing registration surfaces as an exception on
            // the first authenticated request rather than at startup.
            services.TryAddScoped<ISecurityStampValidator, SecurityStampValidator<Models.User>>();
            services.TryAddScoped<ITwoFactorSecurityStampValidator,
                TwoFactorSecurityStampValidator<Models.User>>();
        }

        return services;
    }

    private static Task Problem(
        Microsoft.AspNetCore.Authentication.RedirectContext<CookieAuthenticationOptions> context,
        int statusCode,
        string detail) =>
        Results.Problem(detail: detail, statusCode: statusCode)
            .ExecuteAsync(context.HttpContext);
}
