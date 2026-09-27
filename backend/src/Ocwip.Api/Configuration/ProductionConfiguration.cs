using System.Net;
using Microsoft.Extensions.Hosting;

namespace Ocwip.Api.Configuration;

/// <summary>
/// Settings that production cannot run without, checked before anything else
/// starts (T-91).
///
/// Every value below has a default that is right on a developer's machine and
/// wrong everywhere else: a verification link to localhost, a relay that is
/// not there, a Host header nobody checks. None of them fails loudly on its
/// own. A link to localhost is sent, a mail with no relay goes to the log,
/// and the user only sees that nothing arrived. So in Production the API
/// refuses to start instead, naming every key that is wrong at once, rather
/// than one per restart. Development is left as it is.
/// </summary>
public static class ProductionConfiguration
{
    public static void EnsureValid(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        var problems = Problems(configuration);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "Production configuration is incomplete:" + Environment.NewLine
                + string.Join(Environment.NewLine, problems.Select(problem => "- " + problem)));
        }
    }

    /// <summary>Each problem names the key, in the spelling the environment uses, and its .env variable.</summary>
    public static IReadOnlyList<string> Problems(IConfiguration configuration)
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Postgres")))
        {
            problems.Add("ConnectionStrings__Postgres (DATABASE_URL) is empty.");
        }

        if (!IsPublicHttps(configuration["EmailVerification:FrontendBaseUrl"]))
        {
            problems.Add(
                "EmailVerification__FrontendBaseUrl (FRONTEND_BASE_URL) has to be the public https address "
                + "of the frontend: verification and password reset links are built from it.");
        }

        var origins = (configuration["Cors:Origins"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (origins.Length == 0 || !origins.All(origin => IsPublicHttps(origin) && IsBareOrigin(origin)))
        {
            problems.Add(
                "Cors__Origins (CORS_ORIGINS) has to list only public https origins, "
                + "without a path or a trailing slash.");
        }

        if (string.IsNullOrWhiteSpace(configuration[$"{SmtpOptions.Section}:Host"]))
        {
            problems.Add("Smtp__Host (SMTP_HOST) is empty, so no account mail would ever be sent.");
        }

        // Host filtering treats an empty list as "allow any host", the same as "*".
        var hosts = (configuration["AllowedHosts"] ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (hosts.Length == 0 || hosts.Contains("*"))
        {
            problems.Add("AllowedHosts (ALLOWED_HOSTS) has to name the public host names, not \"*\".");
        }

        return problems;
    }

    /// <summary>
    /// CORS compares the Origin header as text, only lowercased
    /// (CorsPolicyBuilder.GetNormalizedOrigin), and a browser sends it without
    /// a path. "https://konkursy.example.pl/" would never match, and every
    /// call from the frontend would fail with nothing in the API log.
    /// </summary>
    private static bool IsBareOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && string.Equals(uri.GetLeftPart(UriPartial.Authority), origin, StringComparison.OrdinalIgnoreCase);

    private static bool IsPublicHttps(string? address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var host = uri.IdnHost;
        var loopback = host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || (IPAddress.TryParse(host.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip));

        return !loopback;
    }
}
