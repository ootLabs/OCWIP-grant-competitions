namespace Ocwip.Api.Models
{
    /// <summary>
    /// One person appointed to the committee of one competition (R-44,
    /// report step 5.1 and the roles table): "tylko operator, imiennie, na
    /// konkurs". Being an expert is this row, not a property of the account:
    /// the chair of a foundation files an application in one competition and
    /// evaluates other people's in another, with one login.
    ///
    /// An appointment is what lets an assignment exist (ApplicationAssignment)
    /// and what EntityScopedHandler and EvaluationAccessHandler read before
    /// they let an expert see an application. Withdrawn, never deleted.
    /// </summary>
    public class CompetitionExpert : IAuditedEntity
    {
        public Guid Id { get; set; }

        public Guid CompetitionId { get; set; }

        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        /// <summary>The operator who appointed them; null for appointments carried over by the migration.</summary>
        public Guid? AppointedById { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? DeactivatedAt { get; set; }
    }
}
