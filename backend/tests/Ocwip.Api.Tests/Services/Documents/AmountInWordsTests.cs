using Ocwip.Api.Services.Documents;
using Xunit;

namespace Ocwip.Api.Tests.Services.Documents;

/// <summary>T-45: the same words as frontend/lib/amount-in-words.ts, checked on the same kinds of numbers.</summary>
public sealed class AmountInWordsTests
{
    [Theory]
    [InlineData(0, "zero")]
    [InlineData(15, "piętnaście")]
    [InlineData(21, "dwadzieścia jeden")]
    [InlineData(1000, "tysiąc")]
    [InlineData(2004, "dwa tysiące cztery")]
    [InlineData(12000, "dwanaście tysięcy")]
    [InlineData(1_000_000, "milion")]
    [InlineData(123_456_789, "sto dwadzieścia trzy miliony czterysta pięćdziesiąt sześć tysięcy siedemset osiemdziesiąt dziewięć")]
    public void An_integer_reads_as_it_is_said(long value, string words) =>
        Assert.Equal(words, AmountInWords.Integer(value));

    [Theory]
    [InlineData("7000", "siedem tysięcy złotych")]
    [InlineData("1", "jeden złoty")]
    [InlineData("22", "dwadzieścia dwa złote")]
    [InlineData("6500.50", "sześć tysięcy pięćset złotych i pięćdziesiąt groszy")]
    [InlineData("12.005", "dwanaście złotych i jeden grosz")]
    public void An_amount_reads_with_zloty_and_grosze(string amount, string words) =>
        Assert.Equal(words, AmountInWords.Of(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));
}
