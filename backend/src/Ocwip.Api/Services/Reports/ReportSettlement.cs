using System.Text.Json;
using Ocwip.Api.Contracts;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;

namespace Ocwip.Api.Services.Reports;

/// <summary>
/// One cost the operator did not accept, as stored on the report (T-50b).
/// Spent is the grant spending of the row when the operator judged it: a
/// row the applicant changed afterwards, or one that moved, no longer
/// matches, and its judgement stops counting instead of landing on another
/// cost.
/// </summary>
internal sealed record StoredCostReview(int Row, decimal Spent, decimal Refused, string Reason);

/// <summary>
/// Settling the grant against the report (T-50b): the grant spending of each
/// row of the budget, what the operator refused of it, and what goes back.
/// Pure: documents and answers in, numbers out.
/// </summary>
internal static class ReportSettlement
{
    internal const int ReasonMaxLength = 1000;

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<StoredCostReview> Read(JsonElement stored) =>
        stored.ValueKind == JsonValueKind.Array
            ? stored.Deserialize<List<StoredCostReview>>(Wire) ?? []
            : [];

    public static JsonElement Write(IReadOnlyList<StoredCostReview> review) =>
        JsonSerializer.SerializeToElement(review, Wire);

    /// <summary>Null when the report form marks no budget: there is nothing to settle row by row.</summary>
    public static ReportSettlementResponse? Compute(
        FormDocument form,
        JsonElement answers,
        EntityType applicant,
        decimal? awardedGrant,
        IReadOnlyList<StoredCostReview> review)
    {
        if (Spending(form, answers, applicant) is not { } spent)
        {
            return null;
        }

        var current = Current(review, spent.Rows);
        var rows = spent.Rows
            .Select((value, index) => current.TryGetValue(index, out var entry)
                ? new ReportCostRow(index, value, entry.Refused, entry.Reason)
                : new ReportCostRow(index, value, 0m, null))
            .ToList();

        var total = rows.Aggregate(0m, (sum, row) => Saturating.Add(sum, row.Spent));
        var refused = rows.Aggregate(0m, (sum, row) => Saturating.Add(sum, row.Refused));
        var accepted = Saturating.Subtract(total, refused);
        decimal? refund = awardedGrant is { } grant ? Math.Max(0m, Saturating.Subtract(grant, accepted)) : null;

        return new ReportSettlementResponse(spent.BudgetKey, awardedGrant, total, refused, accepted, refund, rows);
    }

    /// <summary>
    /// Checks a review the operator sent and turns it into what is stored.
    /// Errors keyed the way a validation problem is.
    /// </summary>
    public static (IReadOnlyList<StoredCostReview>? Review, IDictionary<string, string[]>? Errors) Check(
        FormDocument form,
        JsonElement answers,
        EntityType applicant,
        IReadOnlyList<CostReviewItem> items)
    {
        if (Spending(form, answers, applicant) is not { } spent)
        {
            return (null, new Dictionary<string, string[]>
            {
                ["items"] = ["Wzór sprawozdania nie ma tabeli budżetu (rola \"reportBudget\"), więc kosztów nie da się ocenić pozycja po pozycji."],
            });
        }

        var errors = new Dictionary<string, string[]>();
        var stored = new List<StoredCostReview>();
        var seen = new HashSet<int>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var key = $"items[{i}]";

            if (item.Row < 0 || item.Row >= spent.Rows.Count)
            {
                errors[key] = [$"Budżet nie ma pozycji {item.Row + 1}."];
                continue;
            }

            if (!seen.Add(item.Row))
            {
                errors[key] = [$"Pozycja {item.Row + 1} jest oceniona dwa razy."];
                continue;
            }

            var reason = item.Reason?.Trim();
            var value = spent.Rows[item.Row];

            if (item.Refused <= 0m || item.Refused > value || decimal.Round(item.Refused, 2) != item.Refused)
            {
                errors[key] = [$"Kwota nieuznana w pozycji {item.Row + 1} musi być większa od zera, "
                    + $"mieć najwyżej dwa miejsca po przecinku i nie przekraczać wydatku z dotacji ({Polish(value)} zł)."];
                continue;
            }

            if (string.IsNullOrEmpty(reason) || reason.Length > ReasonMaxLength)
            {
                errors[key] = [$"Podaj powód nieuznania pozycji {item.Row + 1}, najwyżej {ReasonMaxLength} znaków: wnioskodawca go przeczyta."];
                continue;
            }

            stored.Add(new StoredCostReview(item.Row, value, item.Refused, reason));
        }

        return errors.Count > 0 ? (null, errors) : (stored.OrderBy(x => x.Row).ToList(), null);
    }

    /// <summary>"1400,00": the applicant's and the operator's way of writing an amount, without pl-PL.</summary>
    private static string Polish(decimal value) =>
        value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>The judgements that still match their row; the rest are dropped.</summary>
    public static IReadOnlyList<StoredCostReview> Keep(
        FormDocument form, JsonElement answers, EntityType applicant, IReadOnlyList<StoredCostReview> review) =>
        Spending(form, answers, applicant) is { } spent
            ? Current(review, spent.Rows).Values.OrderBy(x => x.Row).ToList()
            : [];

    private static Dictionary<int, StoredCostReview> Current(IReadOnlyList<StoredCostReview> review, IReadOnlyList<decimal> rows) =>
        review
            .Where(entry => entry.Row >= 0 && entry.Row < rows.Count && rows[entry.Row] == entry.Spent)
            .GroupBy(entry => entry.Row)
            .ToDictionary(group => group.Key, group => group.First());

    /// <summary>
    /// The grant spending of each budget row, counted the way the form shows
    /// it: a table or a cell the applicant is not asked is worth nothing.
    /// </summary>
    private static (string BudgetKey, IReadOnlyList<decimal> Rows)? Spending(
        FormDocument form, JsonElement answers, EntityType applicant)
    {
        var calculator = new AnswerCalculator(form, answers, applicant);

        foreach (var section in form.Sections)
        {
            var budget = section.Fields.FirstOrDefault(field => field.Role == FormFieldRole.ReportBudget);
            if (budget is null)
            {
                continue;
            }

            var column = budget.Table!.Columns.Single(c => c.Role == FormFieldRole.GrantSpent);
            var asked = calculator.IsVisible(section.VisibleWhen)
                && calculator.IsApplicable(budget)
                && calculator.IsVisible(budget.VisibleWhen);

            var rows = asked
                ? calculator.Rows(budget)
                    .Select(row => calculator.IsVisibleInRow(column.VisibleWhen, budget, row)
                        ? calculator.RowValue(budget, row, column)
                        : 0m)
                    .ToList()
                : [];

            return (budget.Key, rows);
        }

        return null;
    }
}
