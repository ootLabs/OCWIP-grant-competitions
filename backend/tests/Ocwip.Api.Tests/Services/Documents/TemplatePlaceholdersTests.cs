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

    [Fact]
    public void A_date_reads_the_way_a_contract_writes_it() =>
        Assert.Equal("5 września 2026 r.", TemplatePlaceholders.DateInWords(new DateOnly(2026, 9, 5)));
}
