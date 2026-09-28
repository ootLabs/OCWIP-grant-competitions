using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class PersonalDataReadConfiguration : IEntityTypeConfiguration<PersonalDataRead>
{
    public void Configure(EntityTypeBuilder<PersonalDataRead> builder)
    {
        builder.ToTable("personal_data_reads", table => table.HasComment(
            "Who read a resource holding personal data, and when (T-47a). Append-only; names the resource, never copies it."));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        // NoAction, like every foreign key here: an account is never removed,
        // and the log must outlive whatever it names.
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);

        builder.Property(x => x.Resource).IsRequired().HasMaxLength(40);
        builder.Property(x => x.Endpoint).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ReadAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        // "Who saw this application" and "what did this person read".
        builder.HasIndex(x => new { x.Resource, x.ResourceId, x.ReadAt });
        builder.HasIndex(x => new { x.UserId, x.ReadAt });
    }
}
