using Xunit;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// Puts every test that talks to PostgreSQL into one xUnit collection.
///
/// Two reasons, both of which appeared once the database tests were split across
/// several classes. xUnit v2 runs collections in parallel and treats every class
/// without an explicit collection as its own, so each class was issuing its own
/// CREATE DATABASE at the same moment. Concurrent CREATE DATABASE copies
/// template1, which intermittently fails with 55006, "source database template1
/// is being accessed by other users", and the class that loses the race fails
/// for a reason that has nothing to do with what it asserts. It also ran the
/// whole migration chain once per class.
///
/// As an ICollectionFixture the database is created and migrated once for all of
/// them. Tests inside one collection run sequentially, so a shared database is
/// safe, but assertions must still scope themselves to the rows they inserted
/// rather than counting everything in a table.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresDatabaseFixture>
{
    public const string Name = "postgres";
}
