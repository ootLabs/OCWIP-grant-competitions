using System.Text.Json;

namespace Ocwip.Api.Models
{
    public class FormDefinition : IAuditedEntity
    {
        public Guid Id { get; set; }

        public Guid CompetitionId { get; set; }
        public Competition Competition { get; set; } = null!;

        /// <summary>
        /// Version number of the form definition. Unique per competition, see
        /// FormDefinitionConfiguration.
        /// </summary>
        public int VersionNumber { get; set; }

        /// <summary>
        /// Form structure stored as PostgreSQL JSONB.
        /// The JSON contract, including sections, fields and validations,
        /// is intentionally not defined here: it is decided in card T-20.
        /// </summary>
        public JsonElement Definition { get; set; }

        /// <summary>
        /// Soft delete flag, see the same field on Competition.
        /// </summary>
        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>
        /// When the row was marked inactive. Null while it is active.
        /// </summary>
        public DateTimeOffset? DeactivatedAt { get; set; }
    }
}
