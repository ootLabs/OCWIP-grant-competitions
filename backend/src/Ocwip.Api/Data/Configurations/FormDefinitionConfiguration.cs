using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ocwip.Api.Models;

namespace Ocwip.Api.Data.Configurations;

public sealed class FormDefinitionConfiguration: IEntityTypeConfiguration<FormDefinition>
{
    public void Configure(EntityTypeBuilder<FormDefinition> builder)
    {
        builder.ToTable("form_definition");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.VersionNumber)
            .IsRequired();

        builder.Property(x => x.Definition)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasComment(
                "Form structure stored as JSONB. " +
                "The JSON contract, including sections, fields and validations, " +
                "will be defined separately in a future sprint.");

        builder.HasIndex(x => new
        {
            x.CompetitionId,
            x.VersionNumber
        })
        .IsUnique();
    }
}
