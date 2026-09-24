using System.Text.Json.Nodes;
using Ocwip.Api.Models.Forms;
using Xunit;
using static Ocwip.Api.Tests.Models.Forms.AnswerSamples;

namespace Ocwip.Api.Tests.Models.Forms;

/// <summary>
/// The two levels of T-30 and the keys its messages travel under: a draft
/// may have gaps, a submission may not, and neither may carry an answer the
/// form does not have.
/// </summary>
public sealed class AnswerValidatorTests
{
    [Fact]
    public void A_complete_application_passes_at_both_levels()
    {
        foreach (var strictness in new[] { AnswerStrictness.Draft, AnswerStrictness.Submission })
        {
            var result = Validate(
                FormDefinitionSamples.AllFieldKinds(), CompleteAllFieldKinds(), strictness);

            Assert.True(result.IsValid, string.Join("; ", result.Errors));
        }
    }

    [Fact]
    public void A_draft_may_be_empty_and_a_submission_names_every_missing_field()
    {
        var definition = FormDefinitionSamples.AllFieldKinds();

        var draft = Validate(definition, new JsonObject(), AnswerStrictness.Draft);
        var submission = Validate(definition, new JsonObject(), AnswerStrictness.Submission);

        Assert.True(draft.IsValid);
        Assert.Equal("To pole jest wymagane.", MessageFor(submission, "tytul"));
        Assert.Equal("To pole jest wymagane.", MessageFor(submission, "oswiadczenie_1"));
        Assert.Equal("To pole jest wymagane.", MessageFor(submission, "rezultaty"));
        // Three fixed rows exist whether or not anybody typed into them.
        Assert.Equal("Wymagane.", MessageFor(submission, "czlonkowie_grupy[2].telefon"));
        // A calculated field has nothing to fill in, so it is never missing.
        Assert.Null(MessageFor(submission, "suma_uczestnikow"));
    }

    [Fact]
    public void An_answer_the_form_does_not_have_is_refused_rather_than_dropped()
    {
        var answers = new JsonObject { ["tytul"] = "Tytuł", ["numer_konta"] = "123" };

        var result = Validate(FormDefinitionSamples.AllFieldKinds(), answers, AnswerStrictness.Draft);

        Assert.Equal("Formularz nie ma takiego pola.", MessageFor(result, "numer_konta"));
        Assert.Single(result.Errors);
    }

    [Fact]
    public void A_key_sent_twice_is_refused_because_two_readers_would_see_two_applications()
    {
        var answers = Parse("""{ "tytul": "Pierwszy", "tytul": "Drugi" }""");

        var result = AnswerValidator.Validate(
            Document(FormDefinitionSamples.AllFieldKinds()),
            answers,
            NoCompetitionSettings,
            AnswerStrictness.Draft);

        Assert.Equal(
            "To pole występuje w odpowiedziach dwa razy.", MessageFor(result, "tytul"));
    }

    [Fact]
    public void A_calculated_value_sent_with_the_answers_is_refused_at_either_level()
    {
        // D11: the grant is computed. A request that carries its own would
        // otherwise walk past every limit measured against it.
        var answers = new JsonObject { ["suma_uczestnikow"] = 1 };

        var result = Validate(FormDefinitionSamples.AllFieldKinds(), answers, AnswerStrictness.Draft);

        Assert.Equal(
            "To pole jest wyliczane i nie przyjmuje odpowiedzi.",
            MessageFor(result, "suma_uczestnikow"));
    }

