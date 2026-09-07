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

    // AddIdentityCore, not AddIdentity: no cookie handler and no role store.
    // Roles are a column here (Models/Role.cs), and the sign in handler
    // belongs to T-12.3.
    builder.Services
        .AddIdentityCore<User>()
        .AddErrorDescriber<CustomPasswordErrorConfiguration>()
        .AddEntityFrameworkStores<AppDbContext>()
        // Without this, GenerateEmailConfirmationTokenAsync/ConfirmEmailAsync
        // throw at runtime: they resolve their token provider by name, and
        // that name is only registered by this call.
        .AddDefaultTokenProviders();

    builder.Services.AddIdentityConfiguration(builder.Configuration);

    // Same condition again: registration needs UserManager, which needs
    // the store above. The endpoint is mapped unconditionally and asks for
    // this service explicitly, see Endpoints/AccountEndpoints.cs.
    builder.Services.AddScoped<IAccountService, AccountService>();

    // Backs EmailVerificationService's resend cooldown. In-process only (see
    // that class), which is fine for a single API instance.
    builder.Services.AddMemoryCache();
    builder.Services.AddScoped<IEmailSender, EmailSenderService>();
    builder.Services.AddScoped<IEmailVerificationService, EmailVerificationService>();
}

// Origins come from configuration so a new deployment never needs a rebuild.
var corsOrigins = (builder.Configuration["Cors:Origins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        // Session will be carried by a cookie, which the browser only sends
        // cross origin when credentials are allowed. See docs/architektura.md.
        .AllowCredentials()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Deliberately not the same condition as the registration above: the API has to
// be able to start against a database it is not allowed to migrate.
app.ApplyPendingMigrations();

app.UseCors();
app.MapEmailVerificationEndpoints();
app.MapHealthEndpoints();
app.MapAccountEndpoints();

app.Run();

return AdminCommandRunner.Success;

// Exposed so the test host can boot the real application instead of a copy of it.
public partial class Program;
