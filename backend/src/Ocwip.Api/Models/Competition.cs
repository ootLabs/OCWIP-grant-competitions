namespace Ocwip.Api.Models
{
    public class Competition
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        /// <summary>
        /// Stored in UTC. The conversion is enforced by
        /// <c>UtcDateTimeOffsetConverter</c>, applied model wide in
        /// <c>AppDbContext.ConfigureConventions</c>.
        /// </summary>
        public DateTimeOffset StartDate { get; set; }
        public DateTimeOffset EndDate { get; set; }

        public decimal MaxGrantAmount { get; set; }
        public CompetitionStatus Status { get; set; }

        /// <summary>
        /// Soft delete flag. Rows are never removed: AGENTS.md, security rule 5,
        /// keeps a minimum retention of 5 years, so deletion means marking the
        /// row inactive.
        /// </summary>
        public bool IsActive { get; set; } = true;

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        /// <summary>
        /// When the row was marked inactive. Null while it is active, so an
        /// active competition never carries a fake date.
        /// </summary>
        public DateTimeOffset? DeactivatedAt { get; set; }

        public ICollection<FormDefinition> FormDefinitions { get; set; } = [];
    }
}
