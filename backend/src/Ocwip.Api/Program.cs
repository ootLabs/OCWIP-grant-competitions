using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Ocwip.Api.Admin;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Data.Encryption;
using Ocwip.Api.Endpoints;
using Ocwip.Api.Models;
using Ocwip.Api.Services;
using Ocwip.Api.Services.Ranking;

// The operator role is never granted over HTTP (docs/architektura.md), so the
// command that grants it is handled here, before a web host exists. A single
// UPDATE has no business opening a listening socket or running startup
// migrations on its way to the database.
//
// Every verb comes through here, not just the one that is spelled correctly:
// a mistyped grant-role falling through to CreateBuilder would boot a second
// api process inside the container that already runs one, take the exclusive
// lock on the migrations history and apply migrations. See IsAdminInvocation.
if (AdminCommandLine.IsAdminInvocation(args))
{
    return await AdminCommandRunner.RunAsync(
        args,
        AppDbContextFactory.BuildConfiguration(),
        Console.Out);
}

var builder = WebApplication.CreateBuilder(args);

// T-91: in Production a localhost link, a missing relay or an open Host header
// stops the start here, before any of them can fail quietly later.
ProductionConfiguration.EnsureValid(builder.Configuration, builder.Environment);

// T-47a: the key the sensitive columns are encrypted with. Outside
// Production a missing key only fails the first sensitive write.
FieldEncryption.Configure(builder.Configuration);

builder.Services.AddOpenApi();

// T-116: JSON lines with the request id outside Development.
builder.AddOcwipLogging();

// T-107: the terms and the privacy notice, read from seed/consents.
builder.Services.AddSingleton<Ocwip.Api.Services.Consents.ConsentCatalog>();
builder.Services.AddProblemDetails();

// T-123: a body the framework cannot read answers 400, not 500.
builder.Services.AddExceptionHandler<UnreadableRequestHandler>();

// T-111: /health/db asks one shared data source, not a new pool per probe.
builder.Services.AddSingleton<DatabaseProbe>();

// T-111: the whole request body, attachments included, stays under the
// proxy's limit (deploy/caddy/Caddyfile) and above the largest attachment a
// competition may allow (CompetitionRequestValidator.MaxAttachmentSizeCeiling).
builder.WebHost.ConfigureKestrel(kestrel =>
    kestrel.Limits.MaxRequestBodySize = Ocwip.Api.Contracts.CompetitionRequestValidator.MaxRequestBodySize);

