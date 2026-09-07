using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        // 255 is what a filename can be on the file systems involved, so a
        // longer one could not have come from a real upload.
        builder.Property(x => x.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.ContentType)
            .IsRequired()
            .HasMaxLength(255)
            .HasComment(
                "MIME type as declared by the client. Declared, not verified: " +
                "whoever accepts the upload in T-32 owns checking that the " +
                "bytes match, because a client controlled value proves nothing.");

        builder.Property(x => x.SizeInBytes)
            .IsRequired();

        builder.Property(x => x.StoragePath)
            .IsRequired()
            .HasMaxLength(500)
            .HasComment(
                "Where the stored bytes live. Must not be guessable and must " +
                "not be reachable without the same permission check as the " +
                "application itself: an attachment is another organisation's " +
                "document. Physical storage is card T-32.");

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasComment(
                "False marks the row as deleted. Rows are never removed, " +
                "because retention is at least 5 years.");

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.DeactivatedAt)
            .HasColumnType("timestamp with time zone")
            .HasComment(
                "When the row was marked inactive, in UTC. " +
                "Null while the attachment is active.");

        builder.ToTable(table =>
        {
            // A zero byte attachment is a failed upload, not a document, and a
            // negative size is a bug in whatever wrote the row.
            table.HasCheckConstraint(
                "ck_attachments_size_in_bytes_positive",
                "size_in_bytes > 0");

            table.HasCheckConstraint(
                "ck_attachments_deactivated_at_matches_is_active",
                "is_active = (deactivated_at IS NULL)");
        });

        // Unique, because two rows pointing at one blob turn deleting a file
        // into a way of breaking a different application's attachment.
        builder.HasIndex(x => x.StoragePath)
            .IsUnique();

        // NoAction, not Cascade: docs/model-danych.md rule 1.
        builder.HasOne(x => x.Application)
            .WithMany(x => x.Attachments)
            .HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
