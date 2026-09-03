using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations
{
    public sealed class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id)
            .HasDefaultValueSql("gen_random_uuid()");

            builder.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(254);
            builder.HasIndex(x => x.Email)
                .IsUnique();

            builder.Property(x => x.Role)
                .IsRequired()
                .HasConversion<string>();

            builder.Property(x => x.Pesel)
                .IsRequired()
                .HasMaxLength(11);
            builder.ToTable(t => t.HasCheckConstraint(
                "ck_user_pesel_length",
                "\"pesel\" ~ '^[0-9]{11}$'"));

            builder.Property(x => x.IsVerified)
                .IsRequired();

            builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

            builder.Property(x => x.UpdatedAt)
                .IsRequired()
                .HasColumnType("timestamp with time zone")
                .HasDefaultValueSql("now()");

            builder.Property(x => x.DeactivatedAt)
                .HasColumnType("timestamp with time zone");


        }
    }
}
