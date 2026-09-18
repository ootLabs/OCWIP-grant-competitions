using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class CompetitionAttachmentConfiguration
    : IEntityTypeConfiguration<CompetitionAttachment>
{
    /// <summary>
    /// Column widths, repeated by CompetitionRequestValidator so a too long
    /// title comes back naming the field instead of as a 500.
    /// </summary>
    public const int TitleLength = 200;

    public const int DescriptionLength = 2000;

    public void Configure(EntityTypeBuilder<CompetitionAttachment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(TitleLength);

        builder.Property(x => x.Description)
            .HasMaxLength(DescriptionLength);

        // Text, not the ordinal, for the reason written on CompetitionStatus:
        // inserting a member would otherwise reinterpret every stored row.
        builder.Property(x => x.Requirement)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        // A text[] rather than a child table of its own. The set is at most
        // eight values, is always read whole together with the attachment and
        // is never joined against, so a table would buy a join and nothing
        // else. Converted member by member, so the column holds "Pdf" and not
        // the digit an enum array would store.
        builder.Property(x => x.AllowedFormats)
            .IsRequired()
            .HasConversion(
                formats => formats.Select(format => format.ToString()).ToArray(),
                values => values
                    .Select(Enum.Parse<AllowedFileFormat>)
                    .ToList(),
                new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<
                    IReadOnlyList<AllowedFileFormat>>(
                    (left, right) => left!.SequenceEqual(right!),
                    formats => formats.Aggregate(
                        0,
                        (hash, format) => HashCode.Combine(hash, format.GetHashCode())),
                    formats => formats.ToList()))
            .HasColumnType("text[]");

        builder.Property(x => x.Position)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        builder.ToTable(table =>
        {
            // An attachment nobody may hand in is a row that only confuses the
            // applicant, and an empty array is what a client sending "[]"
            // produces by accident.
            table.HasCheckConstraint(
                "ck_competition_attachments_allowed_formats_not_empty",
                "cardinality(allowed_formats) > 0");

            table.HasCheckConstraint(
                "ck_competition_attachments_position_not_negative",
                "position >= 0");
        });

        // Ordering is per competition, so the index is too.
        builder.HasIndex(x => new
        {
            x.CompetitionId,
            x.Position
        });

        // Cascade on purpose, unlike everywhere else (docs/model-danych.md
        // rule 1 forbids it for rows that are somebody's record). This one is
        // not a record: it is a line of the competition's own settings, it has
        // no meaning away from it, and the competition itself is never deleted,
        // only marked inactive. Without it, replacing the attachment list on an
        // edit would have to null the key of rows nothing may keep.
        builder.HasOne(x => x.Competition)
            .WithMany(x => x.Attachments)
            .HasForeignKey(x => x.CompetitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
