using System.Text.Json;

namespace Ocwip.Api.Models
{
    public class FormDefinition
    {
        public Guid Id { get; set; }
        public Guid CompetitionId { get; set; }
        public Competition Competition { get; set; } = null!;
        public bool IsActive { get; set; }

        /// <summary>
        /// Version number of the form definition.
        /// </summary>
        public int VersionNumber { get; set; }

        /// <summary>
        /// Form structure stored as PostgreSQL JSONB.
        /// The JSON contract, including sections, fields and validations,
        /// is intentionally not defined in this sprint.
        /// </summary>
        public JsonElement Definition { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime ClosedAt { get; set; }

    }
}
