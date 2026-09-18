using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

/// <summary>
/// The competition lifecycle, in one place (T-20).
///
/// Nothing here compares two CompetitionStatus values to decide whether
/// something may happen: that question goes to CompetitionStatusTransitions,
/// and the question "where is this competition now" goes to
/// CompetitionLifecycle. The reason is R-17: the report has states the cards
/// do not, and every one of them is cheap to add only as long as the rules
/// about them live in one table instead of in the branches of this file.
/// </summary>
internal sealed class CompetitionService : ICompetitionService
{
    /// <summary>
    /// The name EF gives the unique index on the number, see
    /// CompetitionConfiguration. Named rather than matched on a message, so a
    /// different unique violation is never reported as a duplicate number.
    /// </summary>
    private const string NumberIndex = "ix_competitions_number";

    private readonly AppDbContext _context;
    private readonly TimeProvider _time;

    /// <summary>
    /// The clock is injected rather than read from DateTimeOffset.UtcNow,
    /// because the effective state IS a function of it: the boundary cases
    /// worth testing are a minute either side of the closing minute, and a
    /// test that can only observe the real clock cannot reach them.
    /// </summary>
    public CompetitionService(AppDbContext context, TimeProvider time)
    {
        _context = context;
        _time = time;
    }

    public async Task<CompetitionResult> CreateAsync(
        CompetitionRequest request,
        CancellationToken cancellationToken)
    {
        if (await NumberIsTakenAsync(request.Number, null, cancellationToken))
        {
            return new CompetitionResult(CompetitionOutcome.NumberTaken);
        }

        // A form version belongs to a competition, so a competition that does
        // not exist yet cannot have one. Answered here rather than left to the
        // foreign key, which would report the same thing as a 500.
        if (request.FormDefinitionId is not null)
        {
            return new CompetitionResult(CompetitionOutcome.UnknownFormDefinition);
        }

        var competition = new Competition
        {
            // Always a draft. A competition does not arrive published: T-22
            // makes publication a separate, confirmed act, because a published
            // competition is visible and starts accepting applications, and
            // taking that back costs the organisation its reputation with the
            // people who had already started filling the form in.
            Status = CompetitionStatus.Draft,
        };

        Apply(request, competition);

        _context.Competitions.Add(competition);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsNumberTaken(exception))
        {
            // The check above is a SELECT, and a SELECT loses the race against
            // a second operator pressing save in the same moment. The unique
            // index answered instead, and the caller gets the same 409 either
            // way rather than a 500 whose body names an internal type. Same
            // pattern as the duplicate address in AccountService.
            return new CompetitionResult(CompetitionOutcome.NumberTaken);
        }

