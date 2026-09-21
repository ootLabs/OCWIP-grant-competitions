using System.Text.Json;

namespace Ocwip.Api.Models.Forms;

/// <summary>
/// Turns one JSON object into a <see cref="FormField"/>, refusing what the
/// renderer would not know how to draw (T-24).
///
/// Everything here is answerable by looking at the field alone. Whether the
/// field a condition points at exists, and whether a calculation runs in a
/// circle, needs the whole document and lives in
/// <see cref="FormSchemaReferences"/>.
/// </summary>
internal static class FormFieldParser
{
    public static FormField? Parse(
        FormJsonReader reader,
        JsonElement element,
        string path,
        bool asColumn)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            reader.Add(path, "Pole musi być obiektem.");
            return null;
        }

        var key = reader.StringProperty(element, "key", path, required: true);

        if (key is not null && !FormJsonReader.IsValidKey(key))
        {
            reader.Add(
                $"{path}.key",
                $"Klucz \"{key}\" ma niedozwoloną postać: dozwolone są małe litery, "
                + "cyfry i podkreślenia, a pierwszy znak musi być literą.");
            key = null;
        }

        var named = key is null ? "bez klucza" : $"\"{key}\"";
        var type = ParseType(reader, element, path, named, asColumn);

        if (type == FormFieldType.Unknown)
        {
            // Without a kind, every check below asks a question that has no
            // answer, and the operator would get a list of refusals about
            // properties that are correct on top of the one real reason.
            return null;
        }

        var field = new FormField(
            Key: key ?? string.Empty,
            Type: type,
            Label: reader.StringProperty(element, "label", path, required: true)
                ?? string.Empty,
            Help: reader.StringProperty(element, "help", path, required: false),
            Required: reader.BooleanProperty(element, "required", path, required: true),
            Printed: reader.BooleanProperty(element, "printed", path, required: true),
            MaxLength: reader.IntegerProperty(element, "maxLength", path),
            MinLength: reader.IntegerProperty(element, "minLength", path),
            MinValue: reader.DecimalProperty(element, "minValue", path),
            MaxValue: reader.DecimalProperty(element, "maxValue", path),
            VisibleWhen: Condition(reader, element, path),
            Options: FormFieldParts.Options(reader, element, path, type, named),
            Table: asColumn
                ? null
                : FormFieldParts.Table(reader, element, path, type, named),
            Calculation: FormFieldParts.Calculation(reader, element, path, type, named),
            Limits: FormFieldParts.Limits(reader, element, path, type, named),
            File: FormFieldParts.File(reader, element, path, type, named),
            StatementText: ParseStatementText(reader, element, path, type, named));

        CheckLengths(reader, field, path, named);
        CheckRange(reader, field, path, named);

        if (asColumn && element.TryGetProperty("table", out _))
        {
            reader.Add(
                $"{path}.table",
                $"Kolumna {named} nie może być tabelą: tabela w tabeli nie ma jak "
                + "zostać wyświetlona.");
        }

        return key is null || type == FormFieldType.Unknown ? null : field;
    }

    private static FormFieldType ParseType(
        FormJsonReader reader,
        JsonElement element,
        string path,
        string named,
        bool asColumn)
    {
        var name = reader.StringProperty(element, "type", path, required: true);

        if (name is null)
        {
            return FormFieldType.Unknown;
        }

        if (!FormFieldTypes.TryParse(name, out var type))
        {
            reader.Add(
                $"{path}.type",
                $"Pole {named}: rodzaj \"{name}\" nie istnieje w kontrakcie, "
                + "więc formularza nie da się wyświetlić.");
            return FormFieldType.Unknown;
        }

        if (asColumn && !FormFieldTypes.IsAllowedInTable(type))
        {
            reader.Add(
                $"{path}.type",
                $"Kolumna {named}: rodzaj \"{name}\" nie może stać w wierszu tabeli.");
            return FormFieldType.Unknown;
        }

        return type;
    }

    /// <summary>
    /// A section carries the same condition shape as a field, so both read it
    /// through here: two readers would be two ways of writing one rule, and
    /// the creator would end up teaching an operator both.
    /// </summary>
    public static FormCondition? Condition(
        FormJsonReader reader,
        JsonElement element,
        string path)
    {
        var condition = reader.ObjectProperty(element, "visibleWhen", path, required: false);

        if (condition is null)
        {
            return null;
        }

        var conditionPath = $"{path}.visibleWhen";
        var field = reader.StringProperty(
            condition.Value,
            "field",
            conditionPath,
            required: true);

        var values = reader.ComparableValues(
            reader.ArrayProperty(
                condition.Value,
                "equalsAnyOf",
                conditionPath,
                required: true),
            $"{conditionPath}.equalsAnyOf");

        if (values.Count == 0)
        {
            reader.Add(
                $"{conditionPath}.equalsAnyOf",
                "Warunek widoczności bez wartości nigdy nie zostanie spełniony.");
        }

        return field is null ? null : new FormCondition(field, values);
    }

    private static string? ParseStatementText(
        FormJsonReader reader,
        JsonElement element,
        string path,
        FormFieldType type,
        string named)
    {
        var text = reader.StringProperty(
            element,
            "statementText",
            path,
            required: type == FormFieldType.Statement);

        if (type != FormFieldType.Statement && text is not null)
        {
            reader.Add(
                $"{path}.statementText",
                $"Pole {named} nie jest oświadczeniem, więc nie może nieść jego treści.");
        }

        return text;
    }

    private static void CheckLengths(
        FormJsonReader reader,
        FormField field,
        string path,
        string named)
    {
        var textual = FormFieldTypes.IsTextual(field.Type);

        if (textual && field.MaxLength is null)
        {
            reader.Add(
                $"{path}.maxLength",
                $"Pole {named} jest polem tekstowym, więc musi mieć limit znaków: "
                + "bez niego renderer nie ma czego pokazać w liczniku.");
        }

        if (!textual && (field.MaxLength is not null || field.MinLength is not null))
        {
            reader.Add(
                $"{path}.maxLength",
                $"Pole {named} nie jest polem tekstowym, więc limit znaków nic tu "
                + "nie znaczy.");
        }

        if (field.MaxLength is 0)
        {
            reader.Add(
                $"{path}.maxLength",
                $"Pole {named} ma limit zero znaków, więc nie da się go wypełnić.");
        }

        if (field.MinLength is { } min && field.MaxLength is { } max && min > max)
        {
            reader.Add(
                $"{path}.minLength",
                $"Pole {named}: minimalna długość jest większa od maksymalnej.");
        }
    }

    private static void CheckRange(
        FormJsonReader reader,
        FormField field,
        string path,
        string named)
    {
        var numeric = FormFieldTypes.IsNumeric(field.Type);

        if (!numeric && (field.MinValue is not null || field.MaxValue is not null))
        {
            reader.Add(
                $"{path}.minValue",
                $"Pole {named} nie jest polem liczbowym, więc widełki wartości "
                + "nic tu nie znaczą.");
        }

        if (field.MinValue is { } min && field.MaxValue is { } max && min > max)
        {
            reader.Add(
                $"{path}.minValue",
                $"Pole {named}: wartość minimalna jest większa od maksymalnej.");
        }
    }
}
