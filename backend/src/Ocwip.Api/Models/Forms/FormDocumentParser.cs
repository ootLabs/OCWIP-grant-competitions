using System.Text.Json;

namespace Ocwip.Api.Models.Forms;

/// <summary>
/// Reads the outline of a definition: the root, the schema version, the
/// sections and their fields (T-24).
/// </summary>
internal static class FormDocumentParser
{
    /// <summary>
    /// Ceilings on the outline. Nothing else bounds them, and a definition
    /// with ten thousand sections is a way to make one save render forever.
    /// The 2026 template has four parts and about sixty fields.
    /// </summary>
    private const int MaxSections = 100;

    private const int MaxFieldsPerSection = 500;

    public static FormDocument? Parse(FormJsonReader reader, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            reader.Add(
                "$",
                "Korzeń definicji formularza musi być obiektem z wersją kontraktu "
                + "i listą sekcji.");
            return null;
        }

        var version = reader.IntegerProperty(root, "schemaVersion", "$");

        if (version is null)
        {
            reader.Add("$.schemaVersion", "Definicja nie podaje wersji kontraktu.");
        }
        else if (version != FormDocument.CurrentSchemaVersion)
        {
            reader.Add(
                "$.schemaVersion",
                $"Wersja kontraktu {version} nie jest obsługiwana: ta wersja "
                + $"systemu czyta wersję {FormDocument.CurrentSchemaVersion}.");
        }

        var sections = Sections(reader, root);

        return sections is null
            ? null
            : new FormDocument(FormDocument.CurrentSchemaVersion, sections);
    }

    private static IReadOnlyList<FormSection>? Sections(
        FormJsonReader reader,
        JsonElement root)
    {
        var raw = reader.ArrayProperty(root, "sections", "$", required: true);

        if (raw.Count == 0)
        {
            reader.Add("$.sections", "Formularz bez ani jednej sekcji nie istnieje.");
            return null;
        }

        if (raw.Count > MaxSections)
        {
            reader.Add("$.sections", $"Formularz ma więcej niż {MaxSections} sekcji.");
            return null;
        }

        var sections = new List<FormSection>(raw.Count);
        var seenSections = new HashSet<string>(StringComparer.Ordinal);
        var seenFields = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < raw.Count; index++)
        {
            var path = $"$.sections[{index}]";
            var section = ParseSection(reader, raw[index], path, seenFields);

            if (section is null)
            {
                continue;
            }

            if (!seenSections.Add(section.Key))
            {
                reader.Add(
                    $"{path}.key",
                    $"Dwie sekcje mają klucz \"{section.Key}\".");
                continue;
            }

            sections.Add(section);
        }

        return sections;
    }

    private static FormSection? ParseSection(
        FormJsonReader reader,
        JsonElement element,
        string path,
        HashSet<string> seenFields)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            reader.Add(path, "Sekcja musi być obiektem.");
            return null;
        }

        var key = reader.StringProperty(element, "key", path, required: true);

        if (key is not null && !FormJsonReader.IsValidKey(key))
        {
            reader.Add(
                $"{path}.key",
                $"Klucz sekcji \"{key}\" ma niedozwoloną postać.");
            key = null;
        }

        var title = reader.StringProperty(element, "title", path, required: true);
        var description = reader.StringProperty(element, "description", path, false);
        var condition = FormFieldParser.Condition(reader, element, path);
        var fields = Fields(reader, element, path, seenFields);

        return key is null || title is null
            ? null
            : new FormSection(key, title, description, condition, fields);
    }

    private static IReadOnlyList<FormField> Fields(
        FormJsonReader reader,
        JsonElement element,
        string path,
        HashSet<string> seenFields)
    {
        var raw = reader.ArrayProperty(element, "fields", path, required: true);

        if (raw.Count == 0)
        {
            reader.Add($"{path}.fields", "Sekcja bez ani jednego pola nie ma treści.");
            return [];
        }

        if (raw.Count > MaxFieldsPerSection)
        {
            reader.Add(
                $"{path}.fields",
                $"Sekcja ma więcej niż {MaxFieldsPerSection} pól.");
            return [];
        }

        var fields = new List<FormField>(raw.Count);

        for (var index = 0; index < raw.Count; index++)
        {
            var fieldPath = $"{path}.fields[{index}]";
            var field = FormFieldParser.Parse(reader, raw[index], fieldPath, false);

            if (field is null)
            {
                continue;
            }

            if (!seenFields.Add(field.Key))
            {
                reader.Add(
                    $"{fieldPath}.key",
                    $"Klucz \"{field.Key}\" jest użyty drugi raz: odpowiedzi na "
                    + "dwa pola o tym samym kluczu nie da się rozróżnić.");
                continue;
            }

            fields.Add(field);
        }

        return fields;
    }
}
