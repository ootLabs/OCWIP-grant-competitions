using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Data.Encryption;
using Ocwip.Api.Models;
using Ocwip.Api.Services.EntityCards;

namespace Ocwip.Api.Data.Configurations;

public sealed class ApplicationReturnConfiguration : IEntityTypeConfiguration<ApplicationReturn>
{
    public const int MessageMaxLength = 2000;

    public void Configure(EntityTypeBuilder<ApplicationReturn> builder)
    {
        builder.ToTable("application_returns", table =>
        {
            table.HasComment("Returns of a submitted application for correction (T-103): sections, note, deadline.");
            table.HasCheckConstraint("ck_application_returns_sections_not_empty", "cardinality(sections) > 0");
            // A whole minute, like every deadline of a competition: the
            // correction closes at 14:00, not at 14:00:37.
            table.HasCheckConstraint(
                "ck_application_returns_deadline_whole_minute",
                "date_trunc('minute', deadline AT TIME ZONE 'UTC') = deadline AT TIME ZONE 'UTC'");
            table.HasCheckConstraint("ck_application_returns_resolved_after_return", "resolved_at IS NULL OR resolved_at >= returned_at");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.HasOne(x => x.Application).WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ReturnedByUserId).OnDelete(DeleteBehavior.NoAction);

        builder.Property(x => x.Sections).IsRequired().HasColumnType("text[]");
        builder.Property(x => x.Message).IsRequired().HasMaxLength(MessageMaxLength);
        builder.Property(x => x.Deadline).IsRequired().HasColumnType("timestamp with time zone");
        builder.Property(x => x.ReturnedAt).IsRequired().HasColumnType("timestamp with time zone");
        builder.Property(x => x.ResolvedAt).HasColumnType("timestamp with time zone")
            .HasComment("When the correction was submitted. Null while the return is open.");
        builder.Property(x => x.CreatedAt).IsRequired().HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).IsRequired().HasDefaultValueSql("now()");

        // One open return per application; the race of two operators ends here.
        builder.HasIndex(x => x.ApplicationId)
            .IsUnique()
            .HasFilter("resolved_at IS NULL")
            .HasDatabaseName("ux_application_returns_one_open");
    }
}

public sealed class ApplicationVersionConfiguration : IEntityTypeConfiguration<ApplicationVersion>
{
    public const string AnswersPurpose = "application_versions.answers";

    public void Configure(EntityTypeBuilder<ApplicationVersion> builder)
    {
        builder.ToTable("application_versions", table =>
        {
            table.HasComment("Submitted versions of an application kept when it was returned for correction (T-103). Written once.");
            table.HasCheckConstraint("ck_application_versions_number_positive", "version_number > 0");
            table.HasCheckConstraint("ck_application_versions_answers_is_a_document", "jsonb_typeof(answers) IN ('object', 'array')");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.HasOne<Application>().WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<FormDefinition>().WithMany().HasForeignKey(x => x.FormDefinitionId).OnDelete(DeleteBehavior.NoAction);

        // Sensitive Information: encrypted by ApplicationReturnService, which
        // knows the form, and read back here, as for applications.answers.
        builder.Property(x => x.Answers).IsRequired().HasColumnType("jsonb")
            .HasConversion(new RevealedDocumentConverter(AnswersPurpose))
            .HasComment("The answers as submitted; answers of sensitive fields encrypted inside the document (T-47a).");

        // Sensitive Information: the card's sensitive fields encrypted as in applications.entity_snapshot.
        builder.Property(x => x.EntitySnapshot).HasColumnType("jsonb")
            .HasConversion(new EncryptedDocumentConverter("application_versions.entity_snapshot", EntitySnapshots.IsSensitive))
            .HasComment("The entity card as submitted with this version; sensitive fields encrypted (T-47a).");

        builder.Property(x => x.Checksum).IsRequired().HasMaxLength(128);
        builder.Property(x => x.SubmittedAt).IsRequired().HasColumnType("timestamp with time zone");
        builder.Property(x => x.SupersededAt).IsRequired().HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.ApplicationId, x.VersionNumber }).IsUnique();
    }
}
