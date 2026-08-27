using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class CompetitionConfiguration : IEntityTypeConfiguration<Competition>
{
    public void Configure(EntityTypeBuilder<Competition> builder)
    {

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(50);
        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.Status)
            .HasConversion<string>();

        builder.Property(x => x.StartDate)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasComment(
                "Competition start date and time stored in UTC.");

        builder.Property(x => x.EndDate)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasComment(
                "Competition closing date and time stored in UTC. " +
                "Submission is rejected at or after this moment. " +
                "UTC is used to avoid ambiguity caused by local time zones " +
                "and daylight saving time changes.");

        builder.ToTable(t => t.HasCheckConstraint(
        "ck_competition_start_date_before_end_date",
        "start_date < end_date"));

        builder.Property(x => x.MaxGrantAmount)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasComment(
                "Maximum grant amount allowed for the competition. " +
                "Used later to validate the application budget.");
       builder.ToTable(t => t.HasCheckConstraint(
            "ck_maxgrantamount_greater_than_0",
            "max_grant_amount > 0"));

        builder.HasMany(x => x.FormDefinitions)
            .WithOne(x => x.Competition)
            .HasForeignKey(x => x.CompetitionId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
