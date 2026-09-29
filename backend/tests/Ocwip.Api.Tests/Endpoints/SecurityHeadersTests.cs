using System.Net;
using Ocwip.Api.Configuration;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>T-112: every answer of the API carries the security headers, errors included.</summary>
public sealed class SecurityHeadersTests(OcwipWebApplicationFactory factory) : IClassFixture<OcwipWebApplicationFactory>
{
    [Theory]
    [InlineData("/health", HttpStatusCode.OK)]
    [InlineData("/no-such-path", HttpStatusCode.NotFound)]
    [InlineData("/applications", HttpStatusCode.Unauthorized)]
    public async Task Every_answer_carries_the_headers(string path, HttpStatusCode status)
    {
        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Equal(SecurityHeaders.ContentSecurityPolicy, Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
    }
}
