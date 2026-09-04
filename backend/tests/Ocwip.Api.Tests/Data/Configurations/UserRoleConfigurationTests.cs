using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Ocwip.Api.Models;
using Xunit;

namespace Ocwip.Api.Tests.Data.Configurations;

/// <summary>
/// The role column (T-13.1). Every authorization rule this project will ever
/// write starts by reading it, so its shape is tested rather than assumed.
///
/// Stored as text is asserted in AccountConfigurationTests together with the
/// rest of the account metadata; this class owns what is specific to the
/// privilege field: the default, the allowed values, and the uniqueness that
/// deliberately is not there.
/// </summary>
public sealed class UserRoleConfigurationTests
{
    private const string ConstraintName = "ck_users_role_is_known";

    private static IProperty RoleProperty =>
        TestModel.EntityType<User>().FindProperty(nameof(User.Role))
        ?? throw new InvalidOperationException("User.Role is not mapped.");

    [Fact]
    public void Roles_ShouldBeExactlyTheThreeFromTheBusinessRules()
    {
        // Act
        var names = Enum.GetNames<Role>().Order().ToArray();

        // Assert
        // docs/reguly-biznesowe.md: operator, applicant, reviewer, three roles
        // seeing three different systems. A fourth one is a conversation with
        // the client, not a commit.
        Assert.Equal(new[] { "Applicant", "Operator", "Reviewer" }, names);
    }

    [Fact]
    public void LeastPrivilegedRole_ShouldBeTheClrDefault()
    {
        // Assert
        // Applicant sits first in the enum on purpose, so code that forgets to
        // set a role produces the least privileged account rather than the most
        // privileged one. Reordering the enum would silently reverse that, and
        // the database would not notice because the column holds text.
        Assert.Equal(Role.Applicant, default(Role));
    }

    [Fact]
    public void NewAccount_ShouldStartAsApplicant()
    {
        // Act
        var user = new User();

        // Assert
        // The acceptance criterion of T-13.1: the role registration produces.
        // Registration does not exist yet (T-12.1), so this is the guarantee it
        // inherits rather than has to remember.
        Assert.Equal(Role.Applicant, user.Role);
    }

    [Fact]
    public void Role_ShouldDefaultToApplicantInTheSchemaToo()
    {
        // Act
        var defaultValue = RoleProperty.GetDefaultValue();

        // Assert
        // Not a duplicate of the test above: an account can also arrive through
        // a statement that never reaches the change tracker, which is exactly
        // how an operator is created, and such an insert omitting the column has
        // to land on the least privileged role instead of failing.
        //
        // Read through both representations, because whether EF hands back the
        // enum or the converted text is its business, not this test's subject.
        var stored = defaultValue switch
        {
            Role value => value.ToString(),
            string value => value,
            _ => null,
        };

        Assert.Equal(nameof(Role.Applicant), stored);
    }

    [Fact]
    public void Role_ShouldBeConstrainedToExactlyTheKnownRoles()
    {
        // Act
        var constraint = TestModel.EntityType<User>()
            .GetCheckConstraints()
            .SingleOrDefault(x => x.Name == ConstraintName);

        // Assert
        // The one text enum in this schema that is constrained to its values,
        // because it is the one granted by a statement somebody types. Without
        // it, role = 'operator' in lower case is accepted and leaves an account
        // in no role at all.
        Assert.NotNull(constraint);

        foreach (var name in Enum.GetNames<Role>())
        {
            Assert.Contains($"'{name}'", constraint.Sql);
        }

        // And nothing beyond them: a role deleted from the enum must not survive
        // in the SQL, where it would keep letting rows in that no longer map to
        // anything. Two quotes per literal.
        Assert.Equal(
            Enum.GetNames<Role>().Length,
            constraint.Sql.Count(x => x == '\'') / 2);
    }

    [Fact]
    public void Role_ShouldFitEveryRoleName()
    {
        // Act
        var maxLength = RoleProperty.GetMaxLength();

        // Assert
        Assert.Equal(20, maxLength);
        Assert.True(maxLength > Enum.GetNames<Role>().Max(x => x.Length));
    }

    [Fact]
    public void Role_ShouldCarryNoUniqueness()
    {
        // Arrange
        var entityType = TestModel.EntityType<User>();

        // Act
        var uniqueIndexes = entityType.GetIndexes()
            .Where(x =>
                x.IsUnique && x.Properties.Any(p => p.Name == nameof(User.Role)))
            .ToList();

        var keys = entityType.GetKeys()
            .Where(x => x.Properties.Any(p => p.Name == nameof(User.Role)))
            .ToList();

        // Assert
        // A test of ABSENCE, like the one for (entity_id, competition_id) on the
        // application. OCWIP runs its competitions with more than one person, so
        // several accounts hold the operator role at the same time. A comment
        // saying so would not survive the first reader who spots an "obviously
        // missing" constraint on a column with three distinct values.
        Assert.Empty(uniqueIndexes);
        Assert.Empty(keys);
    }
}
