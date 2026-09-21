using Ocwip.Api.Models.Forms;
using Xunit;

namespace Ocwip.Api.Tests.Models.Forms;

/// <summary>
/// What the form contract refuses (T-24). Every case here is a definition that
/// parses as JSON and leaves the renderer with nothing to draw, so the only
/// place to stop it is before it reaches the database.
///
/// Each test also asserts the message names the field. A refusal that says
/// "niepoprawna definicja" sends the operator through sixty fields by hand.
/// </summary>
public sealed class FormSchemaRefusalTests
{
    private static string MessagesOf(FormSchemaValidationResult result) =>
        string.Join(" | ", result.Errors.Select(error => error.Message));

    [Fact]
    public void AnUnknownFieldKind_ShouldBeRefused()
    {
        // Arrange
        var definition = FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field("suwak", "slider"));

        // Act
        var result = FormSchemaValidator.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("slider", MessagesOf(result), StringComparison.Ordinal);
        Assert.Contains("suwak", MessagesOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public void ACalculatedFieldWithoutASource_ShouldBeRefused()
    {
        // Arrange
        var definition = FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field("suma", "calculated"));

        // Act
        var result = FormSchemaValidator.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("suma", MessagesOf(result), StringComparison.Ordinal);
        Assert.Contains("nie ma z czego", MessagesOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public void ACalculationReadingAFieldThatDoesNotExist_ShouldBeRefused()
    {
        // Arrange
        var definition = FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field("cena", "amount"),
            FormDefinitionSamples.Field(
                "wartosc",
                "calculated",
                """
                "calculation": { "kind": "product", "operands": ["cena", "liczba"] }
                """));

        // Act
        var result = FormSchemaValidator.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("liczba", MessagesOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public void ACalculationReadingSomethingThatIsNotANumber_ShouldBeRefused()
    {
        // Arrange
        var definition = FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field("tytul", "shortText", "\"maxLength\": 200"),
            FormDefinitionSamples.Field("cena", "amount"),
            FormDefinitionSamples.Field(
                "wartosc",
                "calculated",
                """
                "calculation": { "kind": "product", "operands": ["tytul", "cena"] }
                """));

        // Act
        var result = FormSchemaValidator.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("nie jest liczbą", MessagesOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public void ACircleOfCalculations_ShouldBeRefused()
    {
        // Arrange
        var definition = FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field("kwota", "amount"),
            FormDefinitionSamples.Field(
                "pierwsze",
                "calculated",
                """
                "calculation": { "kind": "difference", "operands": ["kwota", "drugie"] }
                """),
            FormDefinitionSamples.Field(
                "drugie",
                "calculated",
                """
                "calculation": { "kind": "difference", "operands": ["kwota", "pierwsze"] }
                """));

        // Act
        var result = FormSchemaValidator.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("nigdy się nie skończy", MessagesOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public void AConditionOnAFieldStandingLower_ShouldBeRefused()
    {
        // Arrange
        var definition = FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field(
                "inna_forma",
                "shortText",
                """
                "maxLength": 200,
                "visibleWhen": { "field": "forma_prawna", "equalsAnyOf": ["inna"] }
                """),
            FormDefinitionSamples.Field(
                "forma_prawna",
                "singleChoice",
                """
                "options": [{ "value": "inna", "label": "Inna" }]
                """));

        // Act
        var result = FormSchemaValidator.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("stoi niżej", MessagesOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public void AConditionOnAValueTheChoiceDoesNotHave_ShouldBeRefused()
    {
        // Arrange
        var definition = FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field(
                "forma_prawna",
                "singleChoice",
                """
                "options": [{ "value": "fundacja", "label": "Fundacja" }]
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
        Assert.False(result.IsValid);
        Assert.Contains("nigdy się nie spełni", MessagesOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public void TwoFieldsWithTheSameKey_ShouldBeRefused()
    {
        // Arrange
        var definition = FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field("tytul", "shortText", "\"maxLength\": 200"),
            FormDefinitionSamples.Field("tytul", "shortText", "\"maxLength\": 50"));

        // Act
        var result = FormSchemaValidator.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("użyty drugi raz", MessagesOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public void ATableInsideATable_ShouldBeRefused()
    {
        // Arrange
        var inner = FormDefinitionSamples.Field(
            "wiersz",
            "repeatableTable",
            $$"""
            "table": {
              "columns": [
                {{FormDefinitionSamples.Field("nazwa", "shortText", "\"maxLength\": 50")}}
              ]
            }
            """);

        var definition = FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field(
                "tabela",
                "repeatableTable",
                $$"""
                "table": { "columns": [{{inner}}] }
                """));

        // Act
        var result = FormSchemaValidator.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("wiersz", MessagesOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public void AFixedTableWithoutItsRows_ShouldBeRefused()
    {
        // Arrange
        var definition = FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field(
                "czlonkowie",
                "fixedTable",
                $$"""
                "table": {
                  "columns": [
                    {{FormDefinitionSamples.Field("imie", "shortText", "\"maxLength\": 50")}}
                  ]
                }
                """));

        // Act
        var result = FormSchemaValidator.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("czlonkowie", MessagesOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public void AChoiceWithoutOptions_ShouldBeRefused()
    {
        // Arrange
        var definition = FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field("forma", "singleChoice", "\"options\": []"));

        // Act
        var result = FormSchemaValidator.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("ani jednej opcji", MessagesOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public void ALimitMeasuredAgainstASettingTheCompetitionDoesNotHave_ShouldBeRefused()
    {
        // Arrange
        var definition = FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field(
                "kwota",
                "amount",
                """
                "limits": [
                  { "kind": "maxAmount", "basis": "competition.wymyslonyLimit" }
                ]
                """));

        // Act
        var result = FormSchemaValidator.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("wymyslonyLimit", MessagesOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public void ATextFieldWithoutACharacterLimit_ShouldBeRefused()
    {
        // Arrange
        var definition = FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field("opis", "longText"));

        // Act
        var result = FormSchemaValidator.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("limit znaków", MessagesOf(result), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[{\"key\":\"sekcja\"}]")]
    [InlineData("\"formularz\"")]
    [InlineData("7")]
    public void ARootThatIsNotAnObject_ShouldBeRefused(string json)
    {
        // Act
        var result = FormSchemaValidator.Validate(FormDefinitionSamples.Parse(json));

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Korzeń definicji", MessagesOf(result), StringComparison.Ordinal);
    }

    [Fact]
    public void ADefinitionWrittenAgainstAnotherContractVersion_ShouldBeRefused()
    {
        // Arrange
        var definition = FormDefinitionSamples.Parse(
            """
            {
              "schemaVersion": 99,
              "sections": [
                {
                  "key": "sekcja",
                  "title": "Sekcja",
                  "fields": [
                    {
                      "key": "tytul",
                      "type": "shortText",
                      "label": "Tytuł",
                      "required": true,
                      "printed": true,
                      "maxLength": 200
                    }
                  ]
                }
              ]
            }
            """);

        // Act
        var result = FormSchemaValidator.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Wersja kontraktu 99", MessagesOf(result), StringComparison.Ordinal);
    }

    /// <summary>
    /// Enum.TryParse also reads the numbers behind the names, so without a
    /// guard "77" would be stored as a kind of limit nobody declared and "0"
    /// would quietly become whichever member happens to be first.
    /// </summary>
    [Theory]
    [InlineData(
        "\"limits\": [{ \"kind\": \"77\", \"basis\": \"competition.maxGrantAmount\" }]",
        "amount")]
    [InlineData(
        "\"calculation\": { \"kind\": \"1\", \"operands\": [\"a\", \"b\"] }",
        "calculated")]
    [InlineData(
        "\"file\": { \"allowedFormats\": [\"99\"] }",
        "file")]
    public void AnEnumWrittenAsItsNumber_ShouldBeRefused(string extra, string type)
    {
        // Arrange
        var definition = FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field("pole", type, extra));

        // Act
        var result = FormSchemaValidator.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ATableWithMoreColumnsThanATableCanHave_ShouldBeRefused()
    {
        // Arrange
        var columns = string.Join(
            ",",
            Enumerable.Range(0, 51).Select(
                index => FormDefinitionSamples.Field(
                    $"kolumna_{index}",
                    "shortText",
                    "\"maxLength\": 50")));

        var definition = FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field(
                "tabela",
                "repeatableTable",
                $$"""
                "table": { "columns": [{{columns}}] }
                """));

        // Act
        var result = FormSchemaValidator.Validate(definition);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("więcej niż 50 kolumn", MessagesOf(result), StringComparison.Ordinal);
    }

    /// <summary>
    /// The path is what the creator (T-26) puts the operator back on. A label
    /// built from the key of the field the condition POINTS AT would collide
    /// for two fields revealed by the same answer.
    /// </summary>
    [Fact]
    public void ARefusal_ShouldPointAtTheFieldWithAJsonPath()
    {
        // Arrange
        var definition = FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field("kwota", "amount"),
            FormDefinitionSamples.Field(
                "wartosc",
                "calculated",
                """
                "calculation": { "kind": "product", "operands": ["kwota", "brak"] }
                """));

        // Act
        var result = FormSchemaValidator.Validate(definition);

        // Assert
        var error = Assert.Single(result.Errors);

        Assert.Equal("$.sections[0].fields[1].calculation", error.Path);
    }

    [Fact]
    public void AFieldWithoutAKind_ShouldBeRefusedOnceAndNotForEverythingElse()
    {
        // Arrange
        var definition = FormDefinitionSamples.Parse(
            """
            {
              "schemaVersion": 1,
              "sections": [
                {
                  "key": "sekcja",
                  "title": "Sekcja",
                  "fields": [{ "key": "opis", "label": "Opis" }]
                }
              ]
            }
            """);

        // Act
        var result = FormSchemaValidator.Validate(definition);

        // Assert
        var error = Assert.Single(result.Errors);

        Assert.Equal("$.sections[0].fields[0].type", error.Path);
    }

    [Fact]
    public void ARefusal_ShouldListEveryReasonAtOnce()
    {
        // Arrange
        var definition = FormDefinitionSamples.WithFields(
            FormDefinitionSamples.Field("pierwsze", "slider"),
            FormDefinitionSamples.Field("drugie", "calculated"),
            FormDefinitionSamples.Field("trzecie", "longText"));

        // Act
        var result = FormSchemaValidator.Validate(definition);

        // Assert
        Assert.Equal(3, result.Errors.Count);
        Assert.Null(result.Document);
    }
}