        return Success(competition);
    }

    public async Task<CompetitionResult> UpdateAsync(
        Guid id,
        CompetitionRequest request,
        CancellationToken cancellationToken)
    {
        var competition = await FindAsync(id, cancellationToken);

        if (competition is null)
        {
            return new CompetitionResult(CompetitionOutcome.NotFound);
        }

        if (!competition.IsActive)
        {
            return new CompetitionResult(CompetitionOutcome.Inactive);
        }

        if (await NumberIsTakenAsync(request.Number, id, cancellationToken))
        {
            return new CompetitionResult(CompetitionOutcome.NumberTaken);
        }

        if (request.FormDefinitionId is { } formDefinitionId
            && !await FormDefinitionBelongsAsync(
                id, formDefinitionId, cancellationToken))
        {
            return new CompetitionResult(CompetitionOutcome.UnknownFormDefinition);
        }

        // Editing stays open past Draft on purpose. The report expects a
        // competition with applications already in it to be editable and to
        // warn the operator while they do it (the warning is T-22, on the
        // screen that can show it). Refusing the edit here instead would not
        // make anything safer: the form itself is versioned, so an application
        // in progress does not break, and the parameters that do bite - the
        // money limits - are exactly the ones an operator has to be able to
        // correct after a mistake.
        Apply(request, competition);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsNumberTaken(exception))
        {
            return new CompetitionResult(CompetitionOutcome.NumberTaken);
        }

        return Success(competition);
    }

    public async Task<CompetitionResult> ChangeStatusAsync(
        Guid id,
        CompetitionStatus target,
        CancellationToken cancellationToken)
    {
        var competition = await FindAsync(id, cancellationToken);

        if (competition is null)
        {
            return new CompetitionResult(CompetitionOutcome.NotFound);
        }

        if (!competition.IsActive)
        {
            return new CompetitionResult(CompetitionOutcome.Inactive);
        }

        // From where it EFFECTIVELY is, not from the column. A competition
        // stored as Published whose closing date has passed is closed, and an
        // operator has to be able to start reviewing it without anything
        // having rewritten the row first.
        var current = CompetitionLifecycle.Effective(competition, _time.GetUtcNow());

        if (!CompetitionStatusTransitions.AllowsOperator(current, target))
        {
            return new CompetitionResult(
                CompetitionOutcome.TransitionNotAllowed,
                CurrentStatus: current);
        }

        competition.Status = target;

        if (target is CompetitionStatus.Published)
        {
            competition.PublishedAt = _time.GetUtcNow();
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Success(competition);
    }

    public async Task<CompetitionResult> DeactivateAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var competition = await FindAsync(id, cancellationToken);

        if (competition is null)
        {
            return new CompetitionResult(CompetitionOutcome.NotFound);
        }

        // Idempotent: asking for the state something is already in is not an
        // error, and answering with one only teaches the panel to show a
        // failure for an action that succeeded the first time.
        if (competition.IsActive)
        {
            competition.IsActive = false;

            // Paired with the flag by a check constraint, so the two are set
            // together or the row is refused.
            competition.DeactivatedAt = _time.GetUtcNow();

            await _context.SaveChangesAsync(cancellationToken);
        }

        return Success(competition);
    }

    public async Task<CompetitionResult> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var competition = await FindAsync(id, cancellationToken, tracking: false);

        return competition is null
            ? new CompetitionResult(CompetitionOutcome.NotFound)
            : Success(competition);
    }

    public async Task<IReadOnlyList<CompetitionResponse>> ListAsync(
        CancellationToken cancellationToken)
    {
        var competitions = await _context.Competitions
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var now = _time.GetUtcNow();

        return [.. competitions.Select(x => ToResponse(x, now))];
    }

    public async Task<IReadOnlyList<PublicCompetitionResponse>> ListPublicAsync(
        CancellationToken cancellationToken)
    {
        var competitions = await _context.Competitions
            .AsNoTracking()
            // Both filters are on the stored status, which is safe precisely
            // because neither of them is a state the clock can produce: the
            // scheduled transitions only ever move a competition between
            // Published, OpenForApplications and Closed. Draft is out because
            // it has no public address at all, and Archived because it is the
            // state for leaving the current listing without disappearing, see
            // the permalink in GetPublicAsync.
            .Where(x => x.IsActive
                && x.Status != CompetitionStatus.Draft
                && x.Status != CompetitionStatus.Archived)
            .OrderByDescending(x => x.StartDate)
            .ToListAsync(cancellationToken);

        var now = _time.GetUtcNow();

        return [.. competitions.Select(x => ToPublicResponse(x, now))];
    }

    public async Task<PublicCompetitionResponse?> GetPublicAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var competition = await FindAsync(id, cancellationToken, tracking: false);

        // An archived competition keeps its address, unlike a draft, which
        // never had one. The permanent link is what a results archive and a
        // post on social media both depend on (R-14), and a link that stops
        // working the day the competition is filed away is not permanent.
        if (competition is null
            || !competition.IsActive
            || !CompetitionLifecycle.IsPubliclyVisible(competition.Status))
        {
            return null;
        }

        return ToPublicResponse(competition, _time.GetUtcNow());
    }

    private Task<Competition?> FindAsync(
        Guid id,
        CancellationToken cancellationToken,
        bool tracking = true)
    {
        var query = tracking
            ? _context.Competitions
            : _context.Competitions.AsNoTracking();

        return query.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <summary>
    /// Checked here as well as by the unique index, because the index answers
    /// with a 500 and this answers with the name of the field to change.
    /// The index stays, because it is the half that is still true when two
    /// operators press save in the same second.
    /// </summary>
    private Task<bool> NumberIsTakenAsync(
        string number,
        Guid? exceptId,
        CancellationToken cancellationToken) =>
        _context.Competitions.AnyAsync(
            x => x.Number == number
                && x.IsActive
                && (exceptId == null || x.Id != exceptId),
            cancellationToken);

    /// <summary>
    /// Matches the index this check shadows, including its is_active filter.
    /// A deactivated competition holds no number.
    /// </summary>
    private static bool IsNumberTaken(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres
        && postgres.SqlState == PostgresErrorCodes.UniqueViolation
        && postgres.ConstraintName == NumberIndex;

    private Task<bool> FormDefinitionBelongsAsync(
        Guid competitionId,
        Guid formDefinitionId,
        CancellationToken cancellationToken) =>
        _context.FormDefinitions.AnyAsync(
            x => x.Id == formDefinitionId && x.CompetitionId == competitionId,
            cancellationToken);

    /// <summary>
    /// Copies the settings from the request onto the entity. The status is not
    /// among them, and neither is PublishedAt: both are records of something
    /// that happened rather than fields somebody fills in.
    /// </summary>
    private static void Apply(CompetitionRequest request, Competition competition)
    {
        competition.Number = request.Number;
        competition.Title = request.Title;
        competition.Description = request.Description;
        competition.StartDate = request.StartDate;
        competition.EndDate = request.EndDate;
        competition.IsContinuousIntake = request.IsContinuousIntake;
        competition.MaxGrantAmount = request.MaxGrantAmount;
        competition.FormDefinitionId = request.FormDefinitionId;
    }

    private CompetitionResult Success(Competition competition) =>
        new(CompetitionOutcome.Succeeded,
            ToResponse(competition, _time.GetUtcNow()));

    private static CompetitionResponse ToResponse(
        Competition competition,
        DateTimeOffset now)
    {
        var status = CompetitionLifecycle.Effective(competition, now);

        // Empty for an inactive competition, and not because the table says
        // so: nothing moves through the lifecycle once the row is deactivated,
        // and this field exists to tell a panel which buttons to draw. Left as
        // the table's answer it would draw a "publikuj" button that answers
        // 409 every time it is pressed.
        var allowed = competition.IsActive
            ? CompetitionStatusTransitions.OperatorTargets(status)
            : [];

        return new CompetitionResponse(
            competition.Id,
            competition.Number,
            competition.Title,
            competition.Description,
            status,
            allowed,
            competition.StartDate,
            competition.EndDate,
            competition.IsContinuousIntake,
            competition.MaxGrantAmount,
            competition.FormDefinitionId,
            competition.PublishedAt,
            competition.IsActive,
            competition.CreatedAt,
            competition.UpdatedAt);
    }

    private static PublicCompetitionResponse ToPublicResponse(
        Competition competition,
        DateTimeOffset now) =>
        new(competition.Id,
            competition.Number,
            competition.Title,
            competition.Description,
            CompetitionLifecycle.Effective(competition, now),
            competition.StartDate,
            competition.EndDate,
            competition.IsContinuousIntake,
            competition.MaxGrantAmount);
}
