using System.Net;
using Ocwip.Api.Configuration;
using Xunit;

namespace Ocwip.Api.Tests.Configuration;

/// <summary>
/// Who shares a request budget (S-03). One IPv4 address is one caller, but a
/// single customer is routinely handed a whole IPv6 /64 and often more, so
/// counting per full address let an attacker mint as many budgets as they
/// cared to and the limit on login, registration and password reset stopped
/// being a limit.
/// </summary>
public sealed class RateLimitPartitionTests
{
    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    public void An_ipv4_caller_is_its_own_address(string address, string expected) =>
        Assert.Equal(expected, RateLimitingConfiguration.Partition(IPAddress.Parse(address)));

    [Fact]
    public void Two_addresses_from_one_ipv6_network_share_a_budget()
    {
        var first = RateLimitingConfiguration.Partition(IPAddress.Parse("2001:db8:1:2::1"));
        var second = RateLimitingConfiguration.Partition(IPAddress.Parse("2001:db8:1:2:dead:beef:cafe:1"));

        Assert.Equal(first, second);
        Assert.Equal("2001:db8:1:2::/64", first);
    }

    [Fact]
    public void Two_different_ipv6_networks_do_not()
    {
        var first = RateLimitingConfiguration.Partition(IPAddress.Parse("2001:db8:1:2::1"));
        var second = RateLimitingConfiguration.Partition(IPAddress.Parse("2001:db8:1:3::1"));

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// A request with no remote address (a test host, most often) shares one
    /// partition rather than crashing the endpoint: a null key throws inside
    /// the limiter.
    /// </summary>
    [Fact]
    public void A_caller_without_an_address_still_gets_a_key() =>
        Assert.Equal("unknown", RateLimitingConfiguration.Partition(null));
}
