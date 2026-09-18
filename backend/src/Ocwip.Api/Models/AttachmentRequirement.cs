using System.Text.Json.Serialization;

namespace Ocwip.Api.Models
{
    /// <summary>
    /// How strongly a competition wants one attachment (step 1.5 of the
    /// wizard). Three variants and not the six the current tool has, because
    /// the report asked for exactly these three; adding a fourth is a member
    /// here plus a branch wherever the requirement is read.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AttachmentRequirement>))]
    public enum AttachmentRequirement
    {
        /// <summary>"Wymagany".</summary>
        Required,

        /// <summary>"Niewymagalny".</summary>
        Optional,

        /// <summary>
        /// "Wymagany warunkowo": required only when the applicant is in a
        /// register other than KRS. The condition is evaluated against the
        /// organisation card when an application is filled in (M4); here it is
        /// only recorded.
        /// </summary>
        RequiredOutsideKrs
    }
}
