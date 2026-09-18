using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Endpoints;

/// <summary>
/// The dates as they cross the API, and what a badly filled form gets told
/// (T-20).
///
/// The time half is a card criterion in its own right: terms are stored in
/// UTC, confirmed by a test. It matters here rather than only in the schema
/// tests because the operator types a local time, and the conversion is what
/// the deadline rule in T-21 will stand on.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class CompetitionTimeAndValidationTests
    : IClassFixture<OcwipWebApplicationFactory>
{
    private readonly OcwipWebApplicationFactory _factory;
    private readonly PostgresDatabaseFixture _database;

    public CompetitionTimeAndValidationTests(
        OcwipWebApplicationFactory factory,
        PostgresDatabaseFixture database)
    {
        _factory = factory;
        _database = database;
    }

    private (WebApplicationFactory<Program> Host, FixedTimeProvider Clock) Host() =>
        CompetitionTestHost.Create(_factory, _database);

    [RequiresDatabaseFact]
    public async Task A_local_time_typed_by_an_operator_is_stored_in_utc()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        // 12:00 in Poland during summer time, which is 10:00 UTC. The seconds
        // are there because an operator pasting a value is not going to strip
        // them, and the whole minute rule has to survive that.
        var localStart = new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.FromHours(2));
        var localEnd = new DateTimeOffset(2026, 9, 30, 12, 0, 59, TimeSpan.FromHours(2));

        var request = CompetitionTestHost.Request() with
        {
            StartDate = localStart,
            EndDate = localEnd,
        };

        // Act
        var created = await CompetitionTestHost.CreateAsync(client, request);

        // Assert
        await using var context = _database.CreateContext();
        var stored = await context.Competitions
            .AsNoTracking()
            .SingleAsync(x => x.Id == created.Id);

        // The instant is preserved, the offset is not: 12:00+02:00 is 10:00Z,
        // and everything downstream compares instants.
        Assert.Equal(TimeSpan.Zero, stored.StartDate.Offset);
        Assert.Equal(8, stored.StartDate.Hour);
        Assert.Equal(localStart.UtcDateTime, stored.StartDate.UtcDateTime);

        // And the seconds are gone, because a competition closing at 12:00
        // closes at 12:00:00 and not at 12:00:59 (D7).
        Assert.Equal(TimeSpan.Zero, stored.EndDate!.Value.Offset);
        Assert.Equal(0, stored.EndDate.Value.Second);
        Assert.Equal(10, stored.EndDate.Value.Hour);
    }

    [RequiresDatabaseFact]
    public async Task A_window_that_collapses_to_one_minute_is_refused_with_a_message()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var request = CompetitionTestHost.Request() with
        {
            StartDate = new DateTimeOffset(2026, 9, 1, 12, 0, 30, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 9, 1, 12, 0, 45, TimeSpan.Zero),
        };

        // Act
        var response = await client.PostAsJsonAsync("/competitions", request);

        // Assert
        // Fifteen seconds apart looks like a window and is not one: both ends
        // truncate to 12:00. Validated after truncation, so the answer is a
        // message about the closing date instead of the 500 the check
        // constraint would produce.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("endDate", await response.Content.ReadAsStringAsync());
    }

    [RequiresDatabaseFact]
    public async Task A_continuous_intake_carrying_a_closing_date_is_refused_by_name()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var request = CompetitionTestHost.Request() with
        {
            IsContinuousIntake = true,
        };

        // Act
        var response = await client.PostAsJsonAsync("/competitions", request);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("endDate", body);
    }

    [RequiresDatabaseFact]
    public async Task A_fixed_term_competition_without_a_closing_date_is_refused_by_name()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var request = CompetitionTestHost.Request() with
        {
            EndDate = null,
        };

        // Act
        var response = await client.PostAsJsonAsync("/competitions", request);

        // Assert
        // The direction that would otherwise produce a competition nothing
        // ever stops accepting.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("endDate", await response.Content.ReadAsStringAsync());
    }

    [RequiresDatabaseFact]
    public async Task An_empty_title_and_a_zero_amount_are_named_field_by_field()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var request = CompetitionTestHost.Request() with
        {
            Number = "   ",
            Title = "   ",
            MaxGrantAmount = 0m,
        };

        // Act
        var response = await client.PostAsJsonAsync("/competitions", request);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        // Whitespace only, so this also pins the trimming: without it the
        // three spaces would be a perfectly acceptable title.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("number", body);
        Assert.Contains("title", body);
        Assert.Contains("maxGrantAmount", body);
    }

    [RequiresDatabaseFact]
    public async Task A_pasted_number_is_stored_without_the_spaces_around_it()
    {
        // Arrange
        var (host, _) = Host();
        var client = await CompetitionTestHost.SignedInAs(host, Role.Operator);

        var number = $"{Guid.NewGuid():N}";

        var request = CompetitionTestHost.Request() with
        {
            Number = $"  {number}  ",
            Title = "  Konkurs z nadmiarem spacji  ",
        };

        // Act
        var created = await CompetitionTestHost.CreateAsync(client, request);

        // Assert
        // Otherwise " 1/2026" and "1/2026" are two competitions the unique
        // index is happy with and no person can tell apart.
        Assert.Equal(number, created.Number);
        Assert.Equal("Konkurs z nadmiarem spacji", created.Title);
    }
}
