using System.Text.Json.Serialization;

namespace Ocwip.Api.Models
{
    /// <summary>
    /// "Forma prawna" on the organisation card (T-93), the five options of
    /// docs/runbook/pola.md, step 2.2. <see cref="Other"/> reveals a text
    /// field, <see cref="Entity.LegalFormOther"/>.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<LegalForm>))]
    public enum LegalForm
    {
        Association,
        Foundation,
        SportsClub,
        RuralWomenCircle,
        Other
    }
}
