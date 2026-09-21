using System.Text.Json;

namespace Ocwip.Api.Models.Forms;

/// <summary>
/// The parts of a field that only some kinds have: choices, a table, a
/// calculation, limits, file rules (T-24).
///
/// Every one of them is refused on a kind that has no use for it. A choice
/// list sitting on a date field is not harmless: it is a field the creator
/// will show one way and the renderer another.
/// </summary>
internal static class FormFieldParts
{
    /// <summary>
    /// A ceiling on the choice list. Nothing else bounds it, and the report's
    /// longest list has six entries.
    /// </summary>
    private const int MaxOptions = 200;

    public static IReadOnlyList<FormOption> Options(
        FormJsonReader reader,
        JsonElement element,
        string path,
        FormFieldType type,
        string named)
    {
        var isChoice = FormFieldTypes.IsChoice(type);
        var raw = reader.ArrayProperty(element, "options", path, required: isChoice);

        if (!isChoice)
        {
            if (raw.Count > 0)
            {
                reader.Add(
                    $"{path}.options",
                    $"Pole {named} nie jest listą wyboru, więc nie może mieć opcji.");
            }

            return [];
        }

        if (raw.Count == 0)
        {
            reader.Add(
                $"{path}.options",
                $"Pole {named} jest listą wyboru bez ani jednej opcji.");
            return [];
        }

        if (raw.Count > MaxOptions)
        {
            reader.Add(
                $"{path}.options",
                $"Pole {named} ma więcej niż {MaxOptions} opcji.");
            return [];
        }

        var options = new List<FormOption>(raw.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < raw.Count; index++)
        {
            var optionPath = $"{path}.options[{index}]";

            if (raw[index].ValueKind != JsonValueKind.Object)
            {
                reader.Add(optionPath, "Opcja musi być obiektem.");
                continue;
            }

            var value = reader.StringProperty(raw[index], "value", optionPath, true);
            var label = reader.StringProperty(raw[index], "label", optionPath, true);

            if (value is null || label is null)
            {
                continue;
            }

            if (!seen.Add(value))
            {
                reader.Add(
                    $"{optionPath}.value",
                    $"Pole {named} ma dwie opcje o wartości \"{value}\", więc "
                    + "odpowiedzi nie da się jednoznacznie odczytać.");
                continue;
            }

            options.Add(new FormOption(value, label));
        }

        return options;
    }

    public static FormTable? Table(
        FormJsonReader reader,
        JsonElement element,
        string path,
        FormFieldType type,
        string named)
    {
        var isTable = FormFieldTypes.IsTable(type);
        var table = reader.ObjectProperty(element, "table", path, required: isTable);

        if (table is null)
        {
            return null;
        }

        if (!isTable)
        {
            reader.Add(
                $"{path}.table",
                $"Pole {named} nie jest tabelą, więc nie może nieść kolumn.");
            return null;
        }

        var tablePath = $"{path}.table";
        var columns = Columns(reader, table.Value, tablePath, named);
        var rows = Rows(reader, table.Value, tablePath, type, named);
        var minRows = reader.IntegerProperty(table.Value, "minRows", tablePath);
        var maxRows = reader.IntegerProperty(table.Value, "maxRows", tablePath);

        if (type == FormFieldType.FixedTable && (minRows is not null || maxRows is not null))
        {
            reader.Add(
                $"{tablePath}.minRows",
                $"Tabela {named} ma stałą liczbę wierszy, więc widełki liczby "
                + "wierszy nic tu nie znaczą.");
        }

        if (minRows is { } low && maxRows is { } high && low > high)
        {
            reader.Add(
                $"{tablePath}.minRows",
                $"Tabela {named}: minimalna liczba wierszy jest większa od "
                + "maksymalnej.");
        }

        return new FormTable(columns, rows, minRows, maxRows);
    }

    private static IReadOnlyList<FormField> Columns(
        FormJsonReader reader,
        JsonElement table,
        string tablePath,
        string named)
    {
        var raw = reader.ArrayProperty(table, "columns", tablePath, required: true);

        if (raw.Count == 0)
        {
            reader.Add(
                $"{tablePath}.columns",
                $"Tabela {named} nie ma ani jednej kolumny.");
            return [];
        }

        var columns = new List<FormField>(raw.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < raw.Count; index++)
        {
            var column = FormFieldParser.Parse(
                reader,
                raw[index],
                $"{tablePath}.columns[{index}]",
                asColumn: true);

            if (column is null)
            {
                continue;
            }

            if (!seen.Add(column.Key))
            {
                reader.Add(
                    $"{tablePath}.columns[{index}].key",
                    $"Tabela {named} ma dwie kolumny o kluczu \"{column.Key}\".");
                continue;
            }

            columns.Add(column);
        }

        return columns;
    }

