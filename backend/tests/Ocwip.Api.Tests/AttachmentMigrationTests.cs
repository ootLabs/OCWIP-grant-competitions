using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Ocwip.Api.Data;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests;

/// <summary>
/// What the T-32 migration does to attachment rows that already exist.
///
/// MigrationTests runs the chain against empty tables, so it cannot see that
/// a NOT NULL entity_id with an all zero default breaks its own foreign key
/// on the first row that is already there, or that an empty format cannot be
/// read back as AllowedFileFormat.
/// </summary>
[Collection(Data.PostgresCollection.Name)]
public class AttachmentMigrationTests
{
    /// <summary>The last migration before attachments carried entity_id and format.</summary>
    private const string BeforeAttachmentOwner = "20260918094645_AddCompetitionWizardParameters";

    [RequiresDatabaseFact]
    public async Task An_attachment_from_before_the_migration_gets_its_owner_and_format()
    {
        // Arrange
        await using var database = await ThrowawayDatabase.CreateAsync("mig_att");
        await using var context = CreateContext(database);
        var (attachmentId, entityId) = await SeedOlderAttachmentAsync(
            context, "application/vnd.oasis.opendocument.text", "sprawozdanie.odt");

        // Act
        await context.Database.MigrateAsync();

        // Assert
        var row = await context.Database
            .SqlQuery<AttachmentRow>(
                $"""
                SELECT entity_id, format
                  FROM attachments WHERE id = {attachmentId}
                """)
            .SingleAsync();
        Assert.Equal(entityId, row.EntityId);
        Assert.Equal("Odt", row.Format);
    }

    [RequiresDatabaseFact]
    public async Task The_format_falls_back_to_the_file_name_when_the_type_is_not_ours()
    {
        // Arrange
        await using var database = await ThrowawayDatabase.CreateAsync("mig_att");
        await using var context = CreateContext(database);
        var (attachmentId, _) = await SeedOlderAttachmentAsync(
            context, "application/octet-stream", "Budzet.XLSX");

        // Act
        await context.Database.MigrateAsync();

        // Assert
        var format = await context.Database
            .SqlQuery<string>(
                $"""SELECT format AS "Value" FROM attachments WHERE id = {attachmentId}""")
            .SingleAsync();
        Assert.Equal("Xlsx", format);
    }

    [RequiresDatabaseFact]
    public async Task A_row_whose_format_cannot_be_told_stops_the_migration()
    {
        // Arrange
        // Guessing a format would hand the download endpoint a content type
        // nobody checked. The migration refuses instead, and as one
        // transaction it leaves the table as it found it.
        await using var database = await ThrowawayDatabase.CreateAsync("mig_att");
        await using var context = CreateContext(database);
        await SeedOlderAttachmentAsync(context, "application/octet-stream", "plik");

        // Act
        var exception = await Record.ExceptionAsync(() => context.Database.MigrateAsync());

        // Assert
        Assert.NotNull(exception);
        var postgres = Assert.IsType<PostgresException>(exception.GetBaseException());
        Assert.Equal(PostgresAssert.RaisedException, postgres.SqlState);
        Assert.Contains(BeforeAttachmentOwner, await context.Database.GetAppliedMigrationsAsync());
        Assert.DoesNotContain(
            "20260924095655_AddAttachmentEntityIdAndFormat",
            await context.Database.GetAppliedMigrationsAsync());
    }

    /// <summary>
    /// Writes the row through the current model and then rolls the chain back
    /// past the migration: that leaves exactly the row an older database
    /// holds, without copying the old schema into raw SQL.
    /// </summary>
    private static async Task<(Guid AttachmentId, Guid EntityId)> SeedOlderAttachmentAsync(
        AppDbContext context,
        string contentType,
        string fileName)
    {
        await context.Database.MigrateAsync();

        var competition = TestCompetition.New("Konkurs z załącznikiem");
        var entity = TestEntity.New("Podmiot z załącznikiem");
        context.Competitions.Add(competition);
        context.Entities.Add(entity);
        await context.SaveChangesAsync();

        var definition = TestApplicationChain.NewFormDefinition(competition.Id);
        context.FormDefinitions.Add(definition);
        await context.SaveChangesAsync();

        var application = TestApplication.Draft(
            new ApplicationChain(competition.Id, definition.Id, entity.Id));
        context.Applications.Add(application);
        await context.SaveChangesAsync();

        var attachment = TestAttachment.New(application.Id, entity.Id);
        attachment.ContentType = contentType;
        attachment.FileName = fileName;
        context.Attachments.Add(attachment);
        await context.SaveChangesAsync();

        await context.GetService<IMigrator>().MigrateAsync(BeforeAttachmentOwner);
        context.ChangeTracker.Clear();

        return (attachment.Id, entity.Id);
    }

    /// <summary>Read back through the snake case naming the context uses.</summary>
    private sealed record AttachmentRow(Guid EntityId, string Format);

    private static AppDbContext CreateContext(ThrowawayDatabase database)
    {
        // The same configuration the application and dotnet ef use.
        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseOcwipPostgres(database.ConnectionString);
        return new AppDbContext(options.Options);
    }
}
