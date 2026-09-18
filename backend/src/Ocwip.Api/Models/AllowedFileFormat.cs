using System.Text.Json.Serialization;

namespace Ocwip.Api.Models
{
    /// <summary>
    /// File formats an attachment may be handed in as (step 1.5). A closed set
    /// rather than free text, because the value is read by the upload check in
    /// T-32 and ".doc " typed by a person is not a format.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<AllowedFileFormat>))]
    public enum AllowedFileFormat
    {
        Pdf,
        Doc,
        Docx,
        Xls,
        Xlsx,
        Jpg,
        Odt,
        Ods
    }
}
