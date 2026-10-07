namespace Ocwip.Api.Models
{
    /// <summary>
    /// One person's access to one Podmiot card (T-93a, report step 2.2,
    /// decision 7). Access goes with the organisation, not with the person who
    /// clicked "nowy wniosek": whoever is a member sees every application of
    /// the card, drafts included, and may finish somebody else's draft.
    ///
    /// A person may be a member of several cards and a card may have several
    /// members. The founder is the account that created the card; only the
    /// founder decides who joins, and after seven days without an answer an
    /// operator may (EntityAccessRequest).
    ///
    /// An informal group without a patron has a card with a name and no NIP
    /// (R-37), so nobody can ask to join it: its drafts stay with the one
    /// person who started them, which is what the report says for that type.
    /// </summary>
    public class EntityMember : IAuditedEntity
    {
        public Guid Id { get; set; }

        public Guid EntityId { get; set; }
        public Entity Entity { get; set; } = null!;

        public Guid UserId { get; set; }

        /// <summary>The account that created the card, and so decides who else joins it.</summary>
        public bool IsFounder { get; set; }

        /// <summary>
        /// The request this membership came from, null for the founder. Who
        /// approved it and when, founder or operator, is on that request, so
        /// it is said once.
        /// </summary>
        public Guid? AccessRequestId { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset? DeactivatedAt { get; set; }
    }
}
