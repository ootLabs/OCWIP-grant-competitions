namespace Ocwip.Api.Services;

/// <summary>
/// The three Polish number forms, for any count a mail prints next to a noun.
///
/// The verification and reset mails said "ważny przez 24 godzin" and "przez 1
/// godzin" (P4-04): the count comes from configuration and the noun was fixed.
/// Polish wants "1 godzinę", "2 do 4 godziny", "5 do 21 godzin", "22 godziny".
/// The same rule as the frontend's pluralForm and as AmountInWords.
/// </summary>
internal static class PolishPlural
{
    public static string Choose(long n, string one, string few, string many)
    {
        if (n == 1)
        {
            return one;
        }

        var lastTwo = Math.Abs(n % 100);
        var lastOne = Math.Abs(n % 10);
        return lastOne is >= 2 and <= 4 && lastTwo is not (>= 12 and <= 14) ? few : many;
    }

    /// <summary>"24 godziny", in the accusative a "ważny przez" sentence needs.</summary>
    public static string Hours(int n) => $"{n} {Choose(n, "godzinę", "godziny", "godzin")}";
}