    private static IReadOnlyList<FormTableRow> Rows(
        FormJsonReader reader,
        JsonElement table,
        string tablePath,
        FormFieldType type,
        string named)
    {
        var isFixed = type == FormFieldType.FixedTable;
        var raw = reader.ArrayProperty(table, "rows", tablePath, required: isFixed);

        if (!isFixed)
        {
            if (raw.Count > 0)
            {
                reader.Add(
                    $"{tablePath}.rows",
                    $"Tabela {named} ma zmienną liczbę wierszy, więc wierszy nie "
                    + "wypisuje się z góry.");
            }

            return [];
        }

        if (raw.Count == 0)
        {
            reader.Add(
                $"{tablePath}.rows",
                $"Tabela {named} ma stałą liczbę wierszy, ale nie wypisano ani "
                + "jednego.");
            return [];
        }

        var rows = new List<FormTableRow>(raw.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < raw.Count; index++)
        {
            var rowPath = $"{tablePath}.rows[{index}]";

            if (raw[index].ValueKind != JsonValueKind.Object)
            {
                reader.Add(rowPath, "Wiersz musi być obiektem.");
                continue;
            }

            var key = reader.StringProperty(raw[index], "key", rowPath, true);
            var label = reader.StringProperty(raw[index], "label", rowPath, true);

            if (key is null || label is null)
            {
                continue;
            }

            if (!FormJsonReader.IsValidKey(key) || !seen.Add(key))
            {
                reader.Add(
                    $"{rowPath}.key",
                    $"Tabela {named}: klucz wiersza \"{key}\" jest powtórzony albo "
                    + "ma niedozwoloną postać.");
                continue;
            }

            rows.Add(new FormTableRow(key, label));
        }

        return rows;
    }

    public static FormCalculation? Calculation(
        FormJsonReader reader,
        JsonElement element,
        string path,
        FormFieldType type,
        string named)
    {
        var isCalculated = type == FormFieldType.Calculated;
        var calculation = reader.ObjectProperty(
            element,
            "calculation",
            path,
            required: false);

        if (calculation is null)
        {
            if (isCalculated)
            {
                reader.Add(
                    $"{path}.calculation",
                    $"Pole {named} jest wyliczane, ale nie ma z czego się policzyć.");
            }

            return null;
        }

        if (!isCalculated)
        {
            reader.Add(
                $"{path}.calculation",
                $"Pole {named} nie jest polem wyliczanym, więc nie może mieć "
                + "źródła obliczenia.");
            return null;
        }

        var calculationPath = $"{path}.calculation";
        var kindName = reader.StringProperty(calculation.Value, "kind", calculationPath, true);
        var operands = OperandKeys(reader, calculation.Value, calculationPath);

        if (kindName is null
            || !Enum.TryParse<FormCalculationKind>(kindName, ignoreCase: true, out var kind)
            || kind == FormCalculationKind.Unknown)
        {
            reader.Add(
                $"{calculationPath}.kind",
                $"Pole {named}: sposób obliczenia \"{kindName}\" nie istnieje.");
            return null;
        }

        var required = kind switch
        {
            FormCalculationKind.Sum => 1,
            FormCalculationKind.Ratio => 2,
            _ => 2,
        };

        var exact = kind is FormCalculationKind.Sum or FormCalculationKind.Ratio;

        if (operands.Count < required || (exact && operands.Count != required))
        {
            reader.Add(
                $"{calculationPath}.operands",
                $"Pole {named}: obliczenie \"{kindName}\" potrzebuje "
                + $"{(exact ? "dokładnie" : "co najmniej")} {required} składników.");
            return null;
        }

        return new FormCalculation(kind, operands);
    }

