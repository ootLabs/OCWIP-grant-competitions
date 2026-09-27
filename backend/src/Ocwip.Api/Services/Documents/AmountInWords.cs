namespace Ocwip.Api.Services.Documents;

/// <summary>
/// An amount in Polish words, for a contract (T-45: "kwota słownie"). The
/// same rules as frontend/lib/amount-in-words.ts, the wizard's preview of the
/// same words, so what the operator saw typed is what the contract prints.
/// </summary>
internal static class AmountInWords
{
    private static readonly string[] Ones = ["zero", "jeden", "dwa", "trzy", "cztery", "pięć", "sześć", "siedem", "osiem", "dziewięć"];
    private static readonly string[] Teen =
    [
        "dziesięć", "jedenaście", "dwanaście", "trzynaście", "czternaście",
        "piętnaście", "szesnaście", "siedemnaście", "osiemnaście", "dziewiętnaście",
    ];
    private static readonly string[] Tens =
    [
        "", "", "dwadzieścia", "trzydzieści", "czterdzieści", "pięćdziesiąt",
        "sześćdziesiąt", "siedemdziesiąt", "osiemdziesiąt", "dziewięćdziesiąt",
    ];
    private static readonly string[] Hundreds =
    [
        "", "sto", "dwieście", "trzysta", "czterysta", "pięćset", "sześćset", "siedemset", "osiemset", "dziewięćset",
    ];

    private static readonly (string One, string Few, string Many) Thousand = ("tysiąc", "tysiące", "tysięcy");
    private static readonly (string One, string Few, string Many) Million = ("milion", "miliony", "milionów");
    private static readonly (string One, string Few, string Many) Zloty = ("złoty", "złote", "złotych");
    private static readonly (string One, string Few, string Many) Grosz = ("grosz", "grosze", "groszy");

    /// <summary>"sto dwadzieścia tysięcy złotych", or with grosze "sto złotych i pięćdziesiąt groszy".</summary>
    public static string Of(decimal amount)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Kwota słownie wymaga kwoty nieujemnej.");
        }

        var totalGrosze = (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);
        var zloty = totalGrosze / 100;
        var grosze = totalGrosze % 100;

        var phrase = $"{Integer(zloty)} {Word(Zloty, zloty)}";
        return grosze == 0 ? phrase : $"{phrase} i {Integer(grosze)} {Word(Grosz, grosze)}";
    }

    public static string Integer(long value)
    {
        if (value is < 0 or >= 1_000_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Liczba słownie obsługuje liczby od zera do miliarda.");
        }

        if (value == 0)
        {
            return "zero";
        }

        var millions = value / 1_000_000;
        var thousands = value % 1_000_000 / 1000;
        var rest = value % 1000;
        var groups = new List<string>();

        // "jeden milion" and "jeden tysiąc" are not said: a lone group is the scale word alone.
        if (millions > 0)
        {
            groups.Add(millions == 1 ? Word(Million, 1) : $"{BelowThousand(millions)} {Word(Million, millions)}");
        }

        if (thousands > 0)
        {
            groups.Add(thousands == 1 ? Word(Thousand, 1) : $"{BelowThousand(thousands)} {Word(Thousand, thousands)}");
        }

        if (rest > 0)
        {
            groups.Add(BelowThousand(rest));
        }

        return string.Join(' ', groups);
    }

    private static string BelowThousand(long n)
    {
        var parts = new List<string>();
        var rest = n % 100;

        if (n / 100 > 0)
        {
            parts.Add(Hundreds[n / 100]);
        }

        if (rest is >= 10 and <= 19)
        {
            parts.Add(Teen[rest - 10]);
        }
        else
        {
            if (rest / 10 >= 2)
            {
                parts.Add(Tens[rest / 10]);
            }

            if (rest % 10 > 0)
            {
                parts.Add(Ones[rest % 10]);
            }
        }

        return string.Join(' ', parts);
    }

    private static string Word((string One, string Few, string Many) forms, long n)
    {
        if (n == 1)
        {
            return forms.One;
        }

        var lastTwo = n % 100;
        var lastOne = n % 10;
        return lastOne is >= 2 and <= 4 && lastTwo is not (>= 12 and <= 14) ? forms.Few : forms.Many;
    }
}
