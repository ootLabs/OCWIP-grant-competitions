using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;
using Ocwip.Api.Models.Forms;

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

        var contacts = await FindContactsAsync(request.ContactUserIds, cancellationToken);

        if (contacts is null)
        {
            return new CompetitionResult(CompetitionOutcome.UnknownContact);
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

        Apply(request, competition, contacts);

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

        var contacts = await FindContactsAsync(request.ContactUserIds, cancellationToken);

        if (contacts is null)
        {
            return new CompetitionResult(CompetitionOutcome.UnknownContact);
        }

        // Editing stays open past Draft on purpose. The report expects a
        // competition with applications already in it to be editable and to
        // warn the operator while they do it (the warning is T-22, on the
        // screen that can show it). Refusing the edit here instead would not
        // make anything safer: the form itself is versioned, so an application
        // in progress does not break, and the parameters that do bite - the
        // money limits - are exactly the ones an operator has to be able to
        // correct after a mistake.
        Apply(request, competition, contacts);

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

        // A published competition takes applications from its start date, so
        // it has to have everything an application and its evaluation need
        // by then (T-97). Checked here, on the operator's move, and not by
        // the schema: a draft without cards is a normal draft.
        if (target is CompetitionStatus.Published && PublicationGaps(competition) is { Count: > 0 } gaps)
        {
            return new CompetitionResult(CompetitionOutcome.PublicationIncomplete, Gaps: gaps);
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

    public async Task<CompetitionResult> RestoreAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var competition = await FindAsync(id, cancellationToken);

        if (competition is null)
        {
            return new CompetitionResult(CompetitionOutcome.NotFound);
        }

        if (!competition.IsActive)
        {
            competition.IsActive = true;
            competition.DeactivatedAt = null;

            // The number index is filtered by is_active (R-26), so another
            // competition may have taken the number while this one was out.
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsNumberTaken(exception))
            {
                return new CompetitionResult(CompetitionOutcome.NumberTaken);
            }
        }

        return Success(competition);
    }

    /// <summary>
    /// What a draft still needs before publication (T-97): what an applicant
    /// fills in and what the evaluation reads. The report form is not here:
    /// it is needed after the results, months later.
    /// </summary>
    internal static IReadOnlyList<string> PublicationGaps(Competition competition)
    {
        var gaps = new List<string>();

        if (competition.FormDefinitionId is null)
        {
            gaps.Add("Brak opublikowanego formularza wniosku.");
        }

        if (competition.FormalCardDefinitionId is null)
        {
            gaps.Add("Brak karty oceny formalnej.");
        }

        if (competition.MeritCardDefinitionId is null)
        {
            gaps.Add("Brak karty oceny merytorycznej.");
        }

        return gaps;
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
        var competitions = await WithParameters(_context.Competitions.AsNoTracking())
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var now = _time.GetUtcNow();

        return [.. competitions.Select(x => ToResponse(x, now))];
    }

    public async Task<IReadOnlyList<PublicCompetitionResponse>> ListPublicAsync(
        CancellationToken cancellationToken)
    {
        var competitions = await WithParameters(_context.Competitions.AsNoTracking())
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
            ? WithParameters(_context.Competitions)
            : WithParameters(_context.Competitions.AsNoTracking());

        return query.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    /// <summary>
    /// The wizard parameters that live in their own tables (T-20a). Loaded
    /// with the competition rather than lazily, because every caller here maps
    /// the whole thing to a response and a lazy load would turn one listing
    /// into a query per row.
    /// </summary>
    private static IQueryable<Competition> WithParameters(
        IQueryable<Competition> query) =>
        query
            .Include(x => x.Attachments)
            .Include(x => x.CostCategories)
            .Include(x => x.Contacts)
            .ThenInclude(x => x.User)

            // One query per collection instead of one join of all three. Three
            // collections in a single query multiply the competition row by
            // attachments times categories times contacts, and that row carries
            // up to four columns of ten thousand characters, so the listing
            // would send the announcement text back a dozen times per
            // competition.
            .AsSplitQuery();

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
            x => x.Id == formDefinitionId
                && x.CompetitionId == competitionId
                // An evaluation card is a version of this competition too
                // (T-38), and must never become the form applicants fill in.
                && x.Purpose == FormPurpose.Application,
            cancellationToken);

    /// <summary>
    /// Copies the settings from the request onto the entity. The status is not
    /// among them, and neither is PublishedAt: both are records of something
    /// that happened rather than fields somebody fills in.
    /// </summary>
    private void Apply(
        CompetitionRequest request,
        Competition competition,
        IReadOnlyDictionary<Guid, User> contacts)
    {
        competition.Number = request.Number;
        competition.Title = request.Title;
        competition.Description = request.Description;
        competition.StartDate = request.StartDate;
        competition.EndDate = request.EndDate;
        competition.IsContinuousIntake = request.IsContinuousIntake;
        competition.MaxGrantAmount = request.MaxGrantAmount;
        // Only when the request names one. A null here is "I am not changing
        // the form version", not "take the form away": since T-25 the column
        // is set by publishing a version, and an edit of the dates that leaves
        // the field out of the body would otherwise un-publish the form of a
        // competition whose intake is open, silently and with a 200.
        if (request.FormDefinitionId is { } requestedFormDefinitionId)
        {
            competition.FormDefinitionId = requestedFormDefinitionId;
        }

        // Steps 1.2 to 1.6 (T-20a).
        competition.ExpectedResults = request.ExpectedResults;
        competition.RulesUrl = request.RulesUrl;
        competition.SubmissionNotice = request.SubmissionNotice;
        competition.SubmissionEmailBody = request.SubmissionEmailBody;
        competition.RequiresPaperSubmission = request.RequiresPaperSubmission;
        competition.PaperSubmissionDeadline = request.PaperSubmissionDeadline;
        competition.PaperSubmissionAddress = request.PaperSubmissionAddress;
        competition.ProjectStartDate = request.ProjectStartDate;
        competition.ProjectEndDate = request.ProjectEndDate;
        competition.TotalPoolAmount = request.TotalPoolAmount;
        competition.MinGrantAmount = request.MinGrantAmount;
        competition.MaxIndirectCostPercent = request.MaxIndirectCostPercent;
        competition.MaxInstitutionalDevelopmentPercent =
            request.MaxInstitutionalDevelopmentPercent;
        competition.PercentageBasis = request.PercentageBasis;
        competition.MaxAverageAnnualRevenue = request.MaxAverageAnnualRevenue;
        competition.PersonalDataProcessedUntil = request.PersonalDataProcessedUntil;
        competition.MaxAttachmentSizeInBytes = request.MaxAttachmentSizeInBytes
            ?? Competition.DefaultMaxAttachmentSizeInBytes;
        competition.MaxApplicationSizeInBytes = request.MaxApplicationSizeInBytes
            ?? Competition.DefaultMaxApplicationSizeInBytes;

        ApplyCostCategories(request, competition);
        ApplyAttachments(request, competition);
        ApplyContacts(request, competition, contacts);
    }

    /// <summary>
    /// Drops child rows the request no longer carries.
    ///
    /// Through the context and not by clearing the navigation, because the
    /// relationship is NoAction like every other one here (rule 1 of
    /// docs/model-danych.md): a severed child with a required key would
    /// otherwise be an orphan EF refuses to save rather than a row that goes
    /// away. The rows are settings of the competition, so dropping one from
    /// the list IS deleting it; nothing else points at them.
    /// </summary>
    /// <summary>
    /// Takes rows off a competition's list without deleting them (T-101):
    /// retention is at least 5 years, and an uploaded attachment points at
    /// the requirement it answers. Replaced the hard delete the lists had.
    /// </summary>
    private void Deactivate(IEnumerable<IRetainedRow> rows)
    {
        var now = _time.GetUtcNow();
        foreach (var row in rows.Where(row => row.IsActive).ToList())
        {
            row.IsActive = false;
            row.DeactivatedAt = now;
        }
    }

    private static void Reactivate(IRetainedRow row)
    {
        row.IsActive = true;
        row.DeactivatedAt = null;
    }

    /// <summary>
    /// The categories the 2026 template names, used when the request carries
    /// none. "No categories at all" is not a setting anybody wants, so an
    /// empty list means the defaults rather than an empty budget.
    /// </summary>
    private static readonly CostCategory[] DefaultCostCategories =
    [
        CostCategory.DirectCosts,
        CostCategory.InstitutionalDevelopment,
        CostCategory.IndirectCosts,
    ];

    private void ApplyCostCategories(
        CompetitionRequest request,
        Competition competition)
    {
        var wanted = request.CostCategories is { Count: > 0 } categories
            ? categories
            : DefaultCostCategories;

        // Matched by category among every row, active or not: the category
        // is unique per competition, so one switched off and on again is the
        // same row coming back, not a second one (T-101).
        var rows = competition.CostCategories.ToDictionary(existing => existing.Category);

        Deactivate(competition.CostCategories.Where(existing => !wanted.Contains(existing.Category)));

        for (var position = 0; position < wanted.Count; position++)
        {
            var category = wanted[position];

            if (rows.TryGetValue(category, out var existing))
            {
                Reactivate(existing);
                existing.Position = position;
                continue;
            }

            competition.CostCategories.Add(new CompetitionCostCategory
            {
                Category = category,
                Position = position,
            });
        }
    }

    /// <summary>
    /// The attachment list arrives whole, as the wizard edits it, and is
    /// matched to the stored rows by id (T-101): an uploaded file points at
    /// the requirement it answers, so a requirement that stays keeps its id,
    /// one taken off the list is marked inactive, and one without an id, or
    /// with an id this competition does not have, is new.
    /// </summary>
    private void ApplyAttachments(
        CompetitionRequest request,
        Competition competition)
    {
        var wanted = request.Attachments ?? [];
        var rows = competition.Attachments.ToDictionary(existing => existing.Id);
        var kept = wanted
            .Select(attachment => attachment.Id)
            .OfType<Guid>()
            .Where(rows.ContainsKey)
            .ToHashSet();

        Deactivate(competition.Attachments.Where(existing => !kept.Contains(existing.Id)));

        for (var position = 0; position < wanted.Count; position++)
        {
            var attachment = wanted[position];

            if (attachment.Id is { } id && rows.TryGetValue(id, out var existing))
            {
                Reactivate(existing);
                existing.Title = attachment.Title;
                existing.Description = attachment.Description;
                existing.Requirement = attachment.Requirement;
                existing.AllowedFormats = [.. attachment.AllowedFormats];
                existing.Position = position;
                continue;
            }

            competition.Attachments.Add(new CompetitionAttachment
            {
                Title = attachment.Title,
                Description = attachment.Description,
                Requirement = attachment.Requirement,
                AllowedFormats = [.. attachment.AllowedFormats],
                Position = position,
            });
        }
    }

    private void ApplyContacts(
        CompetitionRequest request,
        Competition competition,
        IReadOnlyDictionary<Guid, User> accounts)
    {
        var wanted = request.ContactUserIds ?? [];

        // By account among every row, active or not, for the reason given
        // on the cost categories: one person per competition (T-101).
        var rows = competition.Contacts.ToDictionary(existing => existing.UserId);

        Deactivate(competition.Contacts.Where(existing => !wanted.Contains(existing.UserId)));

        for (var position = 0; position < wanted.Count; position++)
        {
            var userId = wanted[position];

            if (rows.TryGetValue(userId, out var existing))
            {
                Reactivate(existing);
                existing.Position = position;
                continue;
            }

            competition.Contacts.Add(new CompetitionContact
            {
                UserId = userId,
                Position = position,

                // The account the id was checked against, so a freshly created
                // contact can be answered with a name and an address without a
                // second trip to the database. A new row has no loaded
                // navigation of its own, and reading one is a null reference,
                // not an empty contact.
                User = accounts[userId],
            });
        }
    }

    /// <summary>
    /// The accounts behind the contact ids, or null when any of them is not an
    /// active staff account.
    ///
    /// Checked here and not by a foreign key, because the key can only say the
    /// account exists: it cannot say the account is an operator, and publishing
    /// an applicant's address as the person to ask about the competition would
    /// be a leak of our own making.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, User>?> FindContactsAsync(
        IReadOnlyList<Guid>? contactUserIds,
        CancellationToken cancellationToken)
    {
        if (contactUserIds is not { Count: > 0 } wanted)
        {
            return new Dictionary<Guid, User>();
        }

        var found = await _context.Users
            .Where(user => wanted.Contains(user.Id)
                && user.Role == Role.Operator
                && user.IsActive)
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        return found.Count == wanted.Distinct().Count() ? found : null;
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
            CompetitionIntakeResponse.From(CompetitionIntake.For(competition, now)),
            competition.StartDate,
            competition.EndDate,
            competition.IsContinuousIntake,
            competition.MaxGrantAmount,
            competition.FormDefinitionId,
            competition.PublishedAt,
            competition.IsActive,
            competition.CreatedAt,
            competition.UpdatedAt,
            competition.ExpectedResults,
            competition.RulesUrl,
            competition.SubmissionNotice,
            competition.SubmissionEmailBody,
            competition.RequiresPaperSubmission,
            competition.PaperSubmissionDeadline,
            competition.PaperSubmissionAddress,
            competition.ProjectStartDate,
            competition.ProjectEndDate,
            competition.TotalPoolAmount,
            competition.MinGrantAmount,
            competition.MaxIndirectCostPercent,
            competition.MaxInstitutionalDevelopmentPercent,
            competition.PercentageBasis,
            competition.MaxAverageAnnualRevenue,
            competition.PersonalDataProcessedUntil,
            ToCostCategories(competition),
            competition.MaxAttachmentSizeInBytes,
            competition.MaxApplicationSizeInBytes,
            ToAttachments(competition),
            ToContacts(competition),
            competition.Status is CompetitionStatus.Draft ? PublicationGaps(competition) : []);
    }

    /// <summary>
    /// The child rows in the order the operator arranged them. Sorted here
    /// rather than relied on from the query, because an Include makes no
    /// promise about the order rows come back in.
    /// </summary>
    private static IReadOnlyList<CostCategory> ToCostCategories(
        Competition competition) =>
        [.. competition.CostCategories
            .Where(category => category.IsActive)
            .OrderBy(category => category.Position)
            .Select(category => category.Category)];

    private static IReadOnlyList<CompetitionAttachmentResponse> ToAttachments(
        Competition competition) =>
        [.. competition.Attachments
            .Where(attachment => attachment.IsActive)
            .OrderBy(attachment => attachment.Position)
            .Select(attachment => new CompetitionAttachmentResponse(
                attachment.Id,
                attachment.Title,
                attachment.Description,
                attachment.Requirement,
                attachment.AllowedFormats))];

    /// <summary>
    /// Name and work address of the people to ask, which is what step 1.6 puts
    /// on a page a guest can read. Nothing else off the account travels here.
    /// </summary>
    private static IReadOnlyList<CompetitionContactResponse> ToContacts(
        Competition competition) =>
        [.. competition.Contacts

            // A contact whose account has been deactivated drops out of the
            // answer rather than being published as the person to ask: that
            // account belongs to somebody who has left. It also keeps the
            // round trip honest, because a request may only carry active staff
            // accounts, so an operator editing the closing date would otherwise
            // send back a contact the save refuses and get a 409 about
            // something they never touched.
            .Where(contact => contact.IsActive && contact.User.IsActive)
            .OrderBy(contact => contact.Position)
            .Select(contact => new CompetitionContactResponse(
                contact.UserId,
                $"{contact.User.FirstName} {contact.User.LastName}".Trim(),
                contact.User.Email ?? string.Empty))];

    private static PublicCompetitionResponse ToPublicResponse(
        Competition competition,
        DateTimeOffset now) =>
        new(competition.Id,
            competition.Number,
            competition.Title,
            competition.Description,
            CompetitionLifecycle.Effective(competition, now),
            CompetitionIntakeResponse.From(CompetitionIntake.For(competition, now)),
            competition.StartDate,
            competition.EndDate,
            competition.IsContinuousIntake,
            competition.MaxGrantAmount,
            competition.ExpectedResults,
            competition.RulesUrl,
            competition.RequiresPaperSubmission,
            competition.PaperSubmissionDeadline,
            competition.PaperSubmissionAddress,
            competition.ProjectStartDate,
            competition.ProjectEndDate,
            competition.TotalPoolAmount,
            competition.MinGrantAmount,
            competition.MaxIndirectCostPercent,
            competition.MaxInstitutionalDevelopmentPercent,
            competition.PercentageBasis,
            competition.MaxAverageAnnualRevenue,
            competition.PersonalDataProcessedUntil,
            ToCostCategories(competition),
            competition.MaxAttachmentSizeInBytes,
            competition.MaxApplicationSizeInBytes,
            ToAttachments(competition),
            ToContacts(competition));
}
