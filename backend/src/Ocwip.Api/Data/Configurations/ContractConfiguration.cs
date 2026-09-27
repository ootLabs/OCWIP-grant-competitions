using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class DocumentTemplateConfiguration : IEntityTypeConfiguration<DocumentTemplate>
{
    public void Configure(EntityTypeBuilder<DocumentTemplate> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Kind).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Body).IsRequired().HasMaxLength(100_000);
        builder.Property(x => x.CreatedAt).IsRequired().HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).IsRequired().HasDefaultValueSql("now()");

        builder.HasOne<Competition>().WithMany().HasForeignKey(x => x.CompetitionId).OnDelete(DeleteBehavior.NoAction);

        // A version number is used once per competition and kind; the race of
        // two publications ends here.
        builder.HasIndex(x => new { x.CompetitionId, x.Kind, x.VersionNumber }).IsUnique();

        builder.ToTable(table => table.HasCheckConstraint("ck_document_templates_version_positive", "version_number > 0"));
    }
}

public sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Values).IsRequired().HasColumnType("jsonb")
            .HasComment("Placeholder values typed by the operator. Holds personal data (people who sign, bank account); sensitive.");
        builder.Property(x => x.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreatedAt).IsRequired().HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).IsRequired().HasDefaultValueSql("now()");

        builder.HasOne(x => x.Application).WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Competition>().WithMany().HasForeignKey(x => x.CompetitionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Entity>().WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => x.ApplicationId)
            .IsUnique()
            .HasFilter("is_active")
            .HasDatabaseName("ux_contracts_one_active_per_application");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_contracts_signed_on_matches_status", "(status = 'Signed') = (signed_on IS NOT NULL)");
            table.HasCheckConstraint("ck_contracts_values_is_an_object", "jsonb_typeof(values) = 'object'");
        });
    }
}
