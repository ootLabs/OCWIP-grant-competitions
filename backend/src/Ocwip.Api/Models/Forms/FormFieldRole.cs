using System.Text.Json;

namespace Ocwip.Api.Models.Forms;

/// <summary>
/// What a field means to the rest of the product, beyond the question it asks
/// (T-35). The operator's list of applications shows the project title, the
/// total cost and the requested grant of every offer, and the keys of a form
/// are slugs an operator chose, so nothing but a marker in the definition can
/// say which field holds which. A marker rather than a reserved key: a typo in
/// a key would leave a column silently empty, a typo in a marker is refused
/// when the form is published.
/// </summary>
public enum FormFieldRole
{
    /// <summary>The field plays no role outside the form.</summary>
    None = 0,

    /// <summary>"Tytuł projektu", part II field 1 in docs/runbook/pola.md.</summary>
    ProjectTitle,

    /// <summary>"Całkowity koszt zadania", the whole budget.</summary>
    TotalCost,

    /// <summary>"Wnioskowana kwota", the grant asked for (D11).</summary>
    RequestedGrant,
}

/// <summary>
/// Reading and checking the role marker. Everything here is about one field,
/// except <see cref="CheckUnique"/>, which needs the whole document: two
/// titles would leave the list guessing which one to show.
/// </summary>
internal static class FormFieldRoles
{
    private static readonly Dictionary<string, FormFieldRole> ByWireName =
        new(StringComparer.Ordinal)
        {
            ["projectTitle"] = FormFieldRole.ProjectTitle,
            ["totalCost"] = FormFieldRole.TotalCost,
            ["requestedGrant"] = FormFieldRole.RequestedGrant,
        };

    public static FormFieldRole Parse(
        FormJsonReader reader,
        JsonElement element,
        string path,
        FormFieldType type,
        string named,
        bool asColumn)
    {
        var name = reader.StringProperty(element, "role", path, required: false);

        if (name is null)
        {
            return FormFieldRole.None;
        }

        var rolePath = $"{path}.role";

        if (!ByWireName.TryGetValue(name, out var role))
        {
            reader.Add(rolePath, $"Pole {named}: rola \"{name}\" nie istnieje w kontrakcie.");
            return FormFieldRole.None;
        }

        if (asColumn)
        {
            reader.Add(
                rolePath,
                $"Kolumna {named} nie może mieć roli: lista wniosków pokazuje jedną "
                + "wartość na wniosek, a kolumna ma ich tyle, ile wierszy.");
            return FormFieldRole.None;
        }

        if (!Fits(role, type, element))
        {
            reader.Add(rolePath, $"Pole {named}: {Requirement(role)}");
            return FormFieldRole.None;
        }

        return role;
    }

    /// <summary>A role is refused on its second field, not its first, so
    /// the message points at the one the operator added last.</summary>
    public static void CheckUnique(FormJsonReader reader, FormDocument document)
    {
        var seen = new HashSet<FormFieldRole>();

        for (var s = 0; s < document.Sections.Count; s++)
        {
            var fields = document.Sections[s].Fields;

            for (var f = 0; f < fields.Count; f++)
            {
                var role = fields[f].Role;

                if (role != FormFieldRole.None && !seen.Add(role))
                {
                    reader.Add(
                        $"$.sections[{s}].fields[{f}].role",
                        $"Pole \"{fields[f].Key}\": rolę \"{WireName(role)}\" ma już inne "
                        + "pole formularza, a każda rola może wystąpić najwyżej raz.");
                }
            }
        }
    }

    public static string WireName(FormFieldRole role) =>
        ByWireName.Single(pair => pair.Value == role).Key;

    /// <summary>
    /// A ratio is a percentage, so a calculated field of that kind would put
    /// "12,5" into a column of amounts. The calculation is read straight from
    /// the element because the parsed one is not built yet at this point.
    /// </summary>
    private static bool Fits(FormFieldRole role, FormFieldType type, JsonElement element) =>
        role switch
        {
            FormFieldRole.ProjectTitle => type == FormFieldType.ShortText,
            _ => type == FormFieldType.Amount
                || (type == FormFieldType.Calculated && !IsRatio(element)),
        };

    /// <summary>Read the way FormFieldParts reads the kind, so "Ratio",
    /// which the parser accepts as a ratio, is not let through here.</summary>
    private static bool IsRatio(JsonElement element) =>
        element.TryGetProperty("calculation", out var calculation)
        && calculation.ValueKind == JsonValueKind.Object
        && calculation.TryGetProperty("kind", out var kind)
        && kind.ValueKind == JsonValueKind.String
        && FormJsonReader.TryParseName<FormCalculationKind>(kind.GetString(), out var parsed)
        && parsed == FormCalculationKind.Ratio;

    private static string Requirement(FormFieldRole role) =>
        role == FormFieldRole.ProjectTitle
            ? "tytuł projektu może nieść tylko pole tekstu krótkiego."
            : "koszt i kwotę dotacji może nieść tylko pole kwoty albo pole "
                + "wyliczane, które nie jest procentem.";
}
