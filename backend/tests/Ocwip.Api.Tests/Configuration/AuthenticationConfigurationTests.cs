using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Configuration;
using Xunit;

namespace Ocwip.Api.Tests.Configuration;

/// <summary>
/// Settings that are wrong in a way nothing else notices.
///
/// Both cases below produce a running application that answers 200 to a sign in
/// and then behaves as though nobody signed in, with no error anywhere. Failing
/// to start is the cheap version of that outage, so these tests are about the
/// application REFUSING to come up.
/// </summary>
public sealed class AuthenticationConfigurationTests
{
    private static IServiceCollection Configure(
        IDictionary<string, string?> settings,
        bool isDevelopment = false)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new ServiceCollection().AddOcwipAuthentication(
            configuration, isDevelopment, hasStore: false);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public void A_session_that_expires_on_arrival_stops_the_application(string hours)
    {
        // A zero lifetime issues a cookie that is already expired, so login
        // answers 200 and the very next request answers 401. One typo in .env
        // locks everybody out of the whole product.
        var failure = Assert.Throws<InvalidOperationException>(() => Configure(
            new Dictionary<string, string?>
            {
                ["Auth:SessionLifetimeHours"] = hours,
            }));

        Assert.Contains("Auth:SessionLifetimeHours", failure.Message);
    }

    [Fact]
    public void SameSite_none_without_secure_stops_the_application()
    {
        // Every current browser drops a SameSite=None cookie that is not
        // Secure, and says nothing about it. This is also the exact pair
        // .env.example documents for a split site deployment, so it is the
        // combination somebody will actually reach for.
        var failure = Assert.Throws<InvalidOperationException>(() => Configure(
            new Dictionary<string, string?>
            {
                ["Auth:CookieSameSite"] = "None",
                ["Auth:SecureCookie"] = "false",
            }));

        Assert.Contains("Auth:SecureCookie", failure.Message);
    }

    [Fact]
    public void SameSite_none_with_secure_is_accepted()
    {
        // The pair is legitimate: it is how the front and the API live on two
        // different sites. The guard must refuse the broken half only.
        Configure(new Dictionary<string, string?>
        {
            ["Auth:CookieSameSite"] = "None",
            ["Auth:SecureCookie"] = "true",
        });
    }

    [Fact]
    public void An_unset_secure_flag_follows_the_environment()
    {
        // The value docker-compose passes is EMPTY, not false, and this is the
        // test that keeps it that way: an explicit false in the compose file
        // would carry the local plain http exception into every deployment
        // built from it, including one running as Production.
        var settings = new Dictionary<string, string?>
        {
            ["Auth:SecureCookie"] = string.Empty,
            ["Auth:CookieSameSite"] = "None",
        };

        // Outside Development an unset flag means Secure, so pairing it with
        // SameSite=None is fine.
        Configure(settings);

        // In Development it means the opposite, and the same pair is refused.
        Assert.Throws<InvalidOperationException>(
            () => Configure(settings, isDevelopment: true));
    }
}
