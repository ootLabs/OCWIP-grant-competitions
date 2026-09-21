using Ocwip.Api.Models.Forms;
using Xunit;

namespace Ocwip.Api.Tests.Models.Forms;

/// <summary>
/// What the form contract accepts (T-24): every kind of field the real
/// application templates use, and the budget table that decides the shape of
/// the whole schema.
/// </summary>
public sealed class FormSchemaValidatorTests
{
    [Fact]
    public void EveryFieldKind_ShouldHaveARepresentationInTheSchema()
    {
        // Arrange
        var definition = FormDefinitionSamples.AllFieldKinds();

        // Act
        var result = FormSchemaValidator.Validate(definition);

        // Assert
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));

        var used = result.Document!.AllFields()
            .Select(field => field.Field.Type)
            .ToHashSet();

        var missing = FormFieldTypes.All
            .Where(type => type != FormFieldType.Unknown && !used.Contains(type))
            .Select(FormFieldTypes.WireName);

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryField_ShouldCarryTheMinimumTheRendererNeeds()
    {
        // Act
        var result = FormSchemaValidator.Validate(FormDefinitionSamples.AllFieldKinds());

        // Assert
        Assert.All(
            result.Document!.AllFields(),
            field =>
            {
                Assert.NotEmpty(field.Key);
                Assert.NotEqual(FormFieldType.Unknown, field.Field.Type);
                Assert.NotEmpty(field.Field.Label);
            });

        var text = result.Document.AllFields()
            .First(field => field.Key == "opis")
            .Field;

        Assert.Equal("Podpowiedź do pola opis", text.Help);
        Assert.True(text.Required);
        Assert.Equal(5000, text.MaxLength);
    }

    /// <summary>
    /// Decision D14. The share of indirect costs is a technical field: it is on
    /// the screen so the applicant can see the limit, and it has no business on
    /// the printed offer.
    /// </summary>
    [Fact]
    public void AField_ShouldSayWhetherItGoesOnThePrintedOffer()
    {
        // Act
        var result = FormSchemaValidator.Validate(FormDefinitionSamples.Budget());

        // Assert
        var fields = result.Document!.AllFields().ToDictionary(field => field.Key);

        Assert.False(fields["udzial_posrednich"].Field.Printed);
        Assert.True(fields["dotacja"].Field.Printed);
    }

    [Fact]
    public void TheBudgetTable_ShouldPassThroughTheSchemaWithoutItsOwnExceptions()
    {
        // Act
        var result = FormSchemaValidator.Validate(FormDefinitionSamples.Budget());

        // Assert
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));

        var fields = result.Document!.AllFields().ToDictionary(field => field.Key);

        // The per row value is an ordinary calculated column, not a shape the
        // budget got to itself.
        Assert.Equal(
            FormCalculationKind.Product,
            fields["budzet_a.wartosc"].Field.Calculation!.Kind);

        Assert.Equal(
            ["budzet_a.wartosc"],
            fields["suma_a"].Field.Calculation!.Operands);
    }

    /// <summary>
    /// Decision D11: the grant is what is left after the applicant's own
    /// contribution, so the column is read only and the amount comes out of
    /// the definition rather than out of a text box.
    /// </summary>
    [Fact]
    public void TheGrant_ShouldBeACalculatedDifference()
    {
        // Act
        var result = FormSchemaValidator.Validate(FormDefinitionSamples.Budget());

        // Assert
        var grant = result.Document!.AllFields().First(f => f.Key == "dotacja").Field;

        Assert.Equal(FormFieldType.Calculated, grant.Type);
        Assert.Equal(FormCalculationKind.Difference, grant.Calculation!.Kind);
        Assert.Equal(["suma_a", "wklad_wlasny"], grant.Calculation.Operands);
    }

    /// <summary>
    /// Decision D12: the ceiling is written as a rule plus what it is measured
    /// against, so the engine in T-30 can turn "at most 10% of the grant" into
    /// "you may still enter 900 zl". A yes or no flag could not be inverted.
    /// </summary>
    [Fact]
    public void ALimit_ShouldCarryEnoughToBeReadBackwards()
    {
        // Act
        var result = FormSchemaValidator.Validate(FormDefinitionSamples.Budget());

        // Assert
        var limit = Assert.Single(
            result.Document!.AllFields().First(f => f.Key == "suma_c").Field.Limits);

        Assert.Equal(FormLimitKind.MaxPercentOf, limit.Kind);
        Assert.Equal(10m, limit.Percent);
        Assert.Equal("dotacja", limit.Basis);
    }

    [Fact]
    public void AConditionalField_ShouldKeepThePairItIsRevealedBy()
    {
        // Arrange
        var definition = FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field(
                "forma_prawna",
                "singleChoice",
                """
                "options": [
                  { "value": "fundacja", "label": "Fundacja" },
                  { "value": "inna", "label": "Inna" }
                ]
                """),
            FormDefinitionSamples.Field(
                "inna_forma",
                "shortText",
                """
                "maxLength": 200,
                "visibleWhen": { "field": "forma_prawna", "equalsAnyOf": ["inna"] }
                """));

        // Act
        var result = FormSchemaValidator.Validate(definition);

        // Assert
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));

        var condition = result.Document!.Sections[0].Fields[1].VisibleWhen!;

        Assert.Equal("forma_prawna", condition.Field);
        Assert.Equal(["inna"], condition.EqualsAnyOf);
    }

    [Fact]
    public void TheRoot_ShouldBeAnObjectAndCarryTheContractVersion()
    {
        // Act
        var result = FormSchemaValidator.Validate(FormDefinitionSamples.Budget());

        // Assert
        Assert.Equal(FormDocument.CurrentSchemaVersion, result.Document!.SchemaVersion);
    }
}
