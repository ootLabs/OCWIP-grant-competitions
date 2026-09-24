using Ocwip.Api.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Models.Forms.AnswerSamples;

namespace Ocwip.Api.Tests.Models.Forms;

/// <summary>
/// Every kind refuses a value the renderer could not have produced, already
/// in a draft (T-30): this is what "any JSON sent straight to the API does
/// not get in" means one field at a time.
/// </summary>
public sealed class AnswerShapeTests
{
    [Theory]
    [InlineData("tytul", "12", "Odpowiedź musi być tekstem.")]
    [InlineData("kwota", "\"1500 zł\"", "Odpowiedź musi być liczbą.")]
    [InlineData("uczestnicy", "true", "Odpowiedź musi być liczbą.")]
    [InlineData("data_konca", "\"31.12.2026\"", "Odpowiedź musi być datą.")]
    [InlineData("data_konca", "\"2026-02-30\"", "Odpowiedź musi być datą.")]
    [InlineData("termin_spotkania", "\"2026-11-02\"", "Odpowiedź musi być datą z godziną.")]
    [InlineData("wersja_papierowa", "\"tak\"", "Odpowiedź musi być wyborem tak albo nie.")]
    [InlineData("oswiadczenie_1", "1", "Odpowiedź musi być wyborem tak albo nie.")]
    [InlineData("forma_prawna", "\"spolka\"", "Tej odpowiedzi nie ma na liście wyboru.")]
    [InlineData("forma_prawna", "[\"fundacja\"]", "Odpowiedź musi być jedną z opcji.")]
    [InlineData("formaty", "\"pdf\"", "Odpowiedź musi być listą zaznaczonych opcji.")]
    [InlineData("formaty", "[\"pdf\", \"exe\"]", "Tej odpowiedzi nie ma na liście wyboru.")]
    [InlineData("formaty", "[\"pdf\", \"pdf\"]", "Ta sama opcja jest zaznaczona dwa razy.")]
    [InlineData("odpis_z_rejestru", "\"odpis.pdf\"", "Załącznik musi mieć nazwę i rozmiar.")]
    [InlineData(
        "odpis_z_rejestru",
        "{ \"name\": \"odpis.pdf\", \"sizeBytes\": \"duży\" }",
        "Załącznik musi mieć nazwę i rozmiar.")]
    [InlineData(
        "odpis_z_rejestru",
        "{ \"name\": \"odpis.pdf\", \"sizeBytes\": 1, \"url\": \"https://example.org\" }",
        "Załącznik musi mieć nazwę i rozmiar.")]
    [InlineData("rezultaty", "{ \"rezultat\": \"Spotkania\" }", "Odpowiedź musi być listą wierszy.")]
    public void A_value_of_the_wrong_shape_is_refused_already_in_a_draft(
        string key, string value, string message)
    {
        var answers = Parse($$"""{ "{{key}}": {{value}} }""");

        var result = AnswerValidator.Validate(
            Document(FormDefinitionSamples.AllFieldKinds()),
            answers,
            NoCompetitionSettings,
            AnswerStrictness.Draft);

        Assert.Equal(message, MessageFor(result, key));
        Assert.Single(result.Errors);
    }

    [Theory]
    [InlineData("tytul", "null")]
    [InlineData("tytul", "\"\"")]
    [InlineData("data_konca", "\"\"")]
    [InlineData("forma_prawna", "\"\"")]
    [InlineData("formaty", "[]")]
    [InlineData("kwota", "null")]
    [InlineData("termin_spotkania", "\"2026-11-02T10:30:15\"")]
    [InlineData("czlonkowie_grupy", "null")]
    public void What_a_cleared_or_untouched_input_holds_is_a_gap_a_draft_keeps(
        string key, string value)
    {
        var answers = Parse($$"""{ "{{key}}": {{value}} }""");

        var result = AnswerValidator.Validate(
            Document(FormDefinitionSamples.AllFieldKinds()),
            answers,
            NoCompetitionSettings,
            AnswerStrictness.Draft);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }
}