// Provider and naming convention come from Data/PostgresDbContextOptions.cs,
// which is also what dotnet ef and the tests use.
var connectionString = builder.Configuration.GetConnectionString("Postgres");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseOcwipPostgres(connectionString));

    // Identity needs AppDbContext to build its EF store, so it can only be
    // wired up when there is a database to wire it to. Without one, the DI
    // container still has to build cleanly: /health and /health/db must come
    // up without a database (see HealthEndpointsTests).
    //
    // AddDefaultTokenProviders (below) registers DataProtectorTokenProvider,
    // which needs IDataProtectionProvider - ASP.NET Core does not add Data
    // Protection to the container on its own, so without this the container
    // fails to build the moment anything resolves the token provider.
    // T-113: one application name, so every instance and every new
    // container reads the same key ring, and the keys on a volume when a
    // path is configured. Without that each new container signed everybody
    // out and voided every link in a verification or reset mail.
    // Production refuses to start without the path (ProductionConfiguration).
    var protection = builder.Services.AddDataProtection().SetApplicationName("ocwip");
    if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
    {
        protection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
    }

    // AddIdentityCore, not AddIdentity: no role store. Roles are a column here
    // (Models/Role.cs), and AddIdentity would bring AspNetRoles back through
    // the front door. The cookie handler is added separately below, which is
    // the other half of what AddIdentity would have done.
    builder.Services
        .AddIdentityCore<User>()
        .AddErrorDescriber<CustomPasswordErrorConfiguration>()
        // The upper bound Identity has no option for (S-17): every path that
        // sets a password runs through this one validator.
        .AddPasswordValidator<PasswordLengthValidator>()
        .AddEntityFrameworkStores<AppDbContext>()
        // Without this, GenerateEmailConfirmationTokenAsync/ConfirmEmailAsync
        // throw at runtime: they resolve their token provider by name, and
        // that name is only registered by this call.
        .AddDefaultTokenProviders()
        // T-12.3. SignInManager verifies passwords and writes the cookie; the
        // claims factory copies the role column into the principal, because
        // there is no role table for Identity's own factory to read.
        .AddClaimsPrincipalFactory<RoleClaimsPrincipalFactory>()
        .AddSignInManager()
        // T-12.4: a second DataProtectorTokenProvider under its own name, so
        // password reset tokens get their own lifetime instead of sharing the
        // "Default" provider's with email confirmation. IdentityConfiguration
        // points Options.Tokens.PasswordResetTokenProvider at this name.
        .AddTokenProvider<PasswordResetTokenProvider<User>>(
            IdentityConfiguration.PasswordResetTokenProviderName);

    builder.Services.AddIdentityConfiguration(builder.Configuration);

    // Same condition again: registration needs UserManager, which needs
    // the store above. The endpoint is mapped unconditionally and asks for
    // this service explicitly, see Endpoints/AccountEndpoints.cs.
    builder.Services.AddScoped<IAccountService, AccountService>();
    builder.Services.AddScoped<ISessionService, SessionService>();
    builder.Services.AddScoped<ICompetitionService, CompetitionService>();
    builder.Services.AddScoped<ICompetitionCopyService, CompetitionCopyService>();
    builder.Services.AddScoped<IFormDefinitionService, FormDefinitionService>();
    builder.Services.AddScoped<IApplicationService, ApplicationService>();
    builder.Services.AddScoped<IApplicationSubmissionService, ApplicationSubmissionService>();
    builder.Services.AddScoped<IApplicationReturnService, ApplicationReturnService>();

    // T-105: one scheduler, jobs registered as IBackgroundJob; off with
    // BackgroundJobs:Enabled=false (the tests set it).
    builder.Services.AddScoped<Ocwip.Api.Services.Jobs.IBackgroundJob, Ocwip.Api.Services.Jobs.IntakeReminderJob>();
    builder.Services.AddScoped<Ocwip.Api.Services.Jobs.IBackgroundJob, Ocwip.Api.Services.Jobs.ContractDeadlineJob>();
    builder.Services.AddScoped<Ocwip.Api.Services.Jobs.IBackgroundJob, Ocwip.Api.Services.Jobs.EntityAccessEscalationJob>();
    builder.Services.AddHostedService<Ocwip.Api.Services.Jobs.BackgroundJobScheduler>();
    builder.Services.AddScoped<IApplicationListService, ApplicationListService>();
    builder.Services.AddScoped<IApplicationOverviewService, ApplicationOverviewService>();
    builder.Services.AddScoped<IApplicationAssignmentService, ApplicationAssignmentService>();
    builder.Services.AddScoped<IEvaluationService, EvaluationService>();
    builder.Services.AddScoped<IRankingService, RankingService>();
    builder.Services.AddScoped<IReviewerWorkService, ReviewerWorkService>();
    builder.Services.AddScoped<IDeclarationService, DeclarationService>();
    builder.Services.AddScoped<IReviewerDirectory, ReviewerDirectory>();
    builder.Services.AddScoped<IApplicationEvaluationList, ApplicationEvaluationList>();
    builder.Services.AddScoped<ICardSharingService, CardSharingService>();
    builder.Services.AddScoped<IGrantDecisionService, GrantDecisionService>();
    builder.Services.AddScoped<IResignationService, ResignationService>();
    builder.Services.AddScoped<IRankingPublication, RankingPublication>();
    builder.Services.AddScoped<IResultNotificationService, ResultNotificationService>();
    builder.Services.AddScoped<Ocwip.Api.Services.Reports.IReportService, Ocwip.Api.Services.Reports.ReportService>();
    builder.Services.AddScoped<Ocwip.Api.Services.Documents.IContractService, Ocwip.Api.Services.Documents.ContractService>();
    builder.Services.AddScoped<Ocwip.Api.Services.Documents.IContractBundleService, Ocwip.Api.Services.Documents.ContractBundleService>();
    builder.Services.AddScoped<IAttachmentService, AttachmentService>();
    builder.Services.AddScoped<IAttachmentTemplateService, AttachmentTemplateService>();
    builder.Services.AddScoped<IOperatorDirectoryService, OperatorDirectoryService>();
    builder.Services.AddScoped<Ocwip.Api.Services.EntityCards.IEntityCardService, Ocwip.Api.Services.EntityCards.EntityCardService>();
    builder.Services.AddScoped<Ocwip.Api.Services.EntityCards.IEntityAccessRequestService, Ocwip.Api.Services.EntityCards.EntityAccessRequestService>();

    // Backs EmailVerificationService's resend cooldown. In-process only (see
    // that class), which is fine for a single API instance.
    builder.Services.AddMemoryCache();
    // A relay when one is configured (T-43a), the logging stand in otherwise.
    // Decided once at start: a mail that silently goes to the log in
    // production would look sent to everybody but its recipient.
    var smtp = builder.Configuration.GetSection(SmtpOptions.Section);
    builder.Services.Configure<SmtpOptions>(smtp);
    if (smtp.Get<SmtpOptions>()?.IsConfigured == true)
    {
        var relay = smtp.Get<SmtpOptions>()!;
        if (string.IsNullOrWhiteSpace(relay.From))
        {
            throw new InvalidOperationException("SMTP__HOST is set, so SMTP__FROM has to be set too.");
        }

        // System.Net.Mail speaks STARTTLS only. On 465 the relay expects TLS
        // from the first byte, and every mail would hang until it timed out.
        if (relay.Port == 465)
        {
            throw new InvalidOperationException(
                "SMTP__PORT 465 (implicit TLS) is not supported; use 587 with STARTTLS.");
        }

        builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
    }
    else
    {
        builder.Services.AddScoped<IEmailSender, EmailSenderService>();
    }
    // The verification and reset mails of the public endpoints go through a
    // queue, so a request takes as long for an unknown address as for a known one.
    builder.Services.AddAccountMailQueue();
    builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
    builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
    builder.Services.AddScoped<IAccountSettingsService, AccountSettingsService>();
}

