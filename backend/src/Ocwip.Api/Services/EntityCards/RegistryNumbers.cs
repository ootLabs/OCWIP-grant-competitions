namespace Ocwip.Api.Services.EntityCards;

/// <summary>
/// The checksums of the Polish numbers on an organisation card (T-93). The
/// schema does not check them (Entity.Nip, "checked at the API edge"), so this
/// is the only place that does.
///
/// Each method takes the number as typed and answers with the digits to store,
/// or null when the number is not valid. Spaces and hyphens are dropped first,
/// because that is how people copy these numbers from KRS and bank statements.
/// </summary>
internal static class RegistryNumbers
{
    private static readonly int[] NipWeights = [6, 5, 7, 2, 3, 4, 5, 6, 7];
    private static readonly int[] Regon9Weights = [8, 9, 2, 3, 4, 5, 6, 7];
    private static readonly int[] Regon14Weights = [2, 4, 8, 5, 0, 9, 7, 3, 6, 1, 2, 4, 8];

    /// <summary>Ten digits; the weighted sum of the first nine mod 11 is the tenth, and never 10.</summary>
    public static string? Nip(string? typed)
    {
        var digits = Digits(typed, 10);
        if (digits is null)
        {
            return null;
        }

        var check = WeightedSum(digits, NipWeights) % 11;
        return check != 10 && check == digits[9] - '0' ? digits : null;
    }

    /// <summary>9 or 14 digits; mod 11 of the weighted sum, 10 counting as 0, is the last digit.</summary>
    public static string? Regon(string? typed)
    {
        var digits = Normalize(typed);
        var weights = digits.Length switch
        {
            9 => Regon9Weights,
            14 => Regon14Weights,
            _ => null,
        };

        if (weights is null || !digits.All(char.IsAsciiDigit))
        {
            return null;
        }

        var check = WeightedSum(digits, weights) % 11 % 10;
        return check == digits[^1] - '0' ? digits : null;
    }

    /// <summary>
    /// Ten digits. KRS numbers carry no checksum, so the length is all there
    /// is to check; leading zeros are part of the number.
    /// </summary>
    public static string? Krs(string? typed) => Digits(typed, 10);

    /// <summary>
    /// NRB, 26 digits, checked as the Polish IBAN it is: the two check digits
    /// and "PL" (25 21) moved to the end, the whole number mod 97 is 1. A
    /// leading "PL" is accepted and dropped.
    /// </summary>
    public static string? BankAccount(string? typed)
    {
        var normalized = Normalize(typed);
        if (normalized.StartsWith("PL", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..];
        }

        if (normalized.Length != 26 || !normalized.All(char.IsAsciiDigit))
        {
            return null;
        }

        var rearranged = normalized[2..] + "2521" + normalized[..2];
        var remainder = 0;
        foreach (var digit in rearranged)
        {
            remainder = (remainder * 10 + (digit - '0')) % 97;
        }

        return remainder == 1 ? normalized : null;
    }

    private static string? Digits(string? typed, int length)
    {
        var digits = Normalize(typed);
        return digits.Length == length && digits.All(char.IsAsciiDigit) ? digits : null;
    }

    private static string Normalize(string? typed) =>
        new((typed ?? string.Empty).Where(character => character is not (' ' or '-')).ToArray());

    private static int WeightedSum(string digits, int[] weights)
    {
        var sum = 0;
        for (var i = 0; i < weights.Length; i++)
        {
            sum += (digits[i] - '0') * weights[i];
        }

        return sum;
    }
}
