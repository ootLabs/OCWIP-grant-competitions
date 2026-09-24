using Ocwip.Api.Models.Forms;
using Xunit;

namespace Ocwip.Api.Tests.Models.Forms;

/// <summary>
/// The strings formatAmount and formatPercent write in frontend/lib/format.ts
/// (Intl.NumberFormat with "pl-PL"), so a limit message reads the same from
/// the form and from the server.
/// </summary>
public sealed class PolishNumbersTests
{
    [Theory]
    [InlineData("900", "900,00 zł")]
    [InlineData("9000", "9000,00 zł")]
    [InlineData("12500.5", "12 500,50 zł")]
    [InlineData("1234567.891", "1 234 567,89 zł")]
    [InlineData("0.005", "0,01 zł")]
    public void An_amount_groups_digits_from_five_up_and_keeps_two_decimals(
        string amount, string expected)
    {
        var text = PolishNumbers.Amount(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(expected, text.Replace(' ', ' '));
    }

    [Theory]
    [InlineData("10", "10%")]
    [InlineData("12.5", "12,5%")]
    [InlineData("33.3333", "33,33%")]
    public void A_percentage_has_no_decimals_it_does_not_need(string value, string expected)
    {
        Assert.Equal(
            expected,
            PolishNumbers.Percent(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)));
    }
}