// The clock, as a service. CompetitionService derives the state of a
// competition from it (T-20), and the cases worth testing are a minute either
// side of the closing minute, which a test cannot reach through the real one.
// Outside the connection string check because it is not a database concern.
builder.Services.AddSingleton(TimeProvider.System);

// Local disk, not a database concern either (T-32): a host with no
// connection string still has to serve /health, and registering this
// unconditionally is what keeps that host's container from failing to build.
builder.Services.AddSingleton<IAttachmentStorage, AttachmentStorageService>();

// Outside the block above on purpose. The cookie handler needs no database, and
// registering it unconditionally is what lets /me answer 401 on a host without
// one instead of failing to build the endpoint. Validating the security stamp
// does need the store, which is why the flag is passed in.
builder.Services.AddOcwipAuthentication(
    builder.Configuration,
    builder.Environment.IsDevelopment(),
    hasStore: !string.IsNullOrWhiteSpace(connectionString));

// Every access rule lives in one file, including the fallback that refuses an
// endpoint nobody wrote a rule for (T-13.2). Outside the connection string
// check for the same reason as the cookie handler: the policies themselves
// need no database, and an application that cannot build its authorization
// is an application whose every route stops routing. The resource handler is
// the part that needs the store, hence the flag.
builder.Services.AddOcwipAuthorization(
    hasStore: !string.IsNullOrWhiteSpace(connectionString));

// Origins come from configuration so a new deployment never needs a rebuild.
var corsOrigins = (builder.Configuration["Cors:Origins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        // The session travels in a cookie (T-12.3), which the browser only
        // sends cross origin when credentials are allowed, and which it only
        // stores from a response that allows them. See docs/architektura.md.
        .AllowCredentials()));

// T-12.5, the IP half of brute force protection. No database needed, so this
// stays outside the block above and applies even on a host with none.
builder.Services.AddOcwipRateLimiting(builder.Configuration);

