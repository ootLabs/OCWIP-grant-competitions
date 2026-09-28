using System.Net;
using System.Net.Http.Json;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// T-113: with the key ring on a shared directory, a session outlives the
/// container that issued it. Before, every new container generated its own
/// keys, signed everybody out and voided every link in account mail.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DataProtectionKeysTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    : IClassFixture<OcwipWebApplicationFactory>
{
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Host(string keys) =>
        SessionTestHost.Create(factory, database, new Dictionary<string, string?>
        {
            ["DataProtection:KeysPath"] = keys,
            ["RateLimiting:PermitLimit"] = "200",
        });

    private static async Task<string> SignInAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host, string email)
    {
        var client = SessionTestHost.RawClient(host);
        var login = await client.PostAsJsonAsync("/login", new { email, password = SessionTestHost.Password });
        login.EnsureSuccessStatusCode();
        return login.Headers.GetValues("Set-Cookie").Single(cookie => cookie.StartsWith("ocwip.session=", StringComparison.Ordinal)).Split(';')[0];
    }

    private static async Task<HttpStatusCode> MeAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host, string cookie)
    {
        var client = SessionTestHost.RawClient(host);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/me");
        request.Headers.Add("Cookie", cookie);
        return (await client.SendAsync(request)).StatusCode;
    }

    [RequiresDatabaseFact]
    public async Task A_session_survives_a_new_instance_reading_the_same_keys()
    {
        var keys = Directory.CreateTempSubdirectory("ocwip-keys-").FullName;
        using var first = Host(keys);
        var email = SessionTestHost.Email("klucze");
        await SessionTestHost.CreateAccountAsync(first, email, Role.Applicant);
        var cookie = await SignInAsync(first, email);

        // A second host is a second container: its own DI, its own key ring,
        // reading the same directory.
        using var restarted = Host(keys);
        using var elsewhere = Host(Directory.CreateTempSubdirectory("ocwip-keys-").FullName);

        Assert.Equal(HttpStatusCode.OK, await MeAsync(restarted, cookie));
        // The control: keys of its own and the same cookie is worth nothing.
        Assert.Equal(HttpStatusCode.Unauthorized, await MeAsync(elsewhere, cookie));
    }
}
