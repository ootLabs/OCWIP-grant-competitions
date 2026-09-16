using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Ocwip.Api.Contracts;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// The IP half of brute force protection (T-12.5): a series of requests from
/// one address to a sensitive endpoint gets cut off, independently of
/// whether any one of those requests is against a real account. Every test
/// here sets a small PermitLimit rather than sending the real default's
/// worth of requests, so the suite stays fast and the limit being tested is
/// explicit in the test itself.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RateLimitingTests : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public RateLimitingTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    private WebApplicationFactory<Program> Host(int permitLimit) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(
                "ConnectionStrings:Postgres", _database.ConnectionString!);
            builder.UseSetting("RateLimiting:PermitLimit", permitLimit.ToString());
            builder.UseSetting("RateLimiting:WindowSeconds", "60");
        });

    [RequiresDatabaseFact]
    public async Task Login_is_refused_after_the_permit_limit_from_one_address()
    {
        // Arrange
        // Every request in this test comes from the same TestServer client,
        // which the rate limiter partitions the same way it would partition
        // requests sharing one real IP address.
        var client = Host(permitLimit: 3).CreateClient();

        // Act
        HttpResponseMessage? last = null;
        for (var i = 0; i < 4; i++)
        {
            last = await client.PostAsJsonAsync(
                "/login", new { email = "nikt@example.org", password = "cokolwiek" });
        }

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.True(last.Headers.RetryAfter is not null);
    }

    [RequiresDatabaseFact]
    public async Task Register_is_refused_after_the_permit_limit_from_one_address()
    {
        // Arrange
        var client = Host(permitLimit: 3).CreateClient();

        // Act
        HttpResponseMessage? last = null;
        for (var i = 0; i < 4; i++)
        {
            last = await client.PostAsJsonAsync(
                "/register",
                new RegisterRequest(
                    $"rl-{Guid.NewGuid():N}@example.org", "Tajne-Haslo1", "Ada", "Testowa"));
        }

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Forgot_password_is_refused_after_the_permit_limit_from_one_address()
    {
        // Arrange
        // The endpoint the card names explicitly: sending mail without a
        // limit is a free tool for flooding an inbox.
        var client = Host(permitLimit: 3).CreateClient();

        // Act
        HttpResponseMessage? last = null;
        for (var i = 0; i < 4; i++)
        {
            last = await client.PostAsJsonAsync(
                "/forgot-password", new ForgotPasswordRequest("ktos@example.org"));
        }

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Resend_verification_is_refused_after_the_permit_limit_from_one_address()
    {
        // Arrange
        var client = Host(permitLimit: 3).CreateClient();

        // Act
        HttpResponseMessage? last = null;
        for (var i = 0; i < 4; i++)
        {
            last = await client.PostAsJsonAsync(
                "/resend-verification",
                new ResendVerificationRequest("ktos@example.org"));
        }

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task Reset_password_is_refused_after_the_permit_limit_from_one_address()
    {
        // Arrange
        // The token itself is not practically guessable, so this endpoint is
        // covered for consistency rather than because it is the weak one:
        // "reset hasła" in the card means the whole path, not only the mail
        // that starts it.
        var client = Host(permitLimit: 3).CreateClient();

        // Act
        HttpResponseMessage? last = null;
        for (var i = 0; i < 4; i++)
        {
            last = await client.PostAsJsonAsync(
                "/reset-password",
                new ResetPasswordRequest(
                    Guid.NewGuid().ToString(), "cokolwiek", "Tajne-Haslo1"));
        }

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }

    [RequiresDatabaseFact]
    public async Task The_budget_is_shared_across_every_sensitive_endpoint()
    {
        // Arrange
        // One policy, one partition key (the address), used by all four
        // endpoints: a caller cannot dodge the limit by mixing routes,
        // because there is only ever one counter per address, not one per
        // (address, endpoint) pair.
        var client = Host(permitLimit: 2).CreateClient();

        // Act
        var first = await client.PostAsJsonAsync(
            "/login", new { email = "nikt@example.org", password = "cokolwiek" });
        var second = await client.PostAsJsonAsync(
            "/register",
            new RegisterRequest(
                $"rl-{Guid.NewGuid():N}@example.org", "Tajne-Haslo1", "Ada", "Testowa"));
        var third = await client.PostAsJsonAsync(
            "/forgot-password", new ForgotPasswordRequest("ktos@example.org"));

        // Assert
        Assert.NotEqual(HttpStatusCode.TooManyRequests, first.StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }
}
