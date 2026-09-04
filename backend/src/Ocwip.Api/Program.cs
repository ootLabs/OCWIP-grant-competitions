using Ocwip.Api.Admin;
using Ocwip.Api.Data;
using Ocwip.Api.Endpoints;

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
