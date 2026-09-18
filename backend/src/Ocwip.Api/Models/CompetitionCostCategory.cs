namespace Ocwip.Api.Models
{
    /// <summary>
    /// One cost category this competition allows in an application budget
    /// (step 1.4, R-12).
    ///
    /// Presence is the setting: a category with no row here is switched off,
    /// which hides its budget table and its descriptive section in the part
    /// about the project. A flag column instead of a missing row would mean two
    /// ways to say "off" and one of them silent.
    /// </summary>
    public class CompetitionCostCategory
    {
        public Guid Id { get; set; }

        public Guid CompetitionId { get; set; }
        public Competition Competition { get; set; } = null!;

        public CostCategory Category { get; set; }

        /// <summary>The order the categories are shown in.</summary>
        public int Position { get; set; }
    }
}
