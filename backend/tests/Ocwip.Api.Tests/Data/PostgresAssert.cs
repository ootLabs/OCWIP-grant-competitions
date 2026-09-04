using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// Unwraps the PostgreSQL error EF hides inside a DbUpdateException, so a test
/// can assert on the SQL state and the constraint name rather than on a message.
/// </summary>
internal static class PostgresAssert
{
    public const string CheckViolation = "23514";
    public const string UniqueViolation = "23505";
    public const string ForeignKeyViolation = "23503";

    /// <summary>
    /// What RAISE EXCEPTION in a PL/pgSQL block reports when it is given no
    /// SQLSTATE of its own. The guard at the top of the T-12.0 migration is
    /// the one place this project raises its own error from SQL.
    /// </summary>
    public const string RaisedException = "P0001";

    public static PostgresException Error(DbUpdateException exception) =>
        Assert.IsType<PostgresException>(exception.InnerException);
}
