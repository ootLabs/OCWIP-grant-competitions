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
/// cost. Budget is the key of the budget table (T-95: a report may have
/// several, one per part of the budget); a review stored before that has
/// none and means the first.
/// </summary>
internal sealed record StoredCostReview(int Row, decimal Spent, decimal Refused, string Reason, string? Budget = null);

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
        if (Spending(form, answers, applicant) is not { Count: > 0 } budgets)
        {
            return null;
        }

        var current = Current(review, budgets);
        var rows = budgets
            .SelectMany(budget => budget.Rows.Select((value, index) =>
                current.TryGetValue((budget.Key, index), out var entry)
                    ? new ReportCostRow(index, value, entry.Refused, entry.Reason, budget.Key)
                    : new ReportCostRow(index, value, 0m, null, budget.Key)))
            .ToList();

        var total = rows.Aggregate(0m, (sum, row) => Saturating.Add(sum, row.Spent));
        var refused = rows.Aggregate(0m, (sum, row) => Saturating.Add(sum, row.Refused));
        var accepted = Saturating.Subtract(total, refused);
        decimal? refund = awardedGrant is { } grant ? Math.Max(0m, Saturating.Subtract(grant, accepted)) : null;

        return new ReportSettlementResponse(budgets[0].Key, awardedGrant, total, refused, accepted, refund, rows);
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
        if (Spending(form, answers, applicant) is not { Count: > 0 } budgets)
        {
            return (null, new Dictionary<string, string[]>
            {
                ["items"] = ["Wzór sprawozdania nie ma tabeli budżetu (rola \"reportBudget\"), więc kosztów nie da się ocenić pozycja po pozycji."],
            });
        }

        var errors = new Dictionary<string, string[]>();
        var stored = new List<StoredCostReview>();
        var seen = new HashSet<(string, int)>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var key = $"items[{i}]";

            // No budget named: the first, which is the only one of a report
            // form with a single budget table.
            var budget = item.Budget is null ? budgets[0] : budgets.FirstOrDefault(x => x.Key == item.Budget);
            if (budget is null)
            {
                errors[key] = [$"Sprawozdanie nie ma tabeli budżetu \"{item.Budget}\"."];
                continue;
            }

            var where = budgets.Count > 1 ? $" w tabeli \"{budget.Label}\"" : "";

            if (item.Row < 0 || item.Row >= budget.Rows.Count)
            {
                errors[key] = [$"Budżet nie ma pozycji {item.Row + 1}{where}."];
                continue;
            }

            if (!seen.Add((budget.Key, item.Row)))
            {
                errors[key] = [$"Pozycja {item.Row + 1}{where} jest oceniona dwa razy."];
                continue;
            }

            var reason = item.Reason?.Trim();
            var value = budget.Rows[item.Row];

            if (item.Refused <= 0m || item.Refused > value || decimal.Round(item.Refused, 2) != item.Refused)
            {
                errors[key] = [$"Kwota nieuznana w pozycji {item.Row + 1}{where} musi być większa od zera, "
                    + $"mieć najwyżej dwa miejsca po przecinku i nie przekraczać wydatku z dotacji ({Polish(value)} zł)."];
                continue;
            }

            if (string.IsNullOrEmpty(reason) || reason.Length > ReasonMaxLength)
            {
                errors[key] = [$"Podaj powód nieuznania pozycji {item.Row + 1}{where}, najwyżej {ReasonMaxLength} znaków: wnioskodawca go przeczyta."];
                continue;
            }

            stored.Add(new StoredCostReview(item.Row, value, item.Refused, reason, budget.Key));
        }

        return errors.Count > 0 ? (null, errors) : (InOrder(stored, budgets), null);
    }

    /// <summary>"1400,00": the applicant's and the operator's way of writing an amount, without pl-PL.</summary>
    private static string Polish(decimal value) =>
        value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>The judgements that still match their row; the rest are dropped.</summary>
    public static IReadOnlyList<StoredCostReview> Keep(
        FormDocument form, JsonElement answers, EntityType applicant, IReadOnlyList<StoredCostReview> review) =>
        Spending(form, answers, applicant) is { Count: > 0 } budgets
            ? InOrder(Current(review, budgets).Values, budgets)
            : [];

    /// <summary>Budget by budget in the order of the form, then row by row; every entry names its budget.</summary>
    private static List<StoredCostReview> InOrder(IEnumerable<StoredCostReview> review, IReadOnlyList<Budget> budgets)
    {
        var position = budgets.Select((budget, index) => (budget.Key, index)).ToDictionary(x => x.Key, x => x.index);
        return [.. review
            .Select(entry => entry with { Budget = entry.Budget ?? budgets[0].Key })
            .OrderBy(entry => position.GetValueOrDefault(entry.Budget!, int.MaxValue))
            .ThenBy(entry => entry.Row)];
    }

    private static Dictionary<(string, int), StoredCostReview> Current(IReadOnlyList<StoredCostReview> review, IReadOnlyList<Budget> budgets)
    {
        var byKey = budgets.ToDictionary(x => x.Key);
        return review
            .Select(entry => entry with { Budget = entry.Budget ?? budgets[0].Key })
            .Where(entry => byKey.TryGetValue(entry.Budget!, out var budget)
                && entry.Row >= 0 && entry.Row < budget.Rows.Count && budget.Rows[entry.Row] == entry.Spent)
            .GroupBy(entry => (entry.Budget!, entry.Row))
            .ToDictionary(group => group.Key, group => group.First());
    }

    private sealed record Budget(string Key, string Label, IReadOnlyList<decimal> Rows);

    /// <summary>
    /// The grant spending of each row of every budget table, in the order of
    /// the form, counted the way the form shows it: a table or a cell the
    /// applicant is not asked is worth nothing. T-95: the 2026 report has one
    /// table per part of the budget (A, B, C), and the settlement adds them
    /// all, because the grant was spent on all of them.
    /// </summary>
    private static List<Budget> Spending(FormDocument form, JsonElement answers, EntityType applicant)
    {
        var calculator = new AnswerCalculator(form, answers, applicant);
        var budgets = new List<Budget>();

        foreach (var section in form.Sections)
        foreach (var budget in section.Fields.Where(field => field.Role == FormFieldRole.ReportBudget))
        {
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

            budgets.Add(new Budget(budget.Key, budget.Label, rows));
        }

        return budgets;
    }
}
