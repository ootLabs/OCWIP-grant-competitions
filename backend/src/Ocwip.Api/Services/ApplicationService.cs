using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Models;

namespace Ocwip.Api.Services;

/// <summary>
/// Draft applications (T-29): starting one, autosaving it, reading it and
/// marking it inactive.
///
/// Every write here goes through T-21's rule before touching the row: whether
/// a draft may be created or saved is CompetitionIntake's question to answer,
/// never a comparison of dates written again in this file.
/// </summary>
internal sealed class ApplicationService : IApplicationService
{
    /// <summary>An answer set nobody has touched yet. Object, not array: the
    /// check constraint accepts either, but the form contract's own root is an
    /// object (docs/kontrakt-formularza.md), and starting from the other shape
    /// would be a lie about what is coming.</summary>
    private static readonly JsonElement EmptyAnswers =
        JsonDocument.Parse("{}").RootElement.Clone();

    private readonly AppDbContext _context;
    private readonly TimeProvider _time;
    private readonly UserManager<User> _userManager;

    public ApplicationService(
        AppDbContext context,
        TimeProvider time,
        UserManager<User> userManager)
    {
        _context = context;
        _time = time;
        _userManager = userManager;
    }

    public async Task<ApplicationResult> CreateDraftAsync(
        Guid competitionId,
        ClaimsPrincipal caller,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(caller);

        if (user?.EntityId is not { } entityId)
        {
            return new ApplicationResult(ApplicationOutcome.NoEntity);
        }

        var competition = await _context.Competitions
            .FirstOrDefaultAsync(x => x.Id == competitionId, cancellationToken);

        if (competition is null)
        {
            return new ApplicationResult(ApplicationOutcome.CompetitionNotFound);
        }

        var intake = CompetitionIntake.For(competition, _time.GetUtcNow());

        // Covers an inactive competition and a draft competition too, not
        // only a closed one: CompetitionIntake already treats both as
        // Unavailable, and repeating that comparison here would be a second
        // rule pretending to be T-21's.
        if (!intake.AcceptsApplications)
        {
            return new ApplicationResult(
                ApplicationOutcome.IntakeClosed,
                Message: CompetitionIntakeMessage.For(intake));
        }

        if (competition.FormDefinitionId is not { } formDefinitionId)
        {
            return new ApplicationResult(ApplicationOutcome.NoFormDefinition);
        }

        var application = new Application
        {
            CompetitionId = competitionId,
            EntityId = entityId,
            FormDefinitionId = formDefinitionId,
            Status = ApplicationStatus.Draft,
            Answers = EmptyAnswers,
        };

        _context.Applications.Add(application);
        await _context.SaveChangesAsync(cancellationToken);

        // Reloaded so the checksum below hashes exactly what a later read
        // will: timestamptz round-trips through PostgreSQL at microsecond
        // precision, one digit short of a .NET tick, so the in-memory value
        // this method just wrote and the value a GET reads back can disagree
        // in their last digit. See ApplicationChecksumTests for the jsonb
        // half of the same problem.
        await _context.Entry(application).ReloadAsync(cancellationToken);

        return Success(application);
    }

    public async Task<ApplicationResult> SaveDraftAsync(
        Guid id,
        JsonElement answers,
        CancellationToken cancellationToken)
    {
        // Same shape rule as the check constraint (ApplicationConfiguration),
        // checked here so a missing or malformed body answers 400 naming the
        // problem instead of failing inside the Npgsql serializer with a
        // message that names no field at all.
        if (answers.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
        {
            return new ApplicationResult(ApplicationOutcome.InvalidAnswers);
        }

        var application = await _context.Applications
            .Include(x => x.Competition)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (application is null)
        {
            return new ApplicationResult(ApplicationOutcome.NotFound);
        }

        if (application.Status is not ApplicationStatus.Draft)
        {
            return new ApplicationResult(ApplicationOutcome.AlreadySubmitted);
        }

        var intake = CompetitionIntake.For(application.Competition, _time.GetUtcNow());

        if (!intake.AcceptsApplications)
        {
            return new ApplicationResult(
                ApplicationOutcome.IntakeClosed,
                Message: CompetitionIntakeMessage.For(intake));
        }

        application.Answers = answers.Clone();
        await _context.SaveChangesAsync(cancellationToken);

        // See the same call in CreateDraftAsync: without it, this save's own
        // response checksums a slightly more precise UpdatedAt than any later
        // GET will, and the two never agree again.
        await _context.Entry(application).ReloadAsync(cancellationToken);

        return Success(application);
    }

    public async Task<ApplicationResult> GetAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var application = await _context.Applications
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return application is null
            ? new ApplicationResult(ApplicationOutcome.NotFound)
            : Success(application);
    }

    public async Task<ApplicationResult> DeactivateAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var application = await _context.Applications
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (application is null)
        {
            return new ApplicationResult(ApplicationOutcome.NotFound);
        }

        if (application.Status is not ApplicationStatus.Draft)
        {
            return new ApplicationResult(ApplicationOutcome.AlreadySubmitted);
        }

        // Idempotent, same reasoning as CompetitionService.DeactivateAsync: a
        // second call asks for the state the row is already in, and answering
        // with a failure there only teaches the caller to treat a request
        // that already succeeded once as broken.
        if (application.IsActive)
        {
            application.IsActive = false;
            application.DeactivatedAt = _time.GetUtcNow();

            await _context.SaveChangesAsync(cancellationToken);

            // See the same call in CreateDraftAsync: marking the row
            // modified re-stamps UpdatedAt too, at the database's coarser
            // precision, and the checksum below has to agree with what a
            // later GET recomputes.
            await _context.Entry(application).ReloadAsync(cancellationToken);
        }

        return Success(application);
    }

    public Task<Application?> FindForAuthorizationAsync(
        Guid id, CancellationToken cancellationToken) =>
        _context.Applications
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    private ApplicationResult Success(Application application) =>
        new(ApplicationOutcome.Succeeded, ToResponse(application));

    private static ApplicationResponse ToResponse(Application application) =>
        new(
            application.Id,
            application.CompetitionId,
            application.FormDefinitionId,
            application.Status,
            application.Answers,
            application.Number,
            application.SubmittedAt,
            application.UpdatedAt,
            ApplicationChecksum.Compute(
                application.Id, application.UpdatedAt, application.Answers),
            application.IsActive);
}
