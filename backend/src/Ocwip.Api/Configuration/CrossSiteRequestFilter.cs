using Microsoft.AspNetCore.Mvc;
namespace Ocwip.Api.Configuration;

/// <summary>
/// The second barrier in front of a request that changes something (S-15).
/// The first is the session cookie's <c>SameSite</c>, and until this filter it
/// was the only one: there is no antiforgery token here, the three multipart
/// routes disable the framework's check outright, and CORS does not help,
/// because it decides who may READ an answer, not who may send a request.
///
/// A form on a page belonging to somebody else can POST to this API with the
/// browser attaching the session, and a form submission needs no preflight.
/// So: a state changing request that says it comes from somewhere else is
/// refused, whatever the cookie policy of the day is.
///
/// Reads two headers, both set by the browser and neither settable from a
/// page: <c>Origin</c>, which every current browser sends on a cross site
/// request that changes something, and <c>Sec-Fetch-Site</c>, which says the
/// same thing in one word. A request carrying neither (a script outside a
/// browser, a health probe, the test host) is let through: this filter exists
/// against a browser doing what a page told it to, and refusing everything
/// unlabelled would break every non browser caller for no gain.
/// </summary>
internal static class CrossSiteRequestFilter
{
    private static readonly string[] SafeMethods = ["GET", "HEAD", "OPTIONS", "TRACE"];

    public static IApplicationBuilder UseCrossSiteRequestFilter(this IApplicationBuilder app, IConfiguration configuration)
    {
        var allowed = (configuration["Cors:Origins"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(origin => origin.TrimEnd('/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return app.Use(async (context, next) =>
        {
            if (!IsCrossSite(context, allowed))
            {
                await next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Detail = "Żądanie przyszło z innej witryny.",
            });
        });
    }

    internal static bool IsCrossSite(HttpContext context, IReadOnlySet<string> allowed)
    {
        if (SafeMethods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        // "none" is the address bar: a person typing or a bookmark, which is
        // not a page acting on their behalf. "same-origin" and "same-site"
        // are the product talking to itself.
        if (context.Request.Headers["Sec-Fetch-Site"].FirstOrDefault() is { Length: > 0 } site
            && site is not ("same-origin" or "same-site" or "none"))
        {
            return true;
        }

        return context.Request.Headers.Origin.FirstOrDefault() is { Length: > 0 } origin
            && !allowed.Contains(origin.TrimEnd('/'));
    }
}
