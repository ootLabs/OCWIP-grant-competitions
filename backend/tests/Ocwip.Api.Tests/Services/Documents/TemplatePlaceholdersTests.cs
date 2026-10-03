using Ocwip.Api.Models;
using Ocwip.Api.Services.Documents;
using Xunit;

namespace Ocwip.Api.Tests.Services.Documents;

/// <summary>T-45: what a template asks for, and how a contract prints it.</summary>
public sealed class TemplatePlaceholdersTests
{
    [Fact]
    public void A_known_name_is_filled_by_the_system_and_any_other_is_typed_in()
    {
        var placeholders = TemplatePlaceholders.In("Umowa {{numer_umowy}} z {{nazwa_realizatora}}, konto {{numer_rachunku}}, znów {{numer_umowy}}.");

        Assert.Equal(["numer_umowy", "nazwa_realizatora", "numer_rachunku"], placeholders.Select(x => x.Name));
        Assert.Equal([true, true, false], placeholders.Select(x => x.System));
        Assert.Equal("Numer rachunku", placeholders[2].Label);
    }

    // The label used to be generated from the marker's name, which is ASCII
    // and lower case, so a Polish screen showed "Termin wydatkow" and
    // "Numer umowy niw" (B-GUI-17).
    [Fact]
    public void A_blank_whose_name_does_not_spell_its_label_is_written_in_polish()
    {
        var placeholders = TemplatePlaceholders.In(
            "{{termin_wydatkow}} {{numer_umowy_niw}} {{zrodlo_danych_osobowych}} {{numer_rachunku}}");

        Assert.Equal(
            ["Termin wydatków", "Numer umowy z NIW", "Źródło danych osobowych", "Numer rachunku"],
            placeholders.Select(x => x.Label));
    }

    // A new blank in OCWIP's text still needs no code (D16).
    [Fact]
    public void A_blank_nobody_listed_still_gets_a_label_from_its_name() =>
        Assert.Equal(
            "Numer zarzadzenia",
            Assert.Single(TemplatePlaceholders.In("{{numer_zarzadzenia}}")).Label);

    [Fact]
    public void A_blank_prints_as_a_dotted_line_like_the_paper_template()
    {
        var text = TemplatePlaceholders.Fill("Konto: {{numer_rachunku}}, kwota {{kwota_dotacji}}.",
            new Dictionary<string, string?> { ["kwota_dotacji"] = "6500,00 zł" });

        Assert.Equal($"Konto: {TemplatePlaceholders.Blank}, kwota 6500,00 zł.", text);
    }

    [Theory]
    [InlineData("{{ numer }}")]
    [InlineData("{{Numer}}")]
    [InlineData("{{numer")]
    public void A_brace_that_is_not_a_placeholder_is_refused(string body) =>
        Assert.NotEmpty(TemplatePlaceholders.Problems($"Tekst {body} dalej"));

    // A part of the text can belong to some kinds of applicant only: an
    // informal group has no register and no NIP, and the contract used to
    // demand both of it (znalezisko 11).
    [Fact]
    public void A_part_marked_for_other_kinds_neither_prints_nor_asks_for_anything()
    {
        const string body = "Strona: {{nazwa_realizatora}}"
            + "{{#Organisation,PatronInformalGroup}} wpisana do {{rejestr}} pod numerem {{numer_w_rejestrze}}{{/}}"
            + "{{#InformalGroup}}, grupa nieformalna{{/}}.";

        var organisation = TemplatePlaceholders.In(body, EntityType.Organisation);
        var group = TemplatePlaceholders.In(body, EntityType.InformalGroup);

        Assert.Equal(["nazwa_realizatora", "rejestr", "numer_w_rejestrze"], organisation.Select(x => x.Name));
        Assert.Equal(["nazwa_realizatora"], group.Select(x => x.Name));

        var values = new Dictionary<string, string?> { ["nazwa_realizatora"] = "Grupa Zaodrze" };
        Assert.Equal("Strona: Grupa Zaodrze, grupa nieformalna.",
            TemplatePlaceholders.Fill(body, values, EntityType.InformalGroup));
        Assert.Equal($"Strona: Grupa Zaodrze wpisana do {TemplatePlaceholders.Blank} pod numerem {TemplatePlaceholders.Blank}.",
            TemplatePlaceholders.Fill(body, values, EntityType.Organisation));
    }

    // The editor edits one text for every kind, so it shows the whole text
    // and every blank in it, markers aside.
    [Fact]
    public void Without_a_kind_the_whole_text_and_all_its_blanks_are_there()
    {
        const string body = "A{{#InformalGroup}} lider {{adres_lidera}}{{/}}B";

        Assert.Equal(["adres_lidera"], TemplatePlaceholders.In(body).Select(x => x.Name));
        Assert.Equal("A lider ……………………B", TemplatePlaceholders.Fill(body, new Dictionary<string, string?>()));
    }

    [Theory]
    [InlineData("{{#Fundacja}} tekst {{/}}")]
    [InlineData("{{#Organisation}} tekst")]
    [InlineData("tekst {{/}}")]
    public void A_broken_part_marker_is_refused(string body) =>
        Assert.NotEmpty(TemplatePlaceholders.Problems($"Tekst {body} dalej"));

    [Fact]
    public void A_date_reads_the_way_a_contract_writes_it() =>
        Assert.Equal("5 września 2026 r.", TemplatePlaceholders.DateInWords(new DateOnly(2026, 9, 5)));
}
