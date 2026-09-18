using System.Text.Json.Serialization;

namespace Ocwip.Api.Models
{
    /// <summary>
    /// A cost category an application budget may use (R-12 in
    /// docs/runbook/rozbieznosci.md, step 1.4 of the wizard).
    ///
    /// Which of these a competition allows is a setting of that competition,
    /// stored as rows in competition_cost_categories, not a constant of the
    /// system. Switching a category off hides both its table in the budget and
    /// the matching descriptive section in the part about the project, which is
    /// why the choice has to be readable in one place rather than inferred from
    /// the presence of figures.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<CostCategory>))]
    public enum CostCategory
    {
        /// <summary>"Koszty bezpośrednie".</summary>
        DirectCosts,

        /// <summary>"Rozwój instytucjonalny".</summary>
        InstitutionalDevelopment,

        /// <summary>"Koszty pośrednie".</summary>
        IndirectCosts
    }
}
