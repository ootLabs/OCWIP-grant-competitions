using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class ConsentAcceptanceConfiguration : IEntityTypeConfiguration<ConsentAcceptance>
{
    public void Configure(EntityTypeBuilder<ConsentAcceptance> builder)
    {
        builder.ToTable("consent_acceptances", table =>
            table.HasComment("Documents accepted at registration (T-107): kind, version, the full text seen and the moment. Written once."));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.Property(x => x.Kind).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Version).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Text).IsRequired();
        builder.Property(x => x.AcceptedAt).IsRequired().HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.UserId, x.Kind, x.Version }).IsUnique();
    }
}
