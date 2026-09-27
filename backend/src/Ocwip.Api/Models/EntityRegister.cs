using System.Text.Json.Serialization;

namespace Ocwip.Api.Models
{
    /// <summary>
    /// "Rejestr" on the organisation card (T-93): KRS, whose number is ten
    /// digits, or any other register, whose number is free text (a sports
    /// club in the starosta's register, a rural women's circle in ARiMR's).
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<EntityRegister>))]
    public enum EntityRegister
    {
        Krs,
        Other
    }
}
