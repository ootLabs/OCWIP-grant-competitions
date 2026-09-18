namespace Ocwip.Api.Models
{
    /// <summary>
    /// A person answering questions about this competition (step 1.6 of the
    /// wizard).
    ///
    /// A row pointing at a staff account rather than a copy of a name and an
    /// address, because the contact is shown publicly (docs/runbook/pola.md
    /// says: to a guest with no account too) and a copy taken at announcement
    /// time is what still shows the address of somebody who left.
    /// </summary>
    public class CompetitionContact
    {
        public Guid Id { get; set; }

        public Guid CompetitionId { get; set; }
        public Competition Competition { get; set; } = null!;

        /// <summary>
        /// The staff account. Only an operator may be picked, which is checked
        /// in CompetitionService: the database knows the role is a column, not
        /// that this particular column has to hold one value.
        /// </summary>
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        /// <summary>The order the contacts are listed in, see the same field
        /// on CompetitionAttachment.</summary>
        public int Position { get; set; }
    }
}
