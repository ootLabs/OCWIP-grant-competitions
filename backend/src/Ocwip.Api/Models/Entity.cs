namespace Ocwip.Api.Models
{
    /// <summary>
    /// The party that files an application: an organisation or an informal
    /// group. Not a login account, see <see cref="User"/>.
    ///
    /// One entity with a type column, not three tables. The three types differ
    /// in their DATA, not in the way they log in, so NIP and address are
    /// nullable and their requiredness follows the type. An entity with no NIP
    /// is not broken data, it is an informal group.
    /// </summary>
    public class Entity : IAuditedEntity
    {
        public Guid Id { get; set; }

        public EntityType Type { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Sensitive Information. In scope for encryption at rest in T-80: for
        /// an informal group these are a natural person's contact details, the
        /// same as <see cref="Address"/>.
        /// </summary>
        public string ContactInformation { get; set; } = string.Empty;

        /// <summary>
        /// Sensitive Information. In scope for encryption at rest in T-80.
        ///
        /// Required for an organisation only. That rule is NOT a NOT NULL and
        /// not a check constraint either: we do not know whether an informal
        /// group under an organisation's patronage quotes the patron's NIP, so
        /// the schema would be guessing. Type dependent validation sits at the
        /// API edge, see docs/konwencje.md.
        /// </summary>
        public string? Nip { get; set; }

        /// <summary>
        /// Sensitive Information. In scope for encryption at rest in T-80: for
        /// an informal group this is the address of a natural person.
        ///
        /// Required for an organisation only, same reasoning as Nip.
        /// </summary>
        public string? Address { get; set; }

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

        /// <summary>
        /// The account this entity belongs to. One to one today, and that is an
        /// assumption to confirm, see <see cref="User.EntityId"/>.
        /// </summary>
        public User? User { get; set; }

        public ICollection<Application> Applications { get; set; } = [];
    }
}
