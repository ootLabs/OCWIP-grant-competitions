using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// Shared setup for the sign in tests: a host pointed at the throwaway database
/// and a way to put an account into it in a known state.
///
/// Accounts are created through UserManager rather than through /register,
/// because these tests need a confirmed one, an unconfirmed one, a deactivated
/// one and an operator, and only the second of those is a thing registration
/// can produce (a role is never granted over HTTP, see Models/Role.cs).
/// </summary>
internal static class SessionTestHost
{
    /// <summary>Meets every rule in IdentityConfiguration.</summary>
    public const string Password = "Str0ng!Passw0rd";

    public static string Email(string label) =>
        $"{label}-{Guid.NewGuid():N}@example.org";

    public static WebApplicationFactory<Program> Create(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database,
        IDictionary<string, string?>? settings = null,
        Action<IServiceCollection>? services = null) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", database.ConnectionString);

            foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
            {
                builder.UseSetting(key, value);
            }

            if (services is not null)
            {
                builder.ConfigureServices(services);
            }
        });

    /// <summary>
    /// A client that does NOT keep cookies for the caller. Every test that
    /// checks what the cookie is worth has to hold it by hand: the point of
    /// half of them is to replay a cookie the browser would have thrown away.
    /// </summary>
    public static HttpClient RawClient(WebApplicationFactory<Program> host) =>
        host.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
        });

    public static async Task<User> CreateAccountAsync(
        WebApplicationFactory<Program> host,
        string email,
        Role role = Role.Applicant,
        bool confirmed = true,
        bool active = true,
        string firstName = "Ada",
        string lastName = "Testowa",
        // Null for every caller except the application draft tests (T-29):
        // every other test predates B-09, and an account with no Podmiot is
        // the state every real account is in today.
        Guid? entityId = null)
    {
        using var scope = host.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var user = new User
        {
            Email = email,
            UserName = email,
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            EmailConfirmed = confirmed,
            IsActive = active,
            DeactivatedAt = active ? null : DateTimeOffset.UtcNow,
            EntityId = entityId,
        };

        var created = await users.CreateAsync(user, Password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                "Test account was not created: "
                + string.Join(", ", created.Errors.Select(error => error.Description)));
        }

        return user;
    }

    /// <summary>
    /// The session cookie exactly as the browser would store and resend it,
    /// taken off the Set-Cookie header of a sign in response.
    /// </summary>
    public static string SessionCookie(HttpResponseMessage response)
    {
        var header = response.Headers
            .GetValues("Set-Cookie")
            .Single(value => value.StartsWith(
                Ocwip.Api.Configuration.AuthenticationConfiguration.CookieName,
                StringComparison.Ordinal));

        return header.Split(';')[0];
    }

    public static HttpRequestMessage WithCookie(
        HttpMethod method,
        string path,
        string cookie)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Cookie", cookie);
        return request;
    }
}
