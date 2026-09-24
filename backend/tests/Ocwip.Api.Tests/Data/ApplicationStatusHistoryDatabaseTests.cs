using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// What the schema refuses to store in the append-only status history (T-33).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ApplicationStatusHistoryDatabaseTests
{
    private readonly PostgresDatabaseFixture _database;

    public ApplicationStatusHistoryDatabaseTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    [RequiresDatabaseFact]
    public async Task A_transition_that_does_not_change_the_status_is_refused()
    {
        // Arrange
        var chain = await TestApplicationChain.SeedAsync(_database, "historia bez zmiany");

        await using var context = _database.CreateContext();
        var application = TestApplication.Submitted(chain, number: "001");
        context.Applications.Add(application);

        var user = TestUser.New("operator-historia@example.org");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        context.ApplicationStatusHistory.Add(new ApplicationStatusHistory
        {
            ApplicationId = application.Id,
            FromStatus = ApplicationStatus.Submitted,
            ToStatus = ApplicationStatus.Submitted,
            ChangedAt = DateTimeOffset.UtcNow,
            ChangedByUserId = user.Id,
        });

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.CheckViolation, postgres.SqlState);
        Assert.Equal(
            "ck_application_status_history_from_ne_to", postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task A_real_transition_is_appended_and_keeps_the_application_it_belongs_to()
    {
        // Arrange
        var chain = await TestApplicationChain.SeedAsync(_database, "historia prawdziwa");

        await using var context = _database.CreateContext();
        var application = TestApplication.Submitted(chain, number: "002");
        context.Applications.Add(application);

        var user = TestUser.New("wnioskodawca-historia@example.org");
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var entry = new ApplicationStatusHistory
        {
            ApplicationId = application.Id,
            FromStatus = ApplicationStatus.Draft,
            ToStatus = ApplicationStatus.Submitted,
            ChangedAt = DateTimeOffset.UtcNow,
            ChangedByUserId = user.Id,
        };
        context.ApplicationStatusHistory.Add(entry);

        // Act
        await context.SaveChangesAsync();

        // Assert: an append, never an update to Application itself, and never
        // overwriting a previous entry (only one exists here, but the point is
        // that writing this one did not touch the application row's own
        // history in any way other than adding to it).
        await using var reread = _database.CreateContext();
        var stored = await reread.ApplicationStatusHistory
            .AsNoTracking()
            .SingleAsync(x => x.Id == entry.Id);

        Assert.Equal(application.Id, stored.ApplicationId);
        Assert.Equal(ApplicationStatus.Draft, stored.FromStatus);
        Assert.Equal(ApplicationStatus.Submitted, stored.ToStatus);
        Assert.Equal(user.Id, stored.ChangedByUserId);
    }
}
