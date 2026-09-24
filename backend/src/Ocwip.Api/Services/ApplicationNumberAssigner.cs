using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

/// <summary>
/// Whether AssignAsync actually assigned a number, or found the row already
/// moved out from under it. <see cref="CurrentStatus"/> and
/// <see cref="IsActive"/> are the FRESH values read inside the lock, not
/// whatever the caller had loaded before asking for it, and the caller uses
/// them to tell an already-submitted application apart from a deactivated
/// one when Assigned is false.
/// </summary>
internal readonly record struct ApplicationNumberAssignment(
    bool Assigned, ApplicationStatus CurrentStatus, bool IsActive);

/// <summary>
/// Assigns the application number and flips the status to Submitted, inside
/// one transaction guarded by a per competition advisory lock: the strategy
/// this card had to pick for the numbering race described in
/// docs/model-danych.md and docs/runbook/M4-wnioski.md.
///
/// This is NOT the catch-23505-and-return-409 idiom CompetitionService and
/// AccountService use for their own unique numbers, on purpose. There the
/// number is something a human typed and can retype, so losing the race and
/// getting a 409 back is a fine answer. Here the number is assigned by the
/// system and invisible to the applicant until after it exists, so a 409
/// would ask them to fix something they never touched. The card's own
/// acceptance test wants BOTH concurrent submitters to succeed, each with a
/// distinct number, which a lock gives directly: the second caller's read of
/// the highest existing number only happens once the first caller has
/// committed, so it can never see a value that is about to be taken. See
/// docs/architektura.md for the full writeup.
///
/// Split out of ApplicationSubmissionService because it is a complete piece
/// of write discipline on its own, and because it has to do something the
/// rest of that service does not: re-read the row's status and IsActive
/// FRESH after the lock is held, rather than trust the copy the caller
/// already loaded. Two submit requests for the SAME application share the
/// SAME competition lock, so the second one only reaches the write below
/// after the first one has committed, and by then its own in memory copy of
/// Status is stale. Without the re-read, the second request would silently
/// overwrite the first one's number and append a second "Draft to Submitted"
/// row to an append only table for a transition that already happened.
/// </summary>
internal sealed class ApplicationNumberAssigner
{
    /// <summary>
    /// Zero padded to three digits, matching the seed data's "001"
    /// (docs/model-danych.md) and scripts/seed.py. Grows past three digits
    /// naturally once a competition passes 999 applications; "D3" only sets a
    /// floor, not a ceiling.
    /// </summary>
    private const string NumberFormat = "D3";

    private readonly AppDbContext _context;

    public ApplicationNumberAssigner(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ApplicationNumberAssignment> AssignAsync(
        Application application,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // pg_advisory_XACT_lock, not the session scoped variant: released
        // automatically on commit or rollback, so a crashed request cannot
        // wedge a competition's numbering for every applicant after it.
        // Keyed by a hash of the competition id rather than the id itself,
        // because the lock takes a bigint and hashing is the ordinary way to
        // fold an arbitrary key into one; two unrelated competitions sharing
        // a hash only costs a moment of avoidable waiting, never a wrong
        // number, since the number itself is still computed per competition
        // below.
        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({application.CompetitionId}::text)::bigint)",
            cancellationToken);

        var current = await _context.Applications
            .Where(x => x.Id == application.Id)
            .Select(x => new { x.Status, x.IsActive })
            .SingleAsync(cancellationToken);

        if (current.Status is not ApplicationStatus.Draft || !current.IsActive)
        {
            // Somebody else already submitted this exact application (a
            // double click, a retried request) or deactivated it while this
            // call was waiting for the lock. Either way, this call must not
            // touch the row: rolling back releases the lock without writing
            // a second number or a second history entry for a transition
            // that already happened, or is no longer legal.
            await transaction.RollbackAsync(cancellationToken);
            return new ApplicationNumberAssignment(
                Assigned: false, current.Status, current.IsActive);
        }

        application.Number = await NextNumberAsync(
            application.CompetitionId, cancellationToken);
        application.Status = ApplicationStatus.Submitted;
        application.SubmittedAt = now;

        _context.ApplicationStatusHistory.Add(new ApplicationStatusHistory
        {
            ApplicationId = application.Id,
            FromStatus = ApplicationStatus.Draft,
            ToStatus = ApplicationStatus.Submitted,
            ChangedAt = now,
            ChangedByUserId = userId,
        });

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Reload for the same reason ApplicationService.SaveAndReloadAsync
        // does: timestamptz round trips through PostgreSQL at microsecond
        // precision, one digit short of a .NET tick, so the checksum this
        // call's own response carries has to be computed from what the
        // database actually stored, not from the in-memory value just
        // written.
        await _context.Entry(application).ReloadAsync(cancellationToken);

        return new ApplicationNumberAssignment(
            Assigned: true, ApplicationStatus.Submitted, IsActive: true);
    }

    /// <summary>
    /// One past the highest number already assigned in this competition.
    /// Aggregated in SQL rather than pulling every number into memory and
    /// parsing it in .NET: the read happens while the advisory lock is held,
    /// so every row it costs is a row every other concurrent submitter in
    /// this competition is blocked waiting behind.
    ///
    /// Every value in the column was written by this same method, always in
    /// this exact zero padded format, so casting straight to integer in SQL
    /// is safe for data this service produced; it is not a general purpose
    /// parse of a free text column.
    /// </summary>
    private async Task<string> NextNumberAsync(
        Guid competitionId, CancellationToken cancellationToken)
    {
        // EF's scalar SqlQuery<T> wraps the raw SQL in a subquery and reads
        // it back through a column literally named "Value": without the
        // alias, quoted so PostgreSQL does not lowercase it away, EF asks for
        // a column that never existed and the query fails at 42703.
        var highest = await _context.Database
            .SqlQuery<int>(
                $"""
                SELECT COALESCE(MAX(number::integer), 0) AS "Value"
                FROM applications
                WHERE competition_id = {competitionId}
                """)
            .SingleAsync(cancellationToken);

        return (highest + 1).ToString(NumberFormat, CultureInfo.InvariantCulture);
    }
}
