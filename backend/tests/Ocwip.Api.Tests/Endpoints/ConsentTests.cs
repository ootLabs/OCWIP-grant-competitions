using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Services.Consents;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// Consents at registration (T-107, R-19): both documents in force must be
/// accepted, and the acceptance keeps the full text seen and the moment.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ConsentTests(OcwipWebApplicationFactory factory, PostgresDatabaseFixture database)
    : IClassFixture<OcwipWebApplicationFactory>
{
    private HttpClient Client() =>
        SessionTestHost.Create(factory, database, settings: new Dictionary<string, string?> { ["RateLimiting:PermitLimit"] = "200" })
            .CreateClient();

    [RequiresDatabaseFact]
    public async Task The_documents_are_public_with_the_version_to_send_back()
    {
        var documents = (await Client().GetFromJsonAsync<List<ConsentDocument>>("/public/consents"))!;

        Assert.Equal([ConsentCatalog.Terms, ConsentCatalog.Privacy], documents.Select(x => x.Kind));
        Assert.All(documents, x => Assert.Equal(16, x.Version.Length));
        Assert.Equal("Regulamin serwisu", documents[0].Title);
        Assert.Equal(TestConsents.All, documents.Select(x => x.Version));
    }

    [RequiresDatabaseFact]
    public async Task Registration_needs_both_documents_and_keeps_the_full_text_and_the_moment()
    {
        var client = Client();
        var email = SessionTestHost.Email("zgody");

        var without = await client.PostAsJsonAsync("/register",
            new RegisterRequest(email, SessionTestHost.Password, "Ada", "Testowa", AcceptedConsents: [TestConsents.All[0]]));
        Assert.Equal(HttpStatusCode.BadRequest, without.StatusCode);
        var body = await without.Content.ReadAsStringAsync();
        Assert.Contains("acceptedConsents", body);
        Assert.Contains("Klauzula informacyjna", body);

        var accepted = await client.PostAsJsonAsync("/register",
            new RegisterRequest(email, SessionTestHost.Password, "Ada", "Testowa", AcceptedConsents: TestConsents.All));
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);

        await using var context = database.CreateContext();
        var userId = await context.Users.Where(x => x.Email == email).Select(x => x.Id).SingleAsync();
        var rows = await context.ConsentAcceptances.AsNoTracking().Where(x => x.UserId == userId).OrderBy(x => x.Kind).ToListAsync();
        var documents = new ConsentCatalog(new ConfigurationBuilder().Build()).Current;

        Assert.Equal([ConsentCatalog.Privacy, ConsentCatalog.Terms], rows.Select(x => x.Kind));
        Assert.All(rows, row =>
        {
            var document = documents.Single(x => x.Kind == row.Kind);
            Assert.Equal(document.Text, row.Text);
            Assert.Equal(document.Version, row.Version);
            Assert.True(DateTimeOffset.UtcNow - row.AcceptedAt < TimeSpan.FromMinutes(1));
        });
    }

    [RequiresDatabaseFact]
    public async Task A_version_that_is_not_in_force_counts_as_not_accepted()
    {
        var response = await Client().PostAsJsonAsync("/register",
            new RegisterRequest(SessionTestHost.Email("stara"), SessionTestHost.Password, "Ada", "Testowa",
                AcceptedConsents: ["0000000000000000", TestConsents.All[1]]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Regulamin serwisu", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void A_changed_text_is_a_new_version()
    {
        var directory = Directory.CreateTempSubdirectory();
        File.WriteAllText(Path.Combine(directory.FullName, "terms.md"), "# Regulamin\n\nPierwsza treść.");
        File.WriteAllText(Path.Combine(directory.FullName, "privacy.md"), "# Klauzula\n\nTreść.");
        var settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Consents:Directory"] = directory.FullName })
            .Build();

        var before = new ConsentCatalog(settings).Current[0].Version;
        File.WriteAllText(Path.Combine(directory.FullName, "terms.md"), "# Regulamin\n\nDruga treść.");
        var after = new ConsentCatalog(settings).Current[0].Version;

        Assert.NotEqual(before, after);
        Assert.Equal("Regulamin", new ConsentCatalog(settings).Current[0].Title);
    }
}
