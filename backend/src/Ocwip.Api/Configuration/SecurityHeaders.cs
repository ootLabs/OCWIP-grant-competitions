namespace Ocwip.Api.Configuration;

/// <summary>
/// The security headers of every API answer (T-112, from T-47). The API
/// serves JSON and files for download, never a page, so its policy denies
/// everything a page could load and any framing. Set when the answer starts,
/// not before the pipeline, because the exception handler clears the headers
/// of a failed answer and would take these with them.
/// </summary>
public static class SecurityHeaders
{
    public const string ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                // A file is what its Content-Type says, never sniffed into HTML.
                headers.XContentTypeOptions = "nosniff";
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
                // Reset and verification tokens travel in the address.
                headers["Referrer-Policy"] = "no-referrer";
                return Task.CompletedTask;
            });

            return next(context);
        });
}
