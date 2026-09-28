using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class AttachmentTemplateConfiguration : IEntityTypeConfiguration<AttachmentTemplate>
{
    public void Configure(EntityTypeBuilder<AttachmentTemplate> builder)
    {
        builder.ToTable("attachment_templates", table =>
        {
            table.HasComment("Files given out with an attachment requirement to fill in (T-102). Public, no personal data.");
            table.HasCheckConstraint("ck_attachment_templates_size_positive", "size_in_bytes > 0");
            table.HasCheckConstraint("ck_attachment_templates_deactivated_at_matches_is_active", "is_active = (deactivated_at IS NULL)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.HasOne(x => x.CompetitionAttachment)
            .WithMany(x => x.Templates)
            .HasForeignKey(x => x.CompetitionAttachmentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Property(x => x.FileName).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Format).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.StoragePath).IsRequired().HasMaxLength(500);
        builder.Property(x => x.CreatedAt).IsRequired().HasDefaultValueSql("now()");

        builder.HasIndex(x => x.StoragePath).IsUnique();

        // One template in force per requirement; a replacement racing another ends here.
        builder.HasIndex(x => x.CompetitionAttachmentId)
            .IsUnique()
            .HasFilter("is_active")
            .HasDatabaseName("ux_attachment_templates_one_active");
    }
}
