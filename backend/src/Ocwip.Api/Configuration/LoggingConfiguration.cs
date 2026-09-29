namespace Ocwip.Api.Configuration;

/// <summary>
/// Logs a machine can read (T-116). Outside Development every entry is one
/// JSON line with its scopes, and ASP.NET Core opens a scope per request
/// carrying RequestId, RequestPath and TraceId, so every line a request
/// writes can be found by its id. The same id goes back to the caller in
/// X-Request-Id, and the TraceId of the scope is the traceId of every
/// ProblemDetails: either is what a person reporting an error hands over.
///
/// Development keeps the plain console: a person reads it, not a collector.
/// Nothing here logs a body, a header or a password (AGENTS.md, security
/// rule 4): scopes carry only the id, the path and the trace.
/// </summary>
public static class LoggingConfiguration
{
    public const string RequestIdHeader = "X-Request-Id";

    public static void AddOcwipLogging(this WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
        {
            return;
        }

        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
        });
    }

    public static IApplicationBuilder UseRequestIdHeader(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[RequestIdHeader] = context.TraceIdentifier;
                return Task.CompletedTask;
            });
            return next(context);
        });
}
