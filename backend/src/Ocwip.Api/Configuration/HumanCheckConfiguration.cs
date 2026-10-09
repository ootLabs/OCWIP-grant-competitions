using Ocwip.Api.Services;

namespace Ocwip.Api.Configuration;

/// <summary>
/// The third layer against a script hammering the account forms, beside the
/// per-IP rate limit and Identity's per-account lockout: a Cloudflare
/// Turnstile token the browser earns on the page and sends in a header.
/// The limit and the lockout slow a script down; this one makes it pay for
/// every single request, from however many addresses it comes.
///
/// It runs as an endpoint filter, so after the rate limiter (middleware): a
/// request over the limit is turned away before anything asks Cloudflare.
/// </summary>
public static class HumanCheckConfiguration
{
    /// <summary>The header the forms send the token in (lib/human-check.ts).</summary>
    public const string TokenHeader = "X-Turnstile-Token";

    public const string ClientName = "turnstile";

    /// <summary>
    /// The refusal's ProblemDetails type, so the sign in screen can tell it
    /// from a 400 about the credentials, which it answers with one sentence.
    /// </summary>
    public const string ProblemType = "urn:ocwip:problem:human-check";

    internal const string RefusedMessage =
        "Nie udało się potwierdzić, że formularz wysyła człowiek. Zaznacz pole weryfikacji i spróbuj ponownie.";

    internal const string UnavailableMessage =
        "Weryfikacja chwilowo nie działa. Spróbuj ponownie za chwilę.";

    public static IServiceCollection AddOcwipHumanCheck(
        this IServiceCollection services, IConfiguration configuration)
    {
        var secret = configuration["Turnstile:SecretKey"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            return services;
        }

        services.AddSingleton(new TurnstileOptions(secret));
        services.AddHttpClient<IHumanCheck, TurnstileHumanCheck>(ClientName, client =>
        {
            client.BaseAddress = new Uri("https://challenges.cloudflare.com/");
            // A person is waiting on the other end of the form.
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }

    /// <summary>Lets the request through only with a token Cloudflare accepts.</summary>
    public static RouteHandlerBuilder RequireHumanCheck(this RouteHandlerBuilder builder) =>
        builder
            .AddEndpointFilter(async (context, next) =>
            {
                var http = context.HttpContext;
                var check = http.RequestServices.GetService<IHumanCheck>();
                if (check is null)
                {
                    return await next(context);
                }

                var outcome = await check.VerifyAsync(
                    http.Request.Headers[TokenHeader].ToString(),
                    http.Connection.RemoteIpAddress?.ToString(),
                    http.RequestAborted);

                return outcome switch
                {
                    HumanCheckOutcome.Passed => await next(context),
                    HumanCheckOutcome.Refused => Results.Problem(
                        RefusedMessage, statusCode: StatusCodes.Status400BadRequest, type: ProblemType),
                    _ => Results.Problem(UnavailableMessage, statusCode: StatusCodes.Status503ServiceUnavailable),
                };
            })
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
}
