using System.Text.Json;

namespace Ocwip.Api.Models.Forms;

/// <summary>
/// What a form's answers mean together (T-30): which sections and fields are
/// visible, and what every calculated field comes to. The same rules as
/// frontend/lib/forms/evaluate.ts, read from the same document, so a budget
/// the form shows as within the limit is within the limit here too.
///
/// Amounts stay decimal at full precision the whole way (D13): rounding
/// happens only when a message prints one.
/// </summary>
internal sealed class AnswerCalculator
{
    private readonly JsonElement _answers;
    private readonly Dictionary<string, FormField> _fields;
    private readonly Dictionary<string, decimal> _values = new(StringComparer.Ordinal);
    private readonly HashSet<string> _resolving = new(StringComparer.Ordinal);

    public AnswerCalculator(FormDocument document, JsonElement answers)
    {
        _answers = answers;
        _fields = document.Sections
            .SelectMany(section => section.Fields)
            .ToDictionary(field => field.Key, StringComparer.Ordinal);
    }

    /// <summary>The stored answer to a field of the form, or null.</summary>
    public JsonElement? Answer(string key) => AnswerValues.Property(_answers, key);

    public bool IsVisible(FormCondition? condition) =>
        condition is null || AnswerValues.Matches(Answer(condition.Field), condition.EqualsAnyOf);

    /// <summary>
    /// A column's condition, read inside its row: the key names a sibling
    /// column first and a field of the form otherwise, the same resolution
    /// the contract gate uses (FormSchemaReferences.Resolve).
    /// </summary>
    public bool IsVisibleInRow(FormCondition? condition, FormField table, JsonElement? row)
    {
        if (condition is null)
        {
            return true;
        }

        var value = table.Table!.Columns.Any(column => column.Key == condition.Field)
            ? AnswerValues.Property(row, condition.Field)
            : Answer(condition.Field);

        return AnswerValues.Matches(value, condition.EqualsAnyOf);
    }

    /// <summary>
    /// The rows a table has right now: as many as the applicant added, or
    /// for a table of fixed size exactly its declared rows, missing ones read
    /// as empty (resolveTableRows in the renderer).
    /// </summary>
    public IReadOnlyList<JsonElement?> Rows(FormField table)
    {
        var stored = Answer(table.Key) is { ValueKind: JsonValueKind.Array } array
            ? array.EnumerateArray().Select(row => (JsonElement?)row).ToList()
            : [];

        if (table.Type != FormFieldType.FixedTable)
        {
            return stored;
        }

        return Enumerable.Range(0, table.Table!.Rows.Count)
            .Select(index => index < stored.Count ? stored[index] : null)
            .ToList();
    }

    /// <summary>The value of a field outside any table.</summary>
    public decimal Value(FormField field)
    {
        if (field.Type != FormFieldType.Calculated || field.Calculation is null)
        {
            return AnswerValues.Number(Answer(field.Key));
        }

        if (_values.TryGetValue(field.Key, out var known))
        {
            return known;
        }

        // The contract gate refuses a circle (FormSchemaReferences), so this
        // is a guard for a definition stored before that check existed, not
        // the defence: a circle reads as zero instead of never returning.
        if (!_resolving.Add(field.Key))
        {
            return 0m;
        }

        var result = Combine(
            field.Calculation.Kind,
            field.Calculation.Operands.Select(Operand).ToList());

        _resolving.Remove(field.Key);
        _values[field.Key] = result;

        return result;
    }

    /// <summary>The value of one cell, calculated or typed.</summary>
    public decimal RowValue(FormField table, JsonElement? row, FormField column) =>
        RowValue(table, row, column, new HashSet<string>(StringComparer.Ordinal));

    private decimal RowValue(
        FormField table,
        JsonElement? row,
        FormField column,
        HashSet<string> resolving)
    {
        if (column.Type != FormFieldType.Calculated || column.Calculation is null)
        {
            return AnswerValues.Number(AnswerValues.Property(row, column.Key));
        }

        if (!resolving.Add(column.Key))
        {
            return 0m;
        }

        var values = column.Calculation.Operands
            .Select(operand =>
                table.Table!.Columns.FirstOrDefault(sibling => sibling.Key == operand)
                    is { } sibling
                    ? RowValue(table, row, sibling, resolving)
                    : Operand(operand))
            .ToList();

        resolving.Remove(column.Key);

        return Combine(column.Calculation.Kind, values);
    }

    /// <summary>
    /// An operand outside a table: a field of the form, or "table.column"
    /// for the sum down that column. A plain key never holds a dot
    /// (FormJsonReader.IsValidKey), so the two cannot be mistaken.
    /// </summary>
    private decimal Operand(string operand)
    {
        var dot = operand.IndexOf('.');

        if (dot < 0)
        {
            return _fields.TryGetValue(operand, out var field) ? Value(field) : 0m;
        }

        if (!_fields.TryGetValue(operand[..dot], out var table)
            || table.Table?.Columns.FirstOrDefault(column => column.Key == operand[(dot + 1)..])
                is not { } target)
        {
            return 0m;
        }

        return Rows(table).Aggregate(
            0m,
            (sum, row) => Saturating.Add(sum, RowValue(table, row, target)));
    }

    private static decimal Combine(FormCalculationKind kind, IReadOnlyList<decimal> values) =>
        kind switch
        {
            FormCalculationKind.Sum => values.Aggregate(0m, Saturating.Add),
            FormCalculationKind.Product => values.Aggregate(1m, Saturating.Multiply),
            FormCalculationKind.Difference => values.Skip(1).Aggregate(
                values.Count > 0 ? values[0] : 0m,
                Saturating.Subtract),
            FormCalculationKind.Ratio => values.Count == 2 && values[1] != 0m
                ? Saturating.Multiply(Saturating.Divide(values[0], values[1]), 100m)
                : 0m,
            _ => 0m,
        };
}

/// <summary>
/// Arithmetic that stops at the largest decimal instead of throwing. Every
/// number here comes from a request, and 10^20 times 10^20 typed into two
/// cells must answer with a limit message, not with an error 500.
/// </summary>
internal static class Saturating
{
    public static decimal Add(decimal left, decimal right) =>
        Run(() => left + right, Math.Sign(left) + Math.Sign(right));

    public static decimal Subtract(decimal left, decimal right) =>
        Run(() => left - right, Math.Sign(left) - Math.Sign(right));

    public static decimal Multiply(decimal left, decimal right) =>
        Run(() => left * right, Math.Sign(left) * Math.Sign(right));

    public static decimal Divide(decimal left, decimal right) =>
        Run(() => left / right, Math.Sign(left) * Math.Sign(right));

    private static decimal Run(Func<decimal> operation, int direction)
    {
        try
        {
            return operation();
        }
        catch (OverflowException)
        {
            return direction < 0 ? decimal.MinValue : decimal.MaxValue;
        }
    }
}
