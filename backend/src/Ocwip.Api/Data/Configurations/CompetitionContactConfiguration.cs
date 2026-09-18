using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class CompetitionContactConfiguration
    : IEntityTypeConfiguration<CompetitionContact>
{
    public void Configure(EntityTypeBuilder<CompetitionContact> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Position)
            .IsRequired();

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_competition_contacts_position_not_negative",
                "position >= 0");
        });

        // One person once. Listing the same employee twice is never intended
        // and reaches applicants as a duplicated contact on a public page.
        builder.HasIndex(x => new
        {
            x.CompetitionId,
            x.UserId
        }).IsUnique();

        // NoAction, see the attachment configuration: rule 1 holds everywhere
        // and the rows this one drops are deleted explicitly.
        builder.HasOne(x => x.Competition)
            .WithMany(x => x.Contacts)
            .HasForeignKey(x => x.CompetitionId)
            .OnDelete(DeleteBehavior.NoAction);

        // NoAction towards the account, because that one IS a record: an
        // employee account is deactivated, never deleted, and a competition
        // keeps saying who answered questions about it.
        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
