using Microsoft.EntityFrameworkCore;

namespace Ocwip.Api.Data;

/// <summary>
/// Here will be database context. This class is created now only becouse migrations and the EF tooling
/// must have a home before the first real table lands.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
