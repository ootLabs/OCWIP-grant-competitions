using System.Text.Json;

namespace Ocwip.Api.Models.Forms;

/// <summary>
/// The two properties an evaluation card needs and an application form does
/// not (T-38): which kinds of applicant a field is asked about, and what a
/// "yes" is worth. Read here for every document; whether the document may
/// carry them at all is FormPurposeRules' question.
/// </summary>
internal static class FormEvaluationParts
{
    /// <summary>
    /// Spelled exactly as EntityType travels over the API, not in the
    /// camelCase the rest of the contract uses: a second spelling of the same
    /// enum is the drift R-34 describes between the backend and the front.
    /// </summary>
    private static readonly string[] ApplicantKinds = Enum.GetNames<EntityType>();

    public static IReadOnlyList<EntityType>? AppliesTo(
        FormJsonReader reader,
        JsonElement element,
        string path,
        string named,
        bool asColumn)
    {
        if (!element.TryGetProperty("appliesTo", out var raw) || raw.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var propertyPath = $"{path}.appliesTo";

        if (asColumn)
        {
            reader.Add(
                propertyPath,
                $"Kolumna {named} nie może mieć \"appliesTo\": komu pokazać pytanie, "
                + "decyduje całe pole, nie jedna kolumna tabeli.");
            return null;
        }

        var items = reader.ArrayProperty(element, "appliesTo", path, required: false);

        if (raw.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        if (items.Count == 0)
        {
            reader.Add(
                propertyPath,
                $"Pole {named}: pusta lista \"appliesTo\" ukryłaby pole przed wszystkimi. "
                + "Pole zadawane każdemu nie ma tej właściwości wcale.");
            return null;
        }

        var kinds = new List<EntityType>(items.Count);

        for (var i = 0; i < items.Count; i++)
        {
            var name = items[i].ValueKind == JsonValueKind.String ? items[i].GetString() : null;

            if (name is null || !ApplicantKinds.Contains(name, StringComparer.Ordinal))
            {
                reader.Add(
                    $"{propertyPath}[{i}]",
                    $"Pole {named}: nie ma rodzaju wnioskodawcy \"{name ?? items[i].ToString()}\". "
                    + $"Dozwolone: {string.Join(", ", ApplicantKinds)}.");
                continue;
            }

            var kind = Enum.Parse<EntityType>(name);

            if (kinds.Contains(kind))
            {
                reader.Add(
                    $"{propertyPath}[{i}]",
                    $"Pole {named}: rodzaj wnioskodawcy \"{name}\" powtarza się na liście.");
                continue;
            }

            kinds.Add(kind);
        }

        return kinds;
    }

    public static decimal? Points(
        FormJsonReader reader,
        JsonElement element,
        string path,
        FormFieldType type,
        string named,
        bool asColumn)
    {
        var points = reader.DecimalProperty(element, "points", path);

        if (points is null)
        {
            return null;
        }

        var propertyPath = $"{path}.points";

        if (type != FormFieldType.YesNo || asColumn)
        {
            reader.Add(
                propertyPath,
                $"Pole {named}: punkty za odpowiedź \"tak\" może nieść tylko pole "
                + "tak albo nie stojące poza tabelą.");
            return null;
        }

        if (points <= 0m)
        {
            reader.Add(propertyPath, $"Pole {named}: punkty za \"tak\" muszą być większe od zera.");
            return null;
        }

        return points;
    }
}