    private static IReadOnlyList<string> OperandKeys(
        FormJsonReader reader,
        JsonElement calculation,
        string calculationPath)
    {
        var raw = reader.ArrayProperty(calculation, "operands", calculationPath, true);
        var operands = new List<string>(raw.Count);

        for (var index = 0; index < raw.Count; index++)
        {
            if (raw[index].ValueKind != JsonValueKind.String)
            {
                reader.Add(
                    $"{calculationPath}.operands[{index}]",
                    "Składnik obliczenia musi być kluczem pola.");
                continue;
            }

            operands.Add(raw[index].GetString() ?? string.Empty);
        }

        return operands;
    }

    public static IReadOnlyList<FormLimit> Limits(
        FormJsonReader reader,
        JsonElement element,
        string path,
        FormFieldType type,
        string named)
    {
        var raw = reader.ArrayProperty(element, "limits", path, required: false);

        if (raw.Count == 0)
        {
            return [];
        }

        if (!FormFieldTypes.IsNumeric(type))
        {
            reader.Add(
                $"{path}.limits",
                $"Pole {named} nie jest polem liczbowym, więc nie da się go "
                + "porównać z limitem.");
            return [];
        }

        var limits = new List<FormLimit>(raw.Count);

        for (var index = 0; index < raw.Count; index++)
        {
            var limitPath = $"{path}.limits[{index}]";

            if (raw[index].ValueKind != JsonValueKind.Object)
            {
                reader.Add(limitPath, "Limit musi być obiektem.");
                continue;
            }

            var kindName = reader.StringProperty(raw[index], "kind", limitPath, true);
            var basis = reader.StringProperty(raw[index], "basis", limitPath, true);
            var percent = reader.DecimalProperty(raw[index], "percent", limitPath);

            if (kindName is null
                || !Enum.TryParse<FormLimitKind>(kindName, ignoreCase: true, out var kind)
                || kind == FormLimitKind.Unknown)
            {
                reader.Add(
                    $"{limitPath}.kind",
                    $"Pole {named}: rodzaj limitu \"{kindName}\" nie istnieje.");
                continue;
            }

            if (kind == FormLimitKind.MaxPercentOf && percent is not (> 0 and <= 100))
            {
                reader.Add(
                    $"{limitPath}.percent",
                    $"Pole {named}: limit procentowy musi podawać procent z "
                    + "przedziału od zera do stu.");
                continue;
            }

            if (kind == FormLimitKind.MaxAmount && percent is not null)
            {
                reader.Add(
                    $"{limitPath}.percent",
                    $"Pole {named}: limit kwotowy nie liczy procentu.");
                continue;
            }

            if (basis is null)
            {
                continue;
            }

            limits.Add(new FormLimit(kind, percent, basis));
        }

        return limits;
    }

    public static FormFileRules? File(
        FormJsonReader reader,
        JsonElement element,
        string path,
        FormFieldType type,
        string named)
    {
        var isFile = type == FormFieldType.File;
        var rules = reader.ObjectProperty(element, "file", path, required: isFile);

        if (rules is null)
        {
            return null;
        }

        if (!isFile)
        {
            reader.Add(
                $"{path}.file",
                $"Pole {named} nie jest załącznikiem, więc nie może nieść reguł "
                + "pliku.");
            return null;
        }

        var filePath = $"{path}.file";
        var raw = reader.ArrayProperty(rules.Value, "allowedFormats", filePath, true);
        var formats = new List<AllowedFileFormat>(raw.Count);

        for (var index = 0; index < raw.Count; index++)
        {
            var name = raw[index].ValueKind == JsonValueKind.String
                ? raw[index].GetString()
                : null;

            if (name is null
                || !Enum.TryParse<AllowedFileFormat>(name, ignoreCase: true, out var format))
            {
                reader.Add(
                    $"{filePath}.allowedFormats[{index}]",
                    $"Pole {named}: format pliku spoza listy dopuszczalnych.");
                continue;
            }

            formats.Add(format);
        }

        if (formats.Count == 0)
        {
            reader.Add(
                $"{filePath}.allowedFormats",
                $"Pole {named} nie dopuszcza ani jednego formatu pliku.");
        }

        var maxSize = reader.IntegerProperty(rules.Value, "maxSizeMegabytes", filePath);

        if (maxSize is 0)
        {
            reader.Add(
                $"{filePath}.maxSizeMegabytes",
                $"Pole {named}: limit rozmiaru zero megabajtów odrzuci każdy plik.");
        }

        return new FormFileRules(formats, maxSize);
    }
}
