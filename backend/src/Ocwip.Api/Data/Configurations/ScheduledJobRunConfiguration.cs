using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class ScheduledJobRunConfiguration : IEntityTypeConfiguration<ScheduledJobRun>
{
    public void Configure(EntityTypeBuilder<ScheduledJobRun> builder)
    {
        builder.ToTable("scheduled_job_runs", table =>
        {
            table.HasComment("Runs of background jobs (T-105): one row per job, subject and due moment, completed at most once.");
            table.HasCheckConstraint("ck_scheduled_job_runs_completed_after_claim", "completed_at IS NULL OR claimed_at IS NOT NULL");
            table.HasCheckConstraint("ck_scheduled_job_runs_attempts_not_negative", "attempts >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Job).IsRequired().HasMaxLength(50);
        builder.Property(x => x.DueAt).IsRequired().HasColumnType("timestamp with time zone");
        builder.Property(x => x.ClaimedAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CompletedAt).HasColumnType("timestamp with time zone");
        builder.Property(x => x.LastError).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).IsRequired().HasDefaultValueSql("now()");

        // The idempotency of every job: one run per job, subject and due moment.
        builder.HasIndex(x => new { x.Job, x.SubjectId, x.DueAt })
            .IsUnique()
            .HasDatabaseName("ux_scheduled_job_runs_job_subject_due");
    }
}
