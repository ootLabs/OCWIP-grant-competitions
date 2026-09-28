using System.Globalization;
using System.Text.RegularExpressions;

namespace Ocwip.Api.Services.Documents;

/// <summary>One placeholder of a template: filled by the system, or typed by the operator.</summary>
public sealed record TemplatePlaceholder(string Name, string Label, bool System);

/// <summary>
/// The {{placeholders}} of a document template (T-45). A fixed dictionary of
/// what the system knows (the application, its entity, the grant, the
/// competition); any other name in a template is a value the operator types
/// in for each contract. So the template decides what it asks for, and a new
/// blank in OCWIP's text needs no code (D16).
/// </summary>
internal static partial class TemplatePlaceholders
{
    /// <summary>What a blank looks like in a printed contract, like the dotted lines of the paper template.</summary>
    public const string Blank = "……………………";

    public static readonly IReadOnlyDictionary<string, string> SystemLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["numer_umowy"] = "Numer umowy (numer wniosku)",
        ["data_zawarcia"] = "Data zawarcia umowy słownie (data podpisania)",
        ["numer_wniosku"] = "Numer wniosku",
        ["data_zlozenia_wniosku"] = "Data złożenia wniosku słownie",
        ["nazwa_realizatora"] = "Nazwa wnioskodawcy",
        ["nip"] = "NIP wnioskodawcy",
        ["adres"] = "Adres wnioskodawcy",
        ["tytul_projektu"] = "Tytuł projektu",
        ["koszt_calkowity"] = "Całkowity koszt projektu",
        ["kwota_wnioskowana"] = "Kwota wnioskowana",
        ["kwota_dotacji"] = "Kwota przyznanej dotacji",
        ["kwota_dotacji_slownie"] = "Kwota przyznanej dotacji słownie",
        ["numer_konkursu"] = "Numer konkursu",
        ["tytul_konkursu"] = "Nazwa konkursu",
        ["czlonkowie_grupy"] = "Członkowie grupy nieformalnej (z wniosku)",
    };

    private static readonly string[] Months =
    [
        "stycznia", "lutego", "marca", "kwietnia", "maja", "czerwca",
        "lipca", "sierpnia", "września", "października", "listopada", "grudnia",
    ];

    /// <summary>Every placeholder in the order it first appears, each once.</summary>
    public static IReadOnlyList<TemplatePlaceholder> In(string body) =>
        Placeholder().Matches(body)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Select(name => SystemLabels.TryGetValue(name, out var label)
                ? new TemplatePlaceholder(name, label, System: true)
                : new TemplatePlaceholder(name, Label(name), System: false))
            .ToList();

    /// <summary>
    /// What is wrong with a template, in Polish: a brace pair that is not a
    /// placeholder ("{{ Nazwa }}", "{{nazwa"), which would otherwise print as
    /// it is in a contract somebody signs.
    /// </summary>
    public static IReadOnlyList<string> Problems(string body)
    {
        var problems = new List<string>();
        var remainder = Placeholder().Replace(body, string.Empty);

        if (remainder.Contains("{{", StringComparison.Ordinal) || remainder.Contains("}}", StringComparison.Ordinal))
        {
            problems.Add("We wzorze jest nawias {{ albo }}, który nie tworzy znacznika. Znacznik to małe litery, cyfry "
                + "i podkreślenia w podwójnych nawiasach, bez spacji, na przykład {{numer_rachunku}}.");
        }

        return problems;
    }

    /// <summary>The body with every placeholder replaced; one without a value prints as a dotted blank.</summary>
    public static string Fill(string body, IReadOnlyDictionary<string, string?> values) =>
        Placeholder().Replace(body, match =>
            values.TryGetValue(match.Groups[1].Value, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : Blank);

    /// <summary>"26 września 2026 r.", the way a contract writes a date.</summary>
    public static string DateInWords(DateOnly date) =>
        $"{date.Day.ToString(CultureInfo.InvariantCulture)} {Months[date.Month - 1]} {date.Year.ToString(CultureInfo.InvariantCulture)} r.";

    /// <summary>A name the operator reads: "numer_rachunku" becomes "Numer rachunku".</summary>
    /// <summary>
    /// Sensitive Information (T-47a): a blank whose name says PESEL, such as
    /// {{pesel_skarbnika}}. Its value is shown masked on every screen and in
    /// full only in the contract itself. Every operator value is encrypted
    /// in the database either way (Data/Configurations/ContractConfiguration.cs).
    /// </summary>
    public static bool IsPesel(string name) => name.Contains("pesel", StringComparison.Ordinal);

    /// <summary>The last four characters, the rest as stars: enough to tell two people apart, not to use the number.</summary>
    public static string Mask(string value) =>
        value.Length <= 4 ? new string('*', value.Length) : new string('*', value.Length - 4) + value[^4..];

    private static string Label(string name)
    {
        var words = name.Replace('_', ' ');
        return char.ToUpperInvariant(words[0]) + words[1..];
    }

    [GeneratedRegex(@"\{\{([a-z][a-z0-9_]*)\}\}")]
    private static partial Regex Placeholder();
}
