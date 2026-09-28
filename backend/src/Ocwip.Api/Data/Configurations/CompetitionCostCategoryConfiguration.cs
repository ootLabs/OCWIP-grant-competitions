using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class CompetitionCostCategoryConfiguration
    : IEntityTypeConfiguration<CompetitionCostCategory>
{
    public void Configure(EntityTypeBuilder<CompetitionCostCategory> builder)
    {
        // Taken off the list by an edit, not deleted (T-101).
        builder.Property(x => x.IsActive).IsRequired().HasDefaultValue(true)
            .HasComment("False once an edit took the row off the list. Rows are never removed (retention).");
        builder.Property(x => x.DeactivatedAt).HasColumnType("timestamp with time zone");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Category)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(x => x.Position)
            .IsRequired();

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_competition_cost_categories_deactivated_at_matches_is_active",
                "is_active = (deactivated_at IS NULL)");

            table.HasCheckConstraint(
                "ck_competition_cost_categories_position_not_negative",
                "position >= 0");
        });

        // A category is either allowed or it is not, so it appears at most
        // once. Two rows for one category would make "is this on" a question
        // with a count in the answer.
        builder.HasIndex(x => new
        {
            x.CompetitionId,
            x.Category
        }).IsUnique();

        builder.HasOne(x => x.Competition)
            .WithMany(x => x.CostCategories)
            .HasForeignKey(x => x.CompetitionId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
