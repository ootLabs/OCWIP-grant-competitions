using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class CompetitionConfiguration : IEntityTypeConfiguration<Competition>
{
    public void Configure(EntityTypeBuilder<Competition> builder)
    {
        builder.ToTable("competition");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired();

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

        builder.Property(x => x.MaxGrantAmount)
            .IsRequired()
            .HasPrecision(18, 2)
            .HasComment(
                "Maximum grant amount allowed for the competition. " +
                "Used later to validate the application budget.");

        builder.HasMany(x => x.FormDefinitions)
            .WithOne(x => x.Competition)
            .HasForeignKey(x => x.CompetitionId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
