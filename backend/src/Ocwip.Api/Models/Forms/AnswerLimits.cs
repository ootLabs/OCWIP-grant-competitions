namespace Ocwip.Api.Models.Forms;

/// <summary>
/// Limits measured against the answers (T-30, decision D12): not "at most
/// 10%", but "you may enter at most 900,00 zł", computed for the application
/// as it stands.
///
/// That is why a limit is stored as a rule plus a basis rather than as a yes
/// or no check (docs/kontrakt-formularza.md, "Limity"): the ceiling can be
/// computed, and the ceiling is what the applicant needs to hear. With a grant
/// that is itself computed from the contributions (D11), working it out by
/// hand is genuinely hard.
/// </summary>
internal static class AnswerLimits
{
    /// <summary>
    /// The competition settings a limit may be measured against, by the name
    /// the document writes them with (FormSchemaReferences). A setting the
    /// operator left empty is null, and a limit on it is not checked: there is
    /// no ceiling to exceed, and inventing one would refuse an application
    /// the rules allow.
    /// </summary>
    public static IReadOnlyDictionary<string, decimal?> BasesFor(Competition competition) =>
        new Dictionary<string, decimal?>(StringComparer.Ordinal)
        {
            ["competition.maxGrantAmount"] = competition.MaxGrantAmount,
            ["competition.minGrantAmount"] = competition.MinGrantAmount,
            ["competition.totalPoolAmount"] = competition.TotalPoolAmount,
            ["competition.maxIndirectCostPercent"] = competition.MaxIndirectCostPercent,
            ["competition.maxInstitutionalDevelopmentPercent"] =
                competition.MaxInstitutionalDevelopmentPercent,
            ["competition.maxAverageAnnualRevenue"] = competition.MaxAverageAnnualRevenue,
        };

    /// <summary>
    /// The first limit the value exceeds, or null.
    /// </summary>
    /// <param name="basis">
    /// The value a basis or a percentage setting stands for right now, or
    /// null when it cannot be resolved. The caller resolves it, because inside
    /// a table a basis names a sibling cell first.
    /// </param>
    public static LimitBreach? Find(
        FormField field,
        decimal value,
        Func<string, decimal?> basis)
    {
        foreach (var limit in field.Limits)
        {
            if (Allowed(limit, basis) is not { } allowed || value <= allowed)
            {
                continue;
            }

            return new LimitBreach(allowed, Saturating.Subtract(value, allowed), IsPercent(field));
        }

        return null;
    }

    /// <summary>The message for the first limit the value exceeds, or null.</summary>
    public static string? Check(FormField field, decimal value, Func<string, decimal?> basis) =>
        Find(field, value, basis)?.Message();

    /// <summary>
    /// The ceiling a limit sets for the application as it stands, or null
    /// when there is none: a basis that cannot be resolved, or a percentage
    /// setting the operator left empty (the cost category has no threshold in
    /// this competition, T-31).
    /// </summary>
    private static decimal? Allowed(FormLimit limit, Func<string, decimal?> basis)
    {
        if (basis(limit.Basis) is not { } measure)
        {
            return null;
        }

        if (limit.Kind != FormLimitKind.MaxPercentOf)
        {
            return measure;
        }

        var percent = limit.PercentFrom is { } setting ? basis(setting) : limit.Percent;

        return percent is { } share
            ? Saturating.Divide(Saturating.Multiply(measure, share), 100m)
            : null;
    }

    /// <summary>
    /// A ratio is a percentage (AnswerCalculator multiplies it by a hundred
    /// for that reason), so its message needs the unit a percent field gets.
    /// </summary>
    private static bool IsPercent(FormField field) =>
        field.Type == FormFieldType.Percent
        || field.Calculation?.Kind == FormCalculationKind.Ratio;
}

/// <summary>
/// A limit exceeded: by how much, and what the ceiling is for this
/// application (D12). Both, because "za dużo" without the number sends the
/// applicant to a calculator, and the number without the ceiling leaves them
/// guessing what they may still enter.
/// </summary>
internal sealed record LimitBreach(decimal Allowed, decimal Over, bool IsPercent)
{
    public string Message() =>
        $"Przekroczono dopuszczalną wartość o {Format(Over)}. Maksymalnie {Format(Allowed)}.";

    public string Format(decimal value) =>
        IsPercent ? PolishNumbers.Percent(value) : PolishNumbers.Amount(value);
}
