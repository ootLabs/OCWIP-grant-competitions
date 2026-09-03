
using Ocwip.Api.Configuration;
using Ocwip.Api.Data;
using Ocwip.Api.Endpoints;
using Ocwip.Api.Models;
using Ocwip.Api.Services;

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
    builder.Services.AddScoped<IAccountService, AccountService>();
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
app.MapRegisterEndpoints();
app.MapHealthEndpoints();

app.Run();

// Exposed so the test host can boot the real application instead of a copy of it.
public partial class Program;
