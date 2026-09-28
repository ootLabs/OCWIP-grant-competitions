using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Data.Encryption;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;

namespace Ocwip.Api.Data.Configurations;

public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        // Encrypted by ReportService, which knows both forms; read back here (T-47a).
        builder.Property(x => x.Answers).IsRequired().HasColumnType("jsonb")
            .HasConversion(new RevealedDocumentConverter(SensitiveAnswers.ReportPurpose))
            .HasComment("The applicant's answers. Answers of sensitive fields, and of fields prefilled from one, encrypted inside the document (T-47a).");
        builder.Property(x => x.Prefill).IsRequired().HasColumnType("jsonb")
            .HasConversion(new RevealedDocumentConverter(SensitiveAnswers.ReportPurpose))
            .HasComment("Values taken from the application when the report was started, restored on every save. Sensitive ones encrypted like answers (T-47a).");

        builder.Property(x => x.CostReview).IsRequired().HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb")
            .HasComment("Costs the operator did not accept, row by row of the report budget, with the reason (T-50b).");

        // Every save of a report checks the state it was read in (T-50b): an
        // operator returning or reviewing a report another operator accepted
        // meanwhile fails instead of overwriting an accepted, settled report.
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20).IsConcurrencyToken();
        builder.Property(x => x.ReturnReason).HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).IsRequired().HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).IsRequired().HasDefaultValueSql("now()");

        builder.HasOne(x => x.Application).WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.FormDefinition).WithMany().HasForeignKey(x => x.FormDefinitionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Competition>().WithMany().HasForeignKey(x => x.CompetitionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Entity>().WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.NoAction);

        // One active report per application: a second "start" hands back the first.
        builder.HasIndex(x => x.ApplicationId)
            .IsUnique()
            .HasFilter("is_active")
            .HasDatabaseName("ux_reports_one_active_per_application");

        builder.HasIndex(x => new { x.CompetitionId, x.Status });

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_reports_submitted_at_matches_status",
                "(status = 'Draft') = (submitted_at IS NULL)");
            table.HasCheckConstraint(
                "ck_reports_accepted_at_matches_status",
                "(status = 'Accepted') = (accepted_at IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_reports_answers_is_an_object",
                "jsonb_typeof(answers) = 'object' AND jsonb_typeof(prefill) = 'object'");
            table.HasCheckConstraint(
                "ck_reports_cost_review_is_an_array",
                "jsonb_typeof(cost_review) = 'array'");
        });
    }
}

public sealed class ReportStatusHistoryConfiguration : IEntityTypeConfiguration<ReportStatusHistory>
{
    public void Configure(EntityTypeBuilder<ReportStatusHistory> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.FromStatus).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ToStatus).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Reason).HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).IsRequired().HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).IsRequired().HasDefaultValueSql("now()");

        builder.HasOne<Report>().WithMany().HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ChangedByUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(x => x.ReportId);
    }
}
