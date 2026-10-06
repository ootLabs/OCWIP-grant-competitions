using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Data.Encryption;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class ReportVersionConfiguration : IEntityTypeConfiguration<ReportVersion>
{
    public const string AnswersPurpose = "report_versions.answers";

    public const string PrefillPurpose = "report_versions.prefill";

    public void Configure(EntityTypeBuilder<ReportVersion> builder)
    {
        builder.ToTable("report_versions", table =>
        {
            table.HasComment("Submitted versions of a report kept when it was returned for correction (S-35). Written once.");
            table.HasCheckConstraint("ck_report_versions_number_positive", "version_number > 0");
            table.HasCheckConstraint("ck_report_versions_answers_is_an_object", "jsonb_typeof(answers) = 'object'");
            table.HasCheckConstraint("ck_report_versions_prefill_is_an_object", "jsonb_typeof(prefill) = 'object'");
            table.HasCheckConstraint("ck_report_versions_cost_review_is_an_array", "jsonb_typeof(cost_review) = 'array'");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.HasOne<Report>().WithMany().HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<FormDefinition>().WithMany().HasForeignKey(x => x.FormDefinitionId).OnDelete(DeleteBehavior.NoAction);

        // Sensitive Information: encrypted by ReportService, which knows both
        // forms, and read back here, as for reports.answers. A purpose of its
        // own per column, so a ciphertext cannot be moved between them.
        builder.Property(x => x.Answers).IsRequired().HasColumnType("jsonb")
            .HasConversion(new RevealedDocumentConverter(AnswersPurpose))
            .HasComment("The answers as submitted; answers of sensitive fields encrypted inside the document (T-47a).");

        builder.Property(x => x.Prefill).IsRequired().HasColumnType("jsonb")
            .HasConversion(new RevealedDocumentConverter(PrefillPurpose))
            .HasComment("The values taken from the application when this version was submitted; sensitive ones encrypted (T-47a).");

        builder.Property(x => x.CostReview).IsRequired().HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb")
            .HasComment("What the operator did not accept in this version, with the reason (S-35).");

        builder.Property(x => x.SubmittedAt).IsRequired().HasColumnType("timestamp with time zone");
        builder.Property(x => x.SupersededAt).IsRequired().HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.ReportId, x.VersionNumber }).IsUnique();
    }
}
