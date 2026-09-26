using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class ResultNotificationConfiguration : IEntityTypeConfiguration<ResultNotification>
{
    public void Configure(EntityTypeBuilder<ResultNotification> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Result)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.LastError)
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.HasOne<Competition>()
            .WithMany()
            .HasForeignKey(x => x.CompetitionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<Application>()
            .WithMany()
            .HasForeignKey(x => x.ApplicationId)
            .OnDelete(DeleteBehavior.NoAction);

        // One mail per application, whatever happens to the sending runs.
        builder.HasIndex(x => x.ApplicationId)
            .IsUnique();

        builder.HasIndex(x => new { x.CompetitionId, x.SentAt });

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_result_notifications_result",
                "result IN ('Funded', 'Reserve', 'Rejected')");

            table.HasCheckConstraint(
                "ck_result_notifications_attempts_not_negative",
                "attempts >= 0");
        });
    }
}
