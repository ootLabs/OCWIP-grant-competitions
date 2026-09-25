using System.Text.Json;

namespace Ocwip.Api.Models.Forms;

/// <summary>
/// Checks an applicant's answers against the form version the application was
/// started on (T-30). The truth side of what the renderer checks as a
/// convenience (frontend/lib/forms/validate.ts): without it anybody could send
/// any JSON straight to the API and have it stored as an application.
///
/// The document is the one the application points at, never the newest one:
/// an application filled in against version 3 must not stop passing because
/// the operator published version 4.
///
/// At most one message per key, the most useful one first, in the renderer's
/// order: the shape beats "required", "required" beats a range, a range beats
/// a limit. There is no point telling somebody their amount is over budget
/// before telling them the field is empty.
/// </summary>
public static class AnswerValidator
{
    /// <summary>
    /// A ceiling on the rows of a table the applicant adds rows to. The
    /// widest budget in the 2026 template has a few dozen rows; a request with
    /// a hundred thousand is not a budget, it is a way to make the server
    /// count. A definition whose maxRows is higher raises it, because the
    /// operator decided that and the form lets the applicant add that many.
    /// </summary>
    public const int MaxTableRows = 500;

    private const string RequiredMessage = "To pole jest wymagane.";

    /// <summary>The renderer's shorter word for a cell (validateCell).</summary>
    private const string RequiredCellMessage = "Wymagane.";

    /// <param name="answers">Must be a JSON object; the caller checks that.</param>
    /// <param name="competitionBases">
    /// The competition settings a limit may be measured against
    /// (<see cref="AnswerLimits.BasesFor"/>). Kept out of the document on
    /// purpose: the same form serves competitions with different ceilings.
    /// </param>
    /// <param name="applicant">
    /// Whose application the answers are about, for a field asked of some
    /// kinds of applicant only (T-38, an evaluation card). Null for an
    /// application form, which may not carry such a field.
    /// </param>
    public static AnswerValidationResult Validate(
        FormDocument document,
        JsonElement answers,
        IReadOnlyDictionary<string, decimal?> competitionBases,
        AnswerStrictness strictness,
        EntityType? applicant = null)
    {
        if (answers.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Answers must be a JSON object.", nameof(answers));
        }

        var errors = new Errors();
        var fields = document.Sections
            .SelectMany(section => section.Fields)
            .ToDictionary(field => field.Key, StringComparer.Ordinal);

        CheckShapes(answers, fields, errors);

        if (strictness == AnswerStrictness.Submission)
        {
            CheckComplete(document, answers, competitionBases, applicant, errors);
        }

        return new AnswerValidationResult(errors.All);
    }

