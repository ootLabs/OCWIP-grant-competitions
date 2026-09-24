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
    /// The message for the first limit the value exceeds, or null.
    /// </summary>
    /// <param name="basis">
    /// The value a basis stands for right now, or null when it cannot be
    /// resolved. The caller resolves it, because inside a table a basis names
    /// a sibling cell first.
    /// </param>
    public static string? Check(
        FormField field,
        decimal value,
        Func<string, decimal?> basis)
    {
        // A ratio is a percentage (AnswerCalculator multiplies it by a hundred
        // for that reason), so its message needs the unit a percent field
        // gets.
        var isPercent = field.Type == FormFieldType.Percent
            || field.Calculation?.Kind == FormCalculationKind.Ratio;

        foreach (var limit in field.Limits)
        {
            if (basis(limit.Basis) is not { } measure)
            {
                continue;
            }

            var allowed = limit.Kind == FormLimitKind.MaxPercentOf
                ? Saturating.Divide(Saturating.Multiply(measure, limit.Percent ?? 0m), 100m)
                : measure;

            if (value <= allowed)
            {
                continue;
            }

            var over = Saturating.Subtract(value, allowed);

            return isPercent
                ? $"Przekroczono dopuszczalną wartość o {PolishNumbers.Percent(over)}. "
                  + $"Maksymalnie {PolishNumbers.Percent(allowed)}."
                : $"Przekroczono dopuszczalną wartość o {PolishNumbers.Amount(over)}. "
                  + $"Maksymalnie {PolishNumbers.Amount(allowed)}.";
        }

        return null;
    }
}
