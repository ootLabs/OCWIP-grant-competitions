using System.Net;
using Microsoft.Extensions.Hosting;
using Ocwip.Api.Data.Encryption;

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

    /// <summary>
    /// The key appsettings.Development.json carries, repeated here so that
    /// Production can refuse it (S-21). Not a secret by any measure: that is
    /// the point.
    /// </summary>
    internal const string DevelopmentFieldKey = "okb/WMqWM3PUPCzEVtM12ftFCSFghUwGl0n3v/aZguA=";

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

        // S-15: SameSite is the barrier the session cookie itself provides,
        // and None takes it away. CrossSiteRequestFilter is the second
        // barrier, so a split site deployment is possible, but it has to say
        // so out loud rather than arrive as one line in .env.
        if (string.Equals(configuration["Auth:CookieSameSite"], "None", StringComparison.OrdinalIgnoreCase)
            && configuration.GetValue<bool?>("Auth:AllowCrossSiteCookie") is not true)
        {
            problems.Add(
                "Auth__CookieSameSite (AUTH_COOKIE_SAME_SITE) is None, which drops the cookie's own protection "
                + "against a form on somebody else's page. Set Auth__AllowCrossSiteCookie (AUTH_ALLOW_CROSS_SITE_COOKIE) "
                + "to true to say that the front and the API really are separate sites.");
        }

        if (string.IsNullOrWhiteSpace(configuration["DataProtection:KeysPath"]))
        {
            problems.Add(
                "DataProtection__KeysPath (DATA_PROTECTION_KEYS_PATH) is empty, so every restart "
                + "would sign everybody out and void every link in account mail.");
        }

        // S-21: the key the repository carries for Development protects
        // fictional data and is public to anyone who can read the repository.
        // Named here so that copying it into .env.prod, which is the obvious
        // thing to do when a start fails for want of a key, stops the start
        // instead of encrypting a PESEL with a published key.
        if (configuration.AsEnumerable()
            .Any(entry => entry.Key.StartsWith("FieldEncryption:Keys:", StringComparison.Ordinal)
                && entry.Value == DevelopmentFieldKey))
        {
            problems.Add(
                "FieldEncryption__Keys (FIELD_ENCRYPTION_KEY) is the key appsettings.Development.json carries, "
                + "which is in the repository and protects nothing. Generate one: openssl rand -base64 32.");
        }

        // T-47a: without a key every write of a sensitive field would fail.
        try
        {
            if (FieldEncryption.Read(configuration) is null)
            {
                problems.Add(
                    "FieldEncryption__Keys__1 (FIELD_ENCRYPTION_KEY) is empty, so no sensitive "
                    + "field could be written or read.");
            }
        }
        catch (InvalidOperationException exception)
        {
            problems.Add(exception.Message);
        }

        if (string.IsNullOrWhiteSpace(configuration[$"{SmtpOptions.Section}:Host"]))
        {
            problems.Add("Smtp__Host (SMTP_HOST) is empty, so no account mail would ever be sent.");
        }

        // Without the key the account forms are open to any script that
        // stays under the rate limit, from as many addresses as it has.
        if (string.IsNullOrWhiteSpace(configuration["Turnstile:SecretKey"]))
        {
            problems.Add(
                "Turnstile__SecretKey (TURNSTILE_SECRET_KEY) is empty, so the sign in and registration "
                + "forms would take requests from scripts. Create a widget in the Cloudflare dashboard.");
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
