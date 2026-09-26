using System.Globalization;
using System.Text.Json;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;
using Ocwip.Api.Services.Export;

namespace Ocwip.Api.Services.Pdf;

/// <summary>What the printed application says about itself, besides its answers.</summary>
internal sealed record ApplicationPdfFacts(
    string Number,
    string CompetitionTitle,
    string EntityName,
    EntityType EntityType,
    int FormVersion,
    DateTimeOffset SubmittedAt,
    string Checksum);

/// <summary>
/// The whole submitted application as a PDF (T-44), for the paper file of the
/// competition and the five years it is kept. Drawn from the form version it
/// was filled in on, never a newer one. Only the fields marked as printed
/// (D14) and only what the applicant was asked (visibility conditions and
/// kinds of applicant), and the checksum of the electronic version on every
/// page (D15). Black text on white, nothing carried by colour.
/// </summary>
internal static class ApplicationPdfBuilder
{
    // Helvetica 11 pt across the 483 points between the portrait margins.
    private const int Width = 88;

    public static byte[] Build(ApplicationPdfFacts facts, FormDocument form, JsonElement answers)
    {
        var header = new[]
        {
            PdfText.Transliterate($"Wniosek {facts.Number} | suma kontrolna {facts.Checksum}"),
            string.Empty,
        };

        var lines = new List<string>
        {
            $"Wniosek nr {facts.Number}",
            $"Konkurs: {facts.CompetitionTitle}",
            $"Wnioskodawca: {facts.EntityName} ({ApplicationListLabels.EntityType(facts.EntityType)})",
            $"Wersja formularza: {facts.FormVersion.ToString(CultureInfo.InvariantCulture)}",
            $"Data złożenia: {ApplicationListLabels.Moment(facts.SubmittedAt)} ({ApplicationListLabels.TimeLabel})",
            $"Suma kontrolna: {facts.Checksum}",
        };

        var calculator = new AnswerCalculator(form, answers, facts.EntityType);

        foreach (var section in form.Sections.Where(section => calculator.IsVisible(section.VisibleWhen)))
        {
            var fields = section.Fields.Where(field => Printed(field, calculator)).ToList();
            if (fields.Count == 0)
            {
                continue;
            }

            lines.Add(string.Empty);
            // Upper case marks a section on paper without colour or bold,
            // which Base14 text lines cannot carry.
            lines.Add(section.Title.ToUpperInvariant());

            foreach (var field in fields)
            {
                lines.Add(string.Empty);
                lines.Add(field.Label);

                if (FormFieldTypes.IsTable(field.Type))
                {
                    lines.AddRange(Table(field, calculator));
                }
                else
                {
                    lines.AddRange(Indent(Value(field, calculator.Answer(field.Key), calculator, null, null)));
                }
            }
        }

        var printable = lines.SelectMany(line => Wrap(PdfText.Transliterate(line), Width)).ToList();
        return SimplePdfDocument.Create(printable, PdfPageLayout.Portrait, header);
    }

    /// <summary>D14: printed fields only, and only those the applicant was asked; a file is listed with the attachments.</summary>
    private static bool Printed(FormField field, AnswerCalculator calculator) =>
        field.Printed
        && field.Type != FormFieldType.File
        && calculator.IsApplicable(field)
        && calculator.IsVisible(field.VisibleWhen);

    private static IEnumerable<string> Table(FormField table, AnswerCalculator calculator)
    {
        var rows = calculator.Rows(table);
        if (rows.Count == 0)
        {
            yield return "  (brak wierszy)";
            yield break;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            yield return table.Type == FormFieldType.FixedTable && i < table.Table!.Rows.Count
                ? $"  {table.Table.Rows[i].Label}:"
                : $"  Wiersz {(i + 1).ToString(CultureInfo.InvariantCulture)}:";

            foreach (var column in table.Table!.Columns.Where(column =>
                column.Printed && calculator.IsVisibleInRow(column.VisibleWhen, table, row)))
            {
                var value = Value(column, AnswerValues.Property(row, column.Key), calculator, table, row);
                yield return $"    {column.Label}: {string.Join(" ", value)}";
            }
        }
    }

    private static IReadOnlyList<string> Value(
        FormField field, JsonElement? answer, AnswerCalculator calculator, FormField? table, JsonElement? row)
    {
        if (field.Type == FormFieldType.Calculated)
        {
            var computed = table is null ? calculator.Value(field) : calculator.RowValue(table, row, field);
            return [Number(computed)];
        }

        if (field.Type == FormFieldType.Statement)
        {
            return [field.StatementText ?? string.Empty, answer is { ValueKind: JsonValueKind.True } ? "Tak" : "Nie"];
        }

        if (AnswerValues.IsEmpty(answer))
        {
            return ["(brak odpowiedzi)"];
        }

        var value = answer!.Value;
        return field.Type switch
        {
            FormFieldType.Amount => [$"{ApplicationListLabels.Amount(AnswerValues.Number(value))} zł"],
            FormFieldType.Percent => [$"{Number(AnswerValues.Number(value))} %"],
            FormFieldType.Number => [Number(AnswerValues.Number(value))],
            FormFieldType.YesNo => [value.ValueKind == JsonValueKind.True ? "Tak" : "Nie"],
            FormFieldType.SingleChoice => [Label(field, value.ToString())],
            FormFieldType.MultipleChoice when value.ValueKind == JsonValueKind.Array =>
                [string.Join(", ", value.EnumerateArray().Select(item => Label(field, item.ToString())))],
            _ => value.ValueKind == JsonValueKind.String
                ? value.GetString()!.Replace("\r\n", "\n").Split('\n')
                : [value.ToString()],
        };
    }

    private static string Label(FormField field, string value) =>
        field.Options.FirstOrDefault(option => option.Value == value)?.Label ?? value;

    private static string Number(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');

    private static IEnumerable<string> Indent(IEnumerable<string> lines) => lines.Select(line => "  " + line);

    /// <summary>Word wrap that keeps the indentation of the line it continues; a word longer than the line is cut.</summary>
    internal static IEnumerable<string> Wrap(string line, int width)
    {
        if (line.Length <= width)
        {
            yield return line;
            yield break;
        }

        var indent = new string(' ', line.Length - line.TrimStart().Length);
        var current = indent;

        foreach (var word in line.TrimStart().Split(' '))
        {
            if (current.Length > indent.Length && current.Length + 1 + word.Length > width)
            {
                yield return current;
                current = indent;
            }

            var piece = word;
            while (current.Length + piece.Length > width)
            {
                var room = width - current.Length;
                yield return current + piece[..room];
                piece = piece[room..];
                current = indent;
            }

            current += current.Length > indent.Length ? " " + piece : piece;
        }

        yield return current;
    }
}
