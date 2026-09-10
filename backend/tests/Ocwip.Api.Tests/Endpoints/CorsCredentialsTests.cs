using System.Net.Http;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// The CORS half of the session contract (T-17).
///
/// The session travels in an HttpOnly cookie, and a browser only sends one
/// cross origin when the API answers the preflight with
/// Access-Control-Allow-Credentials. Signing in is T-12.3, so what is verified
/// here is the envelope: that the cookie is allowed to travel at all, and only
/// from an origin we listed.
/// </summary>
public sealed class CorsCredentialsTests : IClassFixture<OcwipWebApplicationFactory>
{
    private const string AllowedOrigin = "http://localhost:3000";

    private readonly OcwipWebApplicationFactory _factory;

    public CorsCredentialsTests(OcwipWebApplicationFactory factory) => _factory = factory;

    // Allowed origins come from configuration, so the test states its own
    // instead of inheriting whatever .env or CI happens to set.
    private HttpClient Client() =>
        _factory
            .WithWebHostBuilder(builder => builder.UseSetting("Cors:Origins", AllowedOrigin))
            .CreateClient();

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/register");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        return request;
    }

    [Fact]
    public async Task The_session_cookie_may_travel_from_the_frontend_origin()
    {
        var response = await Client().SendAsync(Preflight(AllowedOrigin));

        Assert.Equal(
            "true",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
        Assert.Equal(
            AllowedOrigin,
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task An_origin_we_did_not_list_gets_no_permission_at_all()
    {
        var response = await Client().SendAsync(Preflight("https://zlosliwy.example"));

        // A missing header is how CORS says no: the browser blocks the answer.
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }
}
