using Ocwip.Api.Admin;
using Ocwip.Api.Configuration;
using Ocwip.Api.Data;
using Ocwip.Api.Endpoints;
using Ocwip.Api.Models;

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

    // Inside the same condition as the DbContext, because Identity's EF store
    // resolves AppDbContext in order to build itself. Without a database the
    // container still has to build cleanly: /health and /health/db answer on a
    // host that has no database at all, see HealthEndpointsTests.
    //
    // AddIdentityCore, not AddIdentity: no cookie handler and no role store.
    // Roles are a column here (Models/Role.cs), and the sign in handler belongs
    // to T-12.3. Token providers for the verification and reset links are
    // T-12.2 and T-12.4, so AddDefaultTokenProviders is deliberately absent.
    builder.Services
        .AddIdentityCore<User>()
        .AddErrorDescriber<CustomPasswordErrorConfiguration>()
        .AddEntityFrameworkStores<AppDbContext>();

    builder.Services.AddIdentityConfiguration();
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
app.MapHealthEndpoints();

app.Run();

return AdminCommandRunner.Success;

// Exposed so the test host can boot the real application instead of a copy of it.
public partial class Program;