    [Fact]
    public void A_cell_error_is_keyed_the_way_the_renderer_keys_the_cell()
    {
        var answers = CompleteAllFieldKinds();
        answers["rezultaty"] = new JsonArray(
            new JsonObject { ["rezultat"] = "Spotkania", ["wartosc_docelowa"] = 10 },
            new JsonObject { ["rezultat"] = "Wystawa", ["wartosc_docelowa"] = "dużo" },
            new JsonObject { ["rezultat"] = "Film", ["kolor"] = "zielony" });

        var result = Validate(FormDefinitionSamples.AllFieldKinds(), answers, AnswerStrictness.Draft);

        Assert.Equal(
            "Odpowiedź musi być liczbą.", MessageFor(result, "rezultaty[1].wartosc_docelowa"));
        Assert.Equal("Tabela nie ma takiej kolumny.", MessageFor(result, "rezultaty[2].kolor"));
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void Ranges_and_minimum_lengths_wait_for_submission_but_the_maximum_length_does_not()
    {
        var answers = CompleteAllFieldKinds();
        answers["opis"] = "Za krótko";
        answers["uczestnicy"] = 0;
        answers["tytul"] = new string('x', 201);

        var draft = Validate(FormDefinitionSamples.AllFieldKinds(), answers, AnswerStrictness.Draft);
        var submission = Validate(
            FormDefinitionSamples.AllFieldKinds(), answers, AnswerStrictness.Submission);

        // The input stops at the limit, so a longer text never came from the form.
        Assert.Equal("Przekroczono limit 200 znaków (jest 201).", MessageFor(draft, "tytul"));
        Assert.Single(draft.Errors);

        Assert.Equal("Wymagane co najmniej 1000 znaków (jest 9).", MessageFor(submission, "opis"));
        Assert.Equal("Wartość nie może być mniejsza niż 1.", MessageFor(submission, "uczestnicy"));
        Assert.Equal("Przekroczono limit 200 znaków (jest 201).", MessageFor(submission, "tytul"));
    }

    [Fact]
    public void An_unticked_declaration_is_missing_although_no_to_a_question_is_an_answer()
    {
        var answers = CompleteAllFieldKinds();
        answers["oswiadczenie_1"] = false;
        answers["wersja_papierowa"] = false;

        var result = Validate(
            FormDefinitionSamples.AllFieldKinds(), answers, AnswerStrictness.Submission);

        Assert.Equal("To pole jest wymagane.", MessageFor(result, "oswiadczenie_1"));
        Assert.Null(MessageFor(result, "wersja_papierowa"));
    }

    [Fact]
    public void A_field_hidden_by_its_condition_or_its_section_is_not_required()
    {
        var definition = ConditionalForm();
        var answers = new JsonObject { ["forma_prawna"] = "fundacja" };

        var hidden = Validate(definition, answers, AnswerStrictness.Submission);

        Assert.True(hidden.IsValid, string.Join("; ", hidden.Errors));

        answers["forma_prawna"] = "inna";
        var shown = Validate(definition, answers, AnswerStrictness.Submission);

        Assert.Equal("To pole jest wymagane.", MessageFor(shown, "jaka_forma"));
        Assert.Equal("To pole jest wymagane.", MessageFor(shown, "statut"));
    }

    [Fact]
    public void A_table_of_fixed_size_takes_no_more_rows_than_it_declares()
    {
        var answers = CompleteAllFieldKinds();
        answers["czlonkowie_grupy"] = new JsonArray(
            new JsonObject(), new JsonObject(), new JsonObject(), new JsonObject());

        var result = Validate(FormDefinitionSamples.AllFieldKinds(), answers, AnswerStrictness.Draft);

        Assert.Equal(
            "Za dużo wierszy: dopuszczalna liczba to 3 (jest 4).", MessageFor(result, "czlonkowie_grupy"));
    }

    [Fact]
    public void A_row_left_as_a_hole_reads_as_empty_and_a_row_that_is_not_a_row_is_refused()
    {
        var answers = CompleteAllFieldKinds();
        answers["czlonkowie_grupy"] = new JsonArray(null, "Anna Nowak");

        var draft = Validate(FormDefinitionSamples.AllFieldKinds(), answers, AnswerStrictness.Draft);
        var submission = Validate(
            FormDefinitionSamples.AllFieldKinds(), answers, AnswerStrictness.Submission);

        Assert.Equal("Wiersz musi być obiektem.", MessageFor(draft, "czlonkowie_grupy[1]"));
        Assert.Single(draft.Errors);

        Assert.Equal("Wymagane.", MessageFor(submission, "czlonkowie_grupy[0].imie_i_nazwisko"));
        Assert.Null(MessageFor(submission, "czlonkowie_grupy[1].imie_i_nazwisko"));
    }

    [Fact]
    public void A_table_the_applicant_adds_rows_to_has_a_ceiling_whatever_the_definition_says()
    {
        var answers = new JsonObject
        {
            ["rezultaty"] = new JsonArray(
                Enumerable.Range(0, AnswerValidator.MaxTableRows + 1)
                    .Select(_ => (JsonNode?)new JsonObject())
                    .ToArray()),
        };

        var result = Validate(FormDefinitionSamples.AllFieldKinds(), answers, AnswerStrictness.Draft);

        Assert.Equal(
            $"Za dużo wierszy: dopuszczalna liczba to {AnswerValidator.MaxTableRows} "
            + $"(jest {AnswerValidator.MaxTableRows + 1}).",
            MessageFor(result, "rezultaty"));
    }

    [Fact]
    public void The_row_count_of_a_growing_table_is_checked_at_submission()
    {
        var answers = CompleteAllFieldKinds();
        answers["rezultaty"] = new JsonArray();

        var draft = Validate(FormDefinitionSamples.AllFieldKinds(), answers, AnswerStrictness.Draft);
        var submission = Validate(
            FormDefinitionSamples.AllFieldKinds(), answers, AnswerStrictness.Submission);

        Assert.True(draft.IsValid);
        Assert.Equal("To pole jest wymagane.", MessageFor(submission, "rezultaty"));
    }

    /// <summary>
    /// A legal form, "fundacja" or "inna"; "inna" shows a text field below it
    /// and a whole section with the statute.
    /// </summary>
    private static System.Text.Json.JsonElement ConditionalForm() =>
        Parse(
            $$"""
            {
              "schemaVersion": 1,
              "sections": [
                {
                  "key": "podmiot",
                  "title": "Podmiot",
                  "fields": [
                    {{FormDefinitionSamples.Field(
                        "forma_prawna",
                        "singleChoice",
                        """
                        "options": [
                          { "value": "fundacja", "label": "Fundacja" },
                          { "value": "inna", "label": "Inna" }
                        ]
                        """)}},
                    {{FormDefinitionSamples.Field(
                        "jaka_forma",
                        "shortText",
                        """
                        "maxLength": 200,
                        "visibleWhen": { "field": "forma_prawna", "equalsAnyOf": ["inna"] }
                        """)}}
                  ]
                },
                {
                  "key": "dokumenty",
                  "title": "Dokumenty",
                  "visibleWhen": { "field": "forma_prawna", "equalsAnyOf": ["inna"] },
                  "fields": [
                    {{FormDefinitionSamples.Field("statut", "longText", "\"maxLength\": 2000")}}
                  ]
                }
              ]
            }
            """);
}
