using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Data;

namespace Ocwip.Api.Endpoints;

/// <summary>
/// Records each successful read of a resource holding personal data in
/// personal_data_reads (T-47a, Models/PersonalDataRead.cs). Attached to the
/// endpoint, not called from the handler, so an endpoint cannot return the
/// data and forget the log: the list of logged endpoints is the list of
/// <c>.LogsPersonalDataRead</c> calls.
///
/// Only a successful answer is logged: a refusal read nothing. The row goes
/// in with its own INSERT, outside the change tracker, so it can neither
/// carry the handler's pending changes with it nor be lost with them. If it
/// cannot be written the request fails: data read without the log is the
/// one outcome the log exists to rule out.
/// </summary>
public static class PersonalDataReadFilter
{
    public static RouteHandlerBuilder LogsPersonalDataRead(this RouteHandlerBuilder builder, string resource, string routeValue) =>
        builder.AddEndpointFilter(async (context, next) =>
        {
            var result = await next(context);

            if (!Succeeded(result)
                || context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) is not { } caller
                || !Guid.TryParse(caller, out var userId)
                || !Guid.TryParse(context.HttpContext.Request.RouteValues[routeValue]?.ToString(), out var resourceId))
            {
                return result;
            }

            var endpoint = context.HttpContext.GetEndpoint() is RouteEndpoint route
                ? $"{context.HttpContext.Request.Method} {route.RoutePattern.RawText}"
                : context.HttpContext.Request.Method;

            var database = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            await database.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO personal_data_reads (user_id, resource, resource_id, endpoint) VALUES ({userId}, {resource}, {resourceId}, {endpoint})",
                context.HttpContext.RequestAborted);

            return result;
        });

    /// <summary>Whether the handler's answer carries the data: a 2xx, a file, or a plain value.</summary>
    internal static bool Succeeded(object? result) => result switch
    {
        INestedHttpResult nested => Succeeded(nested.Result),
        IStatusCodeHttpResult { StatusCode: { } code } => code is >= 200 and < 300,
        _ => result is not null,
    };
}
