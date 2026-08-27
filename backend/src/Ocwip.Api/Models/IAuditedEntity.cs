namespace Ocwip.Api.Models
{
    /// <summary>
    /// An entity that records when it was created and last changed.
    ///
    /// The columns carry a database default of now(), which covers inserts that
    /// bypass the change tracker. That default fires on INSERT only, so without
    /// the stamping in <c>AppDbContext.SaveChanges</c> an updated row would keep
    /// reporting its creation instant forever, and a column named updated_at that
    /// never updates is worse than no column at all.
    /// </summary>
    public interface IAuditedEntity
    {
        DateTimeOffset CreatedAt { get; set; }
        DateTimeOffset UpdatedAt { get; set; }
    }
}
