using Microsoft.AspNetCore.Identity;
using Ocwip.Api.Admin;
using Ocwip.Api.Configuration;
using Ocwip.Api.Contracts;
using Ocwip.Api.Data;
using Ocwip.Api.Endpoints;
using Ocwip.Api.Models;
using Ocwip.Api.Services;

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

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

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
    builder.Services.AddDataProtection();

    // AddIdentityCore, not AddIdentity: no role store. Roles are a column here
    // (Models/Role.cs), and AddIdentity would bring AspNetRoles back through
    // the front door. The cookie handler is added separately below, which is
    // the other half of what AddIdentity would have done.
    builder.Services
        .AddIdentityCore<User>()
        .AddErrorDescriber<CustomPasswordErrorConfiguration>()
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

    // Backs EmailVerificationService's resend cooldown. In-process only (see
    // that class), which is fine for a single API instance.
    builder.Services.AddMemoryCache();
    builder.Services.AddScoped<IEmailSender, EmailSenderService>();
    builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
    builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
}

// The clock, as a service. CompetitionService derives the state of a
// competition from it (T-20), and the cases worth testing are a minute either
// side of the closing minute, which a test cannot reach through the real one.
// Outside the connection string check because it is not a database concern.
builder.Services.AddSingleton(TimeProvider.System);

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
app.MapPasswordResetEndpoints();

// The fallback policy from T-13.2 applies to requests that match no endpoint
// at all, so without this a mistyped or removed path answers 401 "zaloguj
// sie" instead of 404. That is wrong twice over: it tells an anonymous caller
// that a path they invented might exist behind a login, and once the panels
// add the usual "401 means the session died, go to the login page" handling,
// a single routing typo would sign a working user out. A terminal route that
// matches everything left over puts the honest answer back.
app.MapFallback(() => Results.Problem(
        "Nie ma takiego zasobu.",
        statusCode: StatusCodes.Status404NotFound))
    .AllowAnonymous();

app.Run();

return AdminCommandRunner.Success;

// Exposed so the test host can boot the real application instead of a copy of it.
public partial class Program;