var app = builder.Build();

// T-111: the client's address from the trusted proxy, before anything reads
// it (the login limiter first of all). Nothing configured, nothing believed.
if (ForwardedHeadersConfiguration.Options(app.Configuration) is { } forwarded)
{
    app.UseForwardedHeaders(forwarded);
}

// T-112: nosniff, no referrer, no framing, on every answer.
app.UseSecurityHeaders();

// T-116: the id of the request's log lines, back to the caller.
app.UseRequestIdHeader();

// T-111: an unhandled exception and an empty error answer both come back as
// ProblemDetails (AddProblemDetails above), in every environment, instead of
// an empty 500 or a stack trace.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    // AllowAnonymous, because the fallback policy from T-13.2 applies to this
    // route too: the document is how the frontend generates its types
    // (npm run api:generate, which runs with no session), so the default deny
    // would break the build of the other half of the product. What keeps the
    // document from being a public map of an API over personal data is the
    // condition around it, decided in T-17: it exists in Development only.
    app.MapOpenApi().AllowAnonymous();
}

// Deliberately not the same condition as the registration above: the API has to
// be able to start against a database it is not allowed to migrate.
app.ApplyPendingMigrations();

app.UseCors();

// After CORS, before authentication (S-15): a request that says it comes from
// another site is refused before it reaches a handler, and the answer carries
// the headers UseCors has just negotiated.
app.UseCrossSiteRequestFilter(builder.Configuration);

// Order matters and is not ours to choose: CORS first, so a preflight is
// answered before anything asks who is calling, then authentication, then
// authorization, which needs the result of the former. The limiter sits
// between CORS and authentication: a request over its limit should never
// reach a password check or a mail send, whether or not it is authenticated.
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapEmailVerificationEndpoints();
app.MapHealthEndpoints();
app.MapAccountEndpoints();
app.MapSessionEndpoints();
app.MapCompetitionEndpoints();
app.MapCompetitionCopyEndpoints();
app.MapFormDefinitionEndpoints();
app.MapEvaluationCardEndpoints();
app.MapApplicationEndpoints();
app.MapApplicationSubmissionEndpoints();
app.MapApplicationReturnEndpoints();
app.MapApplicationAssignmentEndpoints();
app.MapEvaluationEndpoints();
app.MapRankingEndpoints();
app.MapReviewerWorkEndpoints();
app.MapDeclarationEndpoints();
app.MapReviewerDirectoryEndpoints();
app.MapApplicationEvaluationEndpoints();
app.MapCardSharingEndpoints();
app.MapGrantDecisionEndpoints();
app.MapResignationEndpoints();
app.MapRankingPublicationEndpoints();
app.MapResultNotificationEndpoints();
app.MapReportFormEndpoints();
app.MapReportEndpoints();
app.MapContractEndpoints();
app.MapApplicationListEndpoints();
app.MapApplicationOverviewEndpoints();
app.MapEntityCardEndpoints();
app.MapEntityAccessRequestEndpoints();
app.MapAttachmentEndpoints();
app.MapAttachmentTemplateEndpoints();
app.MapPasswordResetEndpoints();
app.MapAccountSettingsEndpoints();

// The fallback policy from T-13.2 applies to requests that match no endpoint
// at all, so without this a mistyped or removed path answers 401 "zaloguj
// sie" instead of 404. That is wrong twice over: it tells an anonymous caller
// that a path they invented might exist behind a login, and once the panels
// add the usual "401 means the session died, go to the login page" handling,
// a single routing typo would sign a working user out. A terminal route that
// matches everything left over puts the honest answer back.
//
// "{*path}" on purpose (T-111): the parameterless MapFallback leaves out any
// path that looks like a file, so /openapi/v1.json outside Development, or
// any other invented "x.json", answered 401 again.
app.MapFallback("{*path}", () => Results.Problem(
        "Nie ma takiego zasobu.",
        statusCode: StatusCodes.Status404NotFound))
    .AllowAnonymous();

app.Run();

return AdminCommandRunner.Success;

// Exposed so the test host can boot the real application instead of a copy of it.
public partial class Program;