    /// <summary>
    /// Both levels: every key belongs to the form and every value has its
    /// field's shape. An answer the form does not have is refused rather than
    /// dropped, because a key dropped quietly today is a key the print, the
    /// report or the agreement reads tomorrow.
    /// </summary>
    private static void CheckShapes(
        JsonElement answers,
        IReadOnlyDictionary<string, FormField> fields,
        Errors errors)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in answers.EnumerateObject())
        {
            // Two values under one key: the reader that takes the first and
            // the reader that takes the last would each see a different
            // application.
            if (!seen.Add(property.Name))
            {
                errors.Add(property.Name, "To pole występuje w odpowiedziach dwa razy.");
                continue;
            }

            if (!fields.TryGetValue(property.Name, out var field))
            {
                errors.Add(property.Name, "Formularz nie ma takiego pola.");
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            if (field.Table is not null)
            {
                CheckTableShape(field, property.Value, errors);
                continue;
            }

            errors.Add(field.Key, AnswerShape.Check(field, property.Value));
        }
    }

    private static void CheckTableShape(FormField table, JsonElement value, Errors errors)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            errors.Add(table.Key, "Odpowiedź musi być listą wierszy.");
            return;
        }

        var count = value.GetArrayLength();
        var ceiling = table.Type == FormFieldType.FixedTable
            ? table.Table!.Rows.Count
            : Math.Max(MaxTableRows, table.Table!.MaxRows ?? 0);

        if (count > ceiling)
        {
            errors.Add(table.Key, $"Za dużo wierszy: dopuszczalna liczba to {ceiling} (jest {count}).");
            return;
        }

        var index = 0;

        foreach (var row in value.EnumerateArray())
        {
            CheckRowShape(table, row, index++, errors);
        }
    }

    private static void CheckRowShape(FormField table, JsonElement row, int index, Errors errors)
    {
        // A row nobody has typed into yet: a table of fixed size edited out
        // of order leaves these, and JSON writes the holes as null.
        if (row.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (row.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{table.Key}[{index}]", "Wiersz musi być obiektem.");
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var cell in row.EnumerateObject())
        {
            var key = CellKey(table, index, cell.Name);

            if (!seen.Add(cell.Name))
            {
                errors.Add(key, "Ta kolumna występuje w wierszu dwa razy.");
                continue;
            }

            if (table.Table!.Columns.FirstOrDefault(column => column.Key == cell.Name)
                is not { } column)
            {
                errors.Add(key, "Tabela nie ma takiej kolumny.");
                continue;
            }

            if (cell.Value.ValueKind != JsonValueKind.Null)
            {
                errors.Add(key, AnswerShape.Check(column, cell.Value));
            }
        }
    }

    /// <summary>
    /// Submission only: every visible required field answered, every range
    /// and every limit kept. A field hidden by a condition, or standing in a
    /// hidden section, cannot be filled in, so it cannot be missing either;
    /// whatever it holds from before it was hidden is left alone.
    /// </summary>
    private static void CheckComplete(
        FormDocument document,
        JsonElement answers,
        IReadOnlyDictionary<string, decimal?> competitionBases,
        EntityType? applicant,
        Errors errors)
    {
        var calculator = new AnswerCalculator(document, answers, applicant);
        var fields = document.Sections.SelectMany(section => section.Fields).ToList();

        decimal? Basis(string basis) =>
            competitionBases.TryGetValue(basis, out var setting)
                ? setting
                : fields.FirstOrDefault(field => field.Key == basis) is { } field
                    ? calculator.Value(field)
                    : null;

        foreach (var section in document.Sections)
        {
            if (!calculator.IsVisible(section.VisibleWhen))
            {
                continue;
            }

            foreach (var field in section.Fields)
            {
                if (!calculator.IsVisible(field.VisibleWhen)
                    || !calculator.IsApplicable(field)
                    || errors.Has(field.Key))
                {
                    continue;
                }

                if (field.Table is not null)
                {
                    CheckTable(field, calculator, Basis, errors);
                    continue;
                }

                var problem = CheckField(field, calculator.Answer(field.Key), RequiredMessage);

                if (problem is not null)
                {
                    errors.Add(field.Key, problem);
                    continue;
                }

                if (AnswerLimits.Find(field, calculator.Value(field), Basis) is { } breach)
                {
                    ReportBreach(field, breach, fields, calculator, errors);
                }
            }
        }
    }

    /// <summary>
    /// A limit exceeded by a field outside any table. When the field is the
    /// sum down one column of a table, which is how every budget table's
    /// total is built, the message names the table and a second one stands
    /// on the position where the running total first goes over (T-31): the
    /// applicant has five pages of application, and "limit exceeded" without
    /// a place to look is a phone call to OCWIP.
    /// </summary>
    private static void ReportBreach(
        FormField field,
        LimitBreach breach,
        IReadOnlyList<FormField> fields,
        AnswerCalculator calculator,
        Errors errors)
    {
        if (SummedColumn(field, fields) is not (var table, var column))
        {
            errors.Add(field.Key, breach.Message());
            return;
        }

        errors.Add(field.Key, $"Tabela „{table.Label}”: {Lowercase(breach.Message())}");

        var rows = calculator.Rows(table);
        var total = 0m;

        for (var index = 0; index < rows.Count; index++)
        {
            total = Saturating.Add(total, calculator.RowValue(table, rows[index], column));

            if (total <= breach.Allowed)
            {
                continue;
            }

            errors.Add(
                CellKey(table, index, column.Key),
                $"Od tej pozycji suma tabeli „{table.Label}” przekracza dopuszczalną "
                + $"wartość o {breach.Format(Saturating.Subtract(total, breach.Allowed))}. "
                + $"Maksymalnie {breach.Format(breach.Allowed)}.");
            return;
        }
    }

    /// <summary>
    /// The table and column a field sums, when it is a sum over exactly one
    /// "table.column" operand; null for anything else.
    /// </summary>
    private static (FormField Table, FormField Column)? SummedColumn(
        FormField field,
        IReadOnlyList<FormField> fields)
    {
        if (field.Calculation is not { Kind: FormCalculationKind.Sum, Operands: [var operand] })
        {
            return null;
        }

        var dot = operand.IndexOf('.');

        if (dot < 0
            || fields.FirstOrDefault(candidate => candidate.Key == operand[..dot]) is not { Table: { } shape } table
            || shape.Columns.FirstOrDefault(candidate => candidate.Key == operand[(dot + 1)..]) is not { } column)
        {
            return null;
        }

        return (table, column);
    }

    private static string Lowercase(string sentence) =>
        sentence.Length == 0 ? sentence : char.ToLowerInvariant(sentence[0]) + sentence[1..];

    /// <summary>
    /// "Required" and the ranges of one value whose shape is already known
    /// to be right. Limits are the caller's, because a cell resolves its
    /// basis inside its own row.
    /// </summary>
    private static string? CheckField(FormField field, JsonElement? value, string requiredMessage)
    {
        if (field.Type == FormFieldType.Calculated)
        {
            return null;
        }

        // "I declare that" is affirmed only by a tick: false is a complete
        // answer to a yes or no question, but not to a declaration.
        if (field.Type == FormFieldType.Statement)
        {
            return field.Required && value?.ValueKind != JsonValueKind.True
                ? requiredMessage
                : null;
        }

        if (AnswerValues.IsEmpty(value))
        {
            return field.Required ? requiredMessage : null;
        }

        var element = value!.Value;

        if (FormFieldTypes.IsTextual(field.Type))
        {
            var length = element.GetString()!.Length;

            return field.MinLength is { } min && length < min
                ? $"Wymagane co najmniej {min} znaków (jest {length})."
                : null;
        }

        if (FormFieldTypes.IsNumeric(field.Type))
        {
            var number = element.GetDecimal();

            if (field.MinValue is { } low && number < low)
            {
                return $"Wartość nie może być mniejsza niż {PolishNumbers.Number(low)}.";
            }

            if (field.MaxValue is { } high && number > high)
            {
                return $"Wartość nie może być większa niż {PolishNumbers.Number(high)}.";
            }
        }

        return null;
    }

    private static void CheckTable(
        FormField table,
        AnswerCalculator calculator,
        Func<string, decimal?> basis,
        Errors errors)
    {
        var rows = calculator.Rows(table);
        var shape = table.Table!;

        if (table.Type == FormFieldType.RepeatableTable)
        {
            if (table.Required && rows.Count == 0)
            {
                errors.Add(table.Key, RequiredMessage);
            }
            else if (shape.MinRows is { } min && rows.Count < min)
            {
                errors.Add(table.Key, $"Za mało wierszy: wymagana liczba to co najmniej {min} (jest {rows.Count}).");
            }
            else if (shape.MaxRows is { } max && rows.Count > max)
            {
                errors.Add(table.Key, $"Za dużo wierszy: dopuszczalna liczba to {max} (jest {rows.Count}).");
            }
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];

            // A row that is not a row already has its message; reading its
            // cells as empty would bury it under one "required" per column.
            if (errors.Has($"{table.Key}[{index}]"))
            {
                continue;
            }

            foreach (var column in shape.Columns)
            {
                var key = CellKey(table, index, column.Key);

                if (errors.Has(key) || !calculator.IsVisibleInRow(column.VisibleWhen, table, row))
                {
                    continue;
                }

                errors.Add(key, CheckField(column, AnswerValues.Property(row, column.Key), RequiredCellMessage)
                    ?? AnswerLimits.Check(
                        column,
                        calculator.RowValue(table, row, column),
                        name => shape.Columns.FirstOrDefault(sibling => sibling.Key == name)
                            is { } sibling
                                ? calculator.RowValue(table, row, sibling)
                                : basis(name)));
            }
        }
    }

    /// <summary>cellKey in frontend/components/form-renderer/renderer-context.tsx.</summary>
    private static string CellKey(FormField table, int index, string column) =>
        $"{table.Key}[{index}].{column}";

    /// <summary>First message per key wins, in the order they are checked.</summary>
    private sealed class Errors
    {
        private readonly List<AnswerError> _all = [];
        private readonly HashSet<string> _keys = new(StringComparer.Ordinal);

        public IReadOnlyList<AnswerError> All => _all;

        public bool Has(string key) => _keys.Contains(key);

        public void Add(string key, string? message)
        {
            if (message is not null && _keys.Add(key))
            {
                _all.Add(new AnswerError(key, message));
            }
        }
    }
}
