using System.Globalization;

namespace Ocwip.Api.Models.Forms;

/// <summary>
/// Amounts and percentages as the form prints them next to a field (T-30),
/// the same strings formatAmount and formatPercent write in
/// frontend/lib/format.ts: "9000,00 zł", "12 500,50 zł", "10%".
///
/// Written out by hand rather than through the "pl-PL" culture, for the
/// reason CompetitionIntakeMessage formats dates with the invariant one: a
/// container image without ICU data does not fail, it quietly formats in the
/// invariant culture, and a limit message reading "9,000.00" in a Polish form
/// is the kind of difference a person notices at once.
/// </summary>
internal static class PolishNumbers
{
    /// <summary>
    /// What the browser puts between digit groups and before "zł" in Polish,
    /// so the amount never breaks across two lines of the message.
    /// </summary>
    private const char NoBreakSpace = ' ';

    /// <summary>
    /// Polish groups digits only from five of them up ("9000", "10 000"),
    /// which is what Intl.NumberFormat does for "pl-PL".
    /// </summary>
    private const int MinimumGroupedDigits = 5;

    public static string Amount(decimal amount)
    {
        var rounded = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        var text = Math.Abs(rounded).ToString("0.00", CultureInfo.InvariantCulture);
        var separator = text.IndexOf('.');
        var whole = Group(text[..separator]);
        var sign = rounded < 0 ? "-" : string.Empty;

        return $"{sign}{whole},{text[(separator + 1)..]}{NoBreakSpace}zł";
    }

    /// <summary>"10%", "12,5%": at most two decimals, none when there are none.</summary>
    public static string Percent(decimal value) => Plain(value, 2) + "%";

    /// <summary>A bound from the definition, such as a minimum of 0,5.</summary>
    public static string Number(decimal value) => Plain(value, 10);

    private static string Plain(decimal value, int decimals)
    {
        var rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        var text = Math.Abs(rounded).ToString(
            "0." + new string('#', decimals),
            CultureInfo.InvariantCulture);
        var separator = text.IndexOf('.');
        var whole = Group(separator < 0 ? text : text[..separator]);
        var fraction = separator < 0 ? string.Empty : "," + text[(separator + 1)..];
        var sign = rounded < 0 ? "-" : string.Empty;

        return sign + whole + fraction;
    }

    private static string Group(string digits)
    {
        if (digits.Length < MinimumGroupedDigits)
        {
            return digits;
        }

        var grouped = new System.Text.StringBuilder(digits.Length + digits.Length / 3);

        for (var index = 0; index < digits.Length; index++)
        {
            if (index > 0 && (digits.Length - index) % 3 == 0)
            {
                grouped.Append(NoBreakSpace);
            }

            grouped.Append(digits[index]);
        }

        return grouped.ToString();
    }
}
