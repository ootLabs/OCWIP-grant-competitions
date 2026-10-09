using System.Text.Json;
using System.Text.Json.Nodes;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;

namespace Ocwip.Api.Services.Reports;

/// <summary>
/// "Było i jest" (T-50a, raport): the report takes what the application said
/// once, when it is started, and holds on to the part the applicant may not
/// change. Pure: documents and answers in, answers out.
/// </summary>
internal static class ReportPrefill
{
    /// <summary>
    /// The values a new report starts with: every field and table column
    /// with "prefillFrom", read from the submitted application, computed
    /// fields computed the way the application computed them. A key the
    /// application form does not have leaves the value empty.
    /// </summary>
    public static JsonObject Build(
        FormDocument report, FormDocument application, JsonElement answers, EntityType applicant, DateOnly? contractSignedOn = null)
    {
        var calculator = new AnswerCalculator(application, answers, applicant);
        // Only what the application actually asked: a field hidden by its
        // condition (or its section's) may still hold an old answer, and the
        // application's own view leaves that out too.
        var byKey = application.Sections
            .Where(section => calculator.IsVisible(section.VisibleWhen))
            .SelectMany(section => section.Fields)
            .Where(field => calculator.IsApplicable(field) && calculator.IsVisible(field.VisibleWhen))
            .ToDictionary(field => field.Key, StringComparer.Ordinal);
        var prefill = new JsonObject();

        foreach (var field in report.Sections.SelectMany(section => section.Fields))
        {
            // O-17: "realizacja od" is the day the contract was signed, which
            // the system knows; empty while the contract is not signed.
            if (field.PrefillFrom == FormReportParts.ContractSignedOn)
            {
                if (contractSignedOn is { } day)
                {
                    prefill[field.Key] = day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                }
                continue;
            }

            // O-17: one cell of a fixed table, the leader's row of the group.
            if (field.PrefillFrom is { } cell && cell.Contains('.'))
            {
                if (Cell(calculator, byKey, cell) is { } taken)
                {
                    prefill[field.Key] = taken;
                }
                continue;
            }

            if (field.PrefillFrom is not { } source || !byKey.TryGetValue(source, out var from))
            {
                continue;
            }

            if (FormFieldTypes.IsTable(field.Type))
            {
                if (from.Table is null)
                {
                    continue;
                }

                var rows = new JsonArray();
                foreach (var row in calculator.Rows(from))
                {
                    var copied = new JsonObject();
                    foreach (var column in field.Table!.Columns.Where(column => column.PrefillFrom is not null))
                    {
                        var fromColumn = from.Table.Columns.FirstOrDefault(c => c.Key == column.PrefillFrom);
                        if (fromColumn is null)
                        {
                            continue;
                        }

                        copied[column.Key] = fromColumn.Type == FormFieldType.Calculated
                            ? JsonValue.Create(calculator.RowValue(from, row, fromColumn))
                            : Copy(AnswerValues.Property(row, fromColumn.Key));
                    }

                    rows.Add(copied);
                }

                prefill[field.Key] = rows;
                continue;
            }

            var value = from.Type == FormFieldType.Calculated
                ? JsonValue.Create(calculator.Value(from))
                : Copy(calculator.Answer(from.Key));

            if (value is not null)
            {
                prefill[field.Key] = value;
            }
        }

        return prefill;
    }

    /// <summary>"table.row.column" of a fixed table, or null when any part is missing or empty.</summary>
    private static JsonNode? Cell(AnswerCalculator calculator, IReadOnlyDictionary<string, FormField> byKey, string path)
    {
        var parts = path.Split('.');
        if (!byKey.TryGetValue(parts[0], out var table)
            || table.Type != FormFieldType.FixedTable
            || table.Table is null)
        {
            return null;
        }

        var index = table.Table.Rows.ToList().FindIndex(row => row.Key == parts[1]);
        if (index < 0 || table.Table.Columns.All(column => column.Key != parts[2]))
        {
            return null;
        }

        return calculator.Rows(table)[index] is { } row ? Copy(AnswerValues.Property(row, parts[2])) : null;
    }

    /// <summary>
    /// The answers as saved, with every read only value put back from the
    /// prefill: a read only field gets its value, the first rows of a table
    /// taken from the application keep their read only cells (and cannot be
    /// deleted), and a row the applicant added has no read only cell at all.
    /// </summary>
    public static JsonObject Apply(FormDocument report, JsonElement answers, JsonElement prefill)
    {
        var result = JsonNode.Parse(answers.GetRawText())!.AsObject();
        var stored = JsonNode.Parse(prefill.GetRawText())!.AsObject();

        foreach (var field in report.Sections.SelectMany(section => section.Fields))
        {
            if (FormFieldTypes.IsTable(field.Type))
            {
                var readOnly = field.Table?.Columns.Where(column => column.ReadOnly).Select(column => column.Key).ToList() ?? [];
                if (field.PrefillFrom is null || readOnly.Count == 0)
                {
                    continue;
                }

                var taken = stored[field.Key] as JsonArray ?? [];
                var rows = result[field.Key] as JsonArray ?? [];
                var merged = new JsonArray();

                for (var i = 0; i < Math.Max(taken.Count, rows.Count); i++)
                {
                    var row = rows.ElementAtOrDefault(i) is JsonObject given
                        ? (JsonObject)given.DeepClone()
                        : [];

                    foreach (var key in readOnly)
                    {
                        row.Remove(key);
                        if (i < taken.Count && taken[i] is JsonObject source && source[key] is { } value)
                        {
                            row[key] = value.DeepClone();
                        }
                    }

                    merged.Add(row);
                }

                result[field.Key] = merged;
                continue;
            }

            if (field.ReadOnly)
            {
                result.Remove(field.Key);
                if (stored[field.Key] is { } value)
                {
                    result[field.Key] = value.DeepClone();
                }
            }
        }

        return result;
    }

    private static JsonNode? Copy(JsonElement? value) =>
        value is null || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            ? null
            : JsonNode.Parse(value.Value.GetRawText());
}
