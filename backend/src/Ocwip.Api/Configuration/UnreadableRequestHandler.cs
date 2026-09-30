using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Ocwip.Api.Configuration;

/// <summary>
/// T-123: a request the framework cannot read (a JSON body cut short, a field
/// of the wrong type, bytes that are not UTF-8) is the caller's mistake, so it
/// answers with the status the framework itself chose for it, 400 in every such
/// case, and not with the 500 of an unhandled failure. Monitoring (T-116)
/// counts every 5xx as an outage.
///
/// The answer is ProblemDetails like the rest of the API and says nothing
/// about what was wrong: the exception message can quote the body, and a body
/// may hold a password or personal data (AGENTS.md, security rule 4).
/// </summary>
internal sealed class UnreadableRequestHandler(IProblemDetailsService problems) : IExceptionHandler
{
    internal const string Detail =
        "Nie udało się odczytać treści żądania. Sprawdź, czy jest poprawnym JSON-em w kodowaniu UTF-8.";

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException unreadable)
        {
            return false;
        }

        httpContext.Response.StatusCode = unreadable.StatusCode;

        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = unreadable.StatusCode,
                Detail = Detail,
            },
        });
    }
}
