using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ocwip.Api.Tests.Data;

/// <summary>
/// Retention of at least 5 years (AGENTS.md, T-47a): nothing removes a row
/// because another one went. Checked over the whole model and over the real
/// schema, not relation by relation, so a table added later is covered
/// without anyone remembering to add a test for it.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RetentionModelTests(PostgresDatabaseFixture database)
{
    [Fact]
    public void No_relation_in_the_model_deletes_or_clears_its_dependants()
    {
        var keys = TestModel.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()).ToList();
        Assert.NotEmpty(keys);

        var removing = keys
            .Where(x => x.DeleteBehavior is DeleteBehavior.Cascade or DeleteBehavior.SetNull or DeleteBehavior.ClientCascade)
            .Select(x => $"{x.DeclaringEntityType.GetTableName()} -> {x.PrincipalEntityType.GetTableName()}: {x.DeleteBehavior}")
            .ToList();

        Assert.Empty(removing);
    }

    [RequiresDatabaseFact]
    public async Task No_foreign_key_in_the_database_cascades_or_clears()
    {
        await using var context = database.CreateContext();

        var removing = await context.Database.SqlQueryRaw<string>(
                """
                SELECT tc.table_name || '.' || tc.constraint_name || ': ' || rc.delete_rule AS "Value"
                FROM information_schema.referential_constraints rc
                JOIN information_schema.table_constraints tc
                  ON tc.constraint_name = rc.constraint_name AND tc.constraint_schema = rc.constraint_schema
                WHERE rc.constraint_schema = 'public' AND rc.delete_rule IN ('CASCADE', 'SET NULL', 'SET DEFAULT')
                """)
            .ToListAsync();

        Assert.Empty(removing);
    }
}
