using System.Globalization;
using System.Text.RegularExpressions;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services.Documents;

/// <summary>One placeholder of a template: filled by the system, or typed by the operator.</summary>
public sealed record TemplatePlaceholder(string Name, string Label, bool System);

/// <summary>
/// The {{placeholders}} of a document template (T-45). A fixed dictionary of
/// what the system knows (the application, its entity, the grant, the
/// competition); any other name in a template is a value the operator types
/// in for each contract. So the template decides what it asks for, and a new
/// blank in OCWIP's text needs no code (D16).
///
/// A part of the text may belong to some kinds of applicant only:
///
///     {{#Organisation,PatronInformalGroup}} wpisaną do {{rejestr}}{{/}}
///
/// For every other kind that part is not printed and its blanks are neither
/// asked for nor required to sign (T-45, znalezisko 11). The kinds are the
/// EntityType names, the same words the application form's visibleWhen and
/// the evaluation card's appliesTo use, so one concept has one spelling
/// across the three places that condition on it. An informal group has no
/// register and no NIP, and the contract used to demand both: the operator
/// typed "nie dotyczy" into a contract somebody signs.
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

    /// <summary>
    /// Polish spelling for the blanks of the templates OCWIP actually uses,
    /// where the name of the marker does not spell the label: a name is
    /// ASCII and lower case, so "{{termin_wydatkow}}" generated "Termin
    /// wydatkow" and "{{numer_umowy_niw}}" generated "Numer umowy niw" on a
    /// screen that is otherwise in Polish (B-GUI-17).
    ///
    /// A spelling aid, not a schema: a name that is not here still becomes a
    /// blank with a label generated from it, so a new blank in OCWIP's text
    /// needs no code (D16). Only the names whose generated label is wrong
    /// belong here, so the list stays short enough to read.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> BlankLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["numer_umowy_niw"] = "Numer umowy z NIW",
        ["data_umowy_niw"] = "Data umowy z NIW",
        ["termin_wydatkow"] = "Termin wydatków",
        ["zrodlo_danych_osobowych"] = "Źródło danych osobowych",
        ["email_kontaktowy"] = "E-mail kontaktowy",
        ["adres_lidera"] = "Adres lidera grupy",
    };

    private static readonly string[] Months =
    [
        "stycznia", "lutego", "marca", "kwietnia", "maja", "czerwca",
        "lipca", "sierpnia", "września", "października", "listopada", "grudnia",
    ];

    /// <summary>
    /// Every placeholder in the order it first appears, each once. Without an
    /// applicant kind (the template editor, which edits one text for all of
    /// them) that is every placeholder in the text; with one, only those that
    /// apply to it.
    /// </summary>
    public static IReadOnlyList<TemplatePlaceholder> In(string body, EntityType? applicant = null) =>
        Placeholder().Matches(For(body, applicant))
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Select(name => SystemLabels.TryGetValue(name, out var label)
                ? new TemplatePlaceholder(name, label, System: true)
                : new TemplatePlaceholder(name, Label(name), System: false))
            .ToList();

    /// <summary>
    /// What is wrong with a template, in Polish: a brace pair that is not a
    /// placeholder ("{{ Nazwa }}", "{{nazwa"), which would otherwise print as
    /// it is in a contract somebody signs, and a part marked for a kind of
    /// applicant that does not exist or never closes.
    /// </summary>
    public static IReadOnlyList<string> Problems(string body)
    {
        var problems = new List<string>();

        foreach (var kind in Opening().Matches(body)
            .SelectMany(match => match.Groups[1].Value.Split(','))
            .Select(name => name.Trim())
            .Where(name => !Enum.TryParse<EntityType>(name, ignoreCase: false, out _))
            .Distinct(StringComparer.Ordinal))
        {
            problems.Add($"We wzorze jest część oznaczona dla \"{kind}\", a tego rodzaju wnioskodawcy nie ma. "
                + $"Rodzaje to: {string.Join(", ", Enum.GetNames<EntityType>())}.");
        }

        // Sections away first: what is left has to hold no marker at all, so
        // an opening without its {{/}} and a stray {{/}} both show up here.
        var remainder = Section().Replace(body, string.Empty);

        if (Opening().IsMatch(remainder) || remainder.Contains("{{/}}", StringComparison.Ordinal))
        {
            problems.Add("We wzorze jest część oznaczona dla rodzaju wnioskodawcy, która się nie domyka. "
                + "Każde {{#Rodzaj}} potrzebuje {{/}} dalej w tekście, bez zagnieżdżania.");
        }

        remainder = Placeholder().Replace(remainder, string.Empty);

        if (remainder.Contains("{{", StringComparison.Ordinal) || remainder.Contains("}}", StringComparison.Ordinal))
        {
            problems.Add("We wzorze jest nawias {{ albo }}, który nie tworzy znacznika. Znacznik to małe litery, cyfry "
                + "i podkreślenia w podwójnych nawiasach, bez spacji, na przykład {{numer_rachunku}}.");
        }

        return problems;
    }

    /// <summary>
    /// The body as this kind of applicant's contract reads: the parts marked
    /// for other kinds gone, the markers themselves gone either way. Without
    /// a kind, only the markers go, so the editor shows the whole text.
    /// </summary>
    public static string For(string body, EntityType? applicant)
    {
        var kept = Section().Replace(body, match =>
            applicant is null || Applies(match.Groups[1].Value, applicant.Value)
                ? match.Groups[2].Value
                : string.Empty);

        // A marker that never closes is refused at publication; in a stored
        // template it would print as it is, so it goes as well.
        return Opening().Replace(kept, string.Empty).Replace("{{/}}", string.Empty, StringComparison.Ordinal);
    }

    /// <summary>The body with every placeholder replaced; one without a value prints as a dotted blank.</summary>
    public static string Fill(
        string body, IReadOnlyDictionary<string, string?> values, EntityType? applicant = null) =>
        Placeholder().Replace(For(body, applicant), match =>
            values.TryGetValue(match.Groups[1].Value, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value.Trim()
                : Blank);

    private static bool Applies(string kinds, EntityType applicant) =>
        kinds.Split(',').Any(name => string.Equals(name.Trim(), applicant.ToString(), StringComparison.Ordinal));

    /// <summary>"26 września 2026 r.", the way a contract writes a date.</summary>
    public static string DateInWords(DateOnly date) =>
        $"{date.Day.ToString(CultureInfo.InvariantCulture)} {Months[date.Month - 1]} {date.Year.ToString(CultureInfo.InvariantCulture)} r.";

    /// <summary>
    /// A name the operator reads: the Polish spelling from
    /// <see cref="BlankLabels"/>, otherwise generated from the name itself
    /// ("numer_rachunku" becomes "Numer rachunku").
    /// </summary>
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
        if (BlankLabels.TryGetValue(name, out var written))
        {
            return written;
        }

        var words = name.Replace('_', ' ');
        return char.ToUpperInvariant(words[0]) + words[1..];
    }

    [GeneratedRegex(@"\{\{([a-z][a-z0-9_]*)\}\}")]
    private static partial Regex Placeholder();

    /// <summary>Not greedy and over line ends: a clause of one line, a paragraph of several.</summary>
    [GeneratedRegex(@"\{\{#([A-Za-z]+(?:\s*,\s*[A-Za-z]+)*)\}\}(.*?)\{\{/\}\}", RegexOptions.Singleline)]
    private static partial Regex Section();

    [GeneratedRegex(@"\{\{#([A-Za-z]+(?:\s*,\s*[A-Za-z]+)*)\}\}")]
    private static partial Regex Opening();
}
