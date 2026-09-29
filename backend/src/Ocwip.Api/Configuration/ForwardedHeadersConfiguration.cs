using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Ocwip.Api.Configuration;

/// <summary>
/// The client's address behind the reverse proxy (T-111). The login limiter
/// partitions by address (RateLimitingConfiguration), so behind Caddy every
/// request would otherwise come from the proxy and everybody would share one
/// limit, or, with every sender trusted, an attacker would pick a new address
/// per try by writing X-Forwarded-For.
///
/// Only the proxies and networks named in ForwardedHeaders:KnownProxies and
/// ForwardedHeaders:KnownNetworks are believed, and only one hop back.
/// Deliberately not ASPNETCORE_FORWARDEDHEADERS_ENABLED: that switch clears
/// the list of trusted senders and believes anybody. Nothing configured means
/// nothing is believed and the connection's own address counts, which is
/// right for an API reached directly (the development compose file).
/// </summary>
public static class ForwardedHeadersConfiguration
{
    public const string Section = "ForwardedHeaders";

    public static ForwardedHeadersOptions? Options(IConfiguration configuration)
    {
        var proxies = Values(configuration, "KnownProxies")
            .Select(value => IPAddress.TryParse(value, out var address)
                ? address
                : throw new InvalidOperationException($"{Section}:KnownProxies has \"{value}\", which is not an IP address."))
            .ToList();
        var networks = Values(configuration, "KnownNetworks")
            .Select(value => System.Net.IPNetwork.TryParse(value, out var network)
                ? network
                : throw new InvalidOperationException($"{Section}:KnownNetworks has \"{value}\", which is not a network such as 172.30.0.0/24."))
            .ToList();

        if (proxies.Count == 0 && networks.Count == 0)
        {
            return null;
        }

        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 1,
        };

        // The defaults trust loopback, which inside a container is not the proxy.
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        proxies.ForEach(options.KnownProxies.Add);
        networks.ForEach(options.KnownIPNetworks.Add);
        return options;
    }

    /// <summary>A list either as array entries (KnownProxies:0) or as one comma separated value.</summary>
    private static IEnumerable<string> Values(IConfiguration configuration, string key)
    {
        var section = configuration.GetSection($"{Section}:{key}");
        var values = section.Value is { Length: > 0 } joined
            ? joined.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : section.GetChildren().Select(child => child.Value ?? string.Empty);
        return values.Where(value => value.Length > 0);
    }
}
