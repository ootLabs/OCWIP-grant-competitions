using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// Attachment invariants on a real PostgreSQL: a size that is a size, one row
/// per stored file, and an application that cannot vanish from under its files.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AttachmentDatabaseTests
{
    private readonly PostgresDatabaseFixture _database;

    public AttachmentDatabaseTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    private async Task<Guid> SeedApplicationAsync(string label)
    {
        var chain = await TestApplicationChain.SeedAsync(_database, label);

        await using var context = _database.CreateContext();
        var application = TestApplication.Draft(chain);
        context.Applications.Add(application);
        await context.SaveChangesAsync();
        return application.Id;
    }

    [RequiresDatabaseTheory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task An_attachment_whose_size_is_not_positive_is_refused(long size)
    {
        // Arrange
        // A zero byte attachment is a failed upload, not a document, and the
        // operator opening it later has no way to tell the difference.
        var applicationId = await SeedApplicationAsync($"z zalacznikiem {size}");

        await using var context = _database.CreateContext();
        context.Attachments.Add(
            TestAttachment.New(applicationId, sizeInBytes: size));

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.CheckViolation, postgres.SqlState);
        Assert.Equal("ck_attachments_size_in_bytes_positive", postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task Two_attachments_on_one_storage_path_are_refused()
    {
        // Arrange
        // Two rows pointing at one blob turn deleting a file into a way of
        // breaking a different application's attachment, and nothing in the
        // interface would show why.
        const string path = "applications/wspolna-sciezka";
        var applicationId = await SeedApplicationAsync("ze wspolna sciezka");

        await using (var first = _database.CreateContext())
        {
            first.Attachments.Add(TestAttachment.New(applicationId, path));
            await first.SaveChangesAsync();
        }

        await using var second = _database.CreateContext();
        second.Attachments.Add(TestAttachment.New(applicationId, path));

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => second.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.UniqueViolation, postgres.SqlState);
        Assert.Equal("ix_attachments_storage_path", postgres.ConstraintName);
    }

    [RequiresDatabaseFact]
    public async Task Several_attachments_on_one_application_are_allowed()
    {
        // Arrange
        // The requiredness of attachments follows the competition
        // configuration, so one offer routinely carries several files.
        var applicationId = await SeedApplicationAsync("z kilkoma plikami");

        await using var context = _database.CreateContext();
        context.Attachments.Add(TestAttachment.New(applicationId));
        context.Attachments.Add(TestAttachment.New(applicationId));

        // Act
        await context.SaveChangesAsync();

        // Assert
        Assert.Equal(
            2,
            await context.Attachments.CountAsync(x => x.ApplicationId == applicationId));
    }

    [RequiresDatabaseFact]
    public async Task Deleting_an_application_that_has_attachments_is_refused()
    {
        // Arrange
        // docs/model-danych.md rule 1. The files are part of the documentation
        // OCWIP has to keep for at least 5 years.
        var applicationId = await SeedApplicationAsync("do usuniecia z plikami");

        await using (var seed = _database.CreateContext())
        {
            seed.Attachments.Add(TestAttachment.New(applicationId));
            await seed.SaveChangesAsync();
        }

        await using var context = _database.CreateContext();
        var application = await context.Applications
            .SingleAsync(x => x.Id == applicationId);
        context.Applications.Remove(application);

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        Assert.Equal(
            PostgresAssert.ForeignKeyViolation,
            PostgresAssert.Error(exception).SqlState);
    }

    [RequiresDatabaseFact]
    public async Task An_attachment_pointing_at_no_application_is_refused()
    {
        // Arrange
        await using var context = _database.CreateContext();
        context.Attachments.Add(TestAttachment.New(Guid.NewGuid()));

        // Act
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        // Assert
        var postgres = PostgresAssert.Error(exception);
        Assert.Equal(PostgresAssert.ForeignKeyViolation, postgres.SqlState);
        Assert.Equal(
            "fk_attachments_applications_application_id",
            postgres.ConstraintName);
    }
}
