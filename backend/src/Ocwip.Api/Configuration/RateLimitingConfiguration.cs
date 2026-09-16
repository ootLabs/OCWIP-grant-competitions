using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Ocwip.Api.Configuration;

/// <summary>
/// The IP half of brute force protection (T-12.5). The account half is
/// Identity's own lockout, configured in IdentityConfiguration.cs: that one
/// stops repeated guessing against ONE address, and cannot see an attack
/// spread across many addresses from one source, which is exactly what this
/// one is for. Applied per endpoint with RequireRateLimiting, on exactly the
/// five routes the card names: /login, /register, /forgot-password,
/// /reset-password and /resend-verification. /verify-email is deliberately
/// NOT among them: it neither checks a password nor sends a mail, and its
/// token is a single use value nobody guesses by repetition.
/// </summary>
public static class RateLimitingConfiguration
{
    public const string SensitivePolicy = "sensitive-auth";

    // Ten requests a minute from one address is generous for a person and
    // expensive for a script: a real applicant retyping a password, or
    // resending a verification mail, never gets near this. Threshold is
    // deliberately the same across all five endpoints rather than one number
    // per route, because a single shared meaning ("this many sensitive
    // requests, this often, from one address") is one thing to reason about
    // instead of five.
    public const int DefaultPermitLimit = 10;
    public const int DefaultWindowSeconds = 60;

    public static IServiceCollection AddOcwipRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue<int?>("RateLimiting:PermitLimit")
            ?? DefaultPermitLimit;

        var windowSeconds = configuration.GetValue<int?>("RateLimiting:WindowSeconds")
            ?? DefaultWindowSeconds;

        // A limit of zero or less does not disable the limiter, it makes
        // every single request the one that is over it: the endpoints this
        // guards would answer 429 to everyone, permanently, which is a worse
        // outage than the brute force attempt this card defends against.
        if (permitLimit <= 0)
        {
            throw new InvalidOperationException(
                "RateLimiting:PermitLimit must be greater than zero, "
                + $"and is {permitLimit}.");
        }

        if (windowSeconds <= 0)
        {
            throw new InvalidOperationException(
                "RateLimiting:WindowSeconds must be greater than zero, "
                + $"and is {windowSeconds}.");
        }

        services.AddRateLimiter(options =>
        {
            // 429 with a Polish message and no internal detail, the same
            // error shape as everything else in this API
            // (docs/architektura.md): a caller that hit the limit is not
            // broken, and the response must not read like it is.
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.Headers.RetryAfter =
                    windowSeconds.ToString();

                await Results
                    .Problem(
                        "Zbyt wiele prób z tego adresu. Spróbuj ponownie za chwilę.",
                        statusCode: StatusCodes.Status429TooManyRequests)
                    .ExecuteAsync(context.HttpContext);
            };

            options.AddPolicy(SensitivePolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ClientAddress(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        // No queue: a request over the limit is refused right
                        // away, not held and retried on our side. Queueing
                        // would turn a burst of attempts into a burst of
                        // delayed logins instead of fewer logins.
                        QueueLimit = 0,
                    }));
        });

        return services;
    }

    // One partition per address also means one budget per OFFICE: a dozen
    // people behind one NAT, or on one mobile carrier's shared address,
    // spend the same ten requests. Raising RateLimiting:PermitLimit is the
    // lever for that, and it is the right one - partitioning by address plus
    // address-typed-in would hand an attacker a fresh budget for every
    // address they try, which is precisely the attack this dimension exists
    // to stop. Written down in .env.example next to the setting.
    //
    // The connection's own address, which is the RIGHT one only as long as
    // the API is reached directly, as it is in docker-compose today. Put a
    // reverse proxy or a load balancer in front of it and every request
    // arrives from the proxy, so all users collapse into ONE partition and
    // the tenth sign in anywhere in the product inside a minute answers 429:
    // a full authentication outage, caused by the defence rather than by the
    // attack. The fix is not code here, it is a deployment switch: ASP.NET
    // Core installs the forwarded headers middleware itself when
    // ASPNETCORE_FORWARDEDHEADERS_ENABLED=true, and then this reads the
    // address the proxy passed on. Whoever sets up the environment (T-48)
    // has to set it, together with the proxy list that makes trusting that
    // header safe; an untrusted X-Forwarded-For is a limit an attacker
    // rotates around at will. Noted in .env.example and in
    // docs/architektura.md, because this is the one way this card can be
    // "done" and still not protect anything.
    //
    // Falls back to a constant rather than null: a partition key of null
    // would throw inside the limiter, and a request that somehow arrives
    // with no remote address (a unit test host, most often) is safer sharing
    // one partition than crashing the endpoint it is trying to reach.
    private static string ClientAddress(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
