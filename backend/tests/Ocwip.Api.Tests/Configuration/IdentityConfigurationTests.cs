using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ocwip.Api.Configuration;
using Ocwip.Api.Data;
using Ocwip.Api.Models;
using Ocwip.Api.Tests.Data;
using Xunit;

namespace Ocwip.Api.Tests.Configuration;

/// <summary>
/// The Identity options this card owns, read back the way the application reads
/// them. Registration (T-12.1) enforces every one of them on its first write, so
/// a value changed by accident here surfaces as a rejected account rather than as
/// a failing test somewhere near the change.
/// </summary>
[Collection(PostgresCollection.Name)]
public class IdentityConfigurationTests
{
    private readonly PostgresDatabaseFixture _database;

    public IdentityConfigurationTests(PostgresDatabaseFixture database)
    {
        _database = database;
    }

    private static IdentityOptions Options()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddIdentityConfiguration();

        return services
            .BuildServiceProvider()
            .GetRequiredService<IOptions<IdentityOptions>>()
            .Value;
    }

    [Fact]
    public void The_password_policy_is_eight_characters_and_four_classes()
    {
        var password = Options().Password;

        Assert.Equal(8, password.RequiredLength);
        Assert.True(password.RequireDigit);
        Assert.True(password.RequireUppercase);
        Assert.True(password.RequireLowercase);
        Assert.True(password.RequireNonAlphanumeric);
    }

    [Fact]
    public void One_address_is_one_account()
    {
        Assert.True(Options().User.RequireUniqueEmail);
    }

    [Fact]
    public void The_username_character_filter_is_off()
    {
        // Empty, and this is the assertion that says so deliberately. UserName
        // mirrors the address (UserConfiguration.cs), so Identity's filter, which
        // it means for usernames, would decide which ADDRESSES may register: its
        // default allows only a-zA-Z0-9-._@+, so the address below is refused
        // with an English message about letters and digits.
        //
        // Empty is what switches the check off in UserValidator. A non empty
        // value here is not a stricter policy, it is a list of addresses OCWIP
        // cannot register, written down nowhere.
        Assert.Empty(Options().User.AllowedUserNameCharacters);
    }

    [Fact]
    public void Lockout_thresholds_are_left_to_the_login_card()
    {
        // A proof of ABSENCE. The lockout columns exist and are enabled in the
        // store, and the numbers belong to T-12.3: guessing them here would put
        // a brute force policy nobody decided on into production, and the card
        // that owns it would find the decision already made.
        var lockout = Options().Lockout;
        var defaults = new IdentityOptions().Lockout;

        Assert.Equal(defaults.MaxFailedAccessAttempts, lockout.MaxFailedAccessAttempts);
        Assert.Equal(defaults.DefaultLockoutTimeSpan, lockout.DefaultLockoutTimeSpan);
        Assert.Equal(defaults.AllowedForNewUsers, lockout.AllowedForNewUsers);
        Assert.Equal(
            new IdentityOptions().SignIn.RequireConfirmedEmail,
            Options().SignIn.RequireConfirmedEmail);
    }

    [RequiresDatabaseFact]
    public async Task An_address_a_username_filter_would_refuse_still_registers()
    {
        // Arrange
        // The option above asserted through the behaviour it exists for, and
        // through the real UserManager rather than through a validator called by
        // hand: an apostrophe is legal in the local part of an address, and it
        // is not in Identity's default username character list. This is the
        // account T-12.1 would have failed to create, in Polish nowhere, for a
        // rule written down nowhere.
        var manager = CreateUserManager();

        var email = $"o'brien-{Guid.NewGuid():N}@example.org";
        var user = new User
        {
            FirstName = "Adam",
            LastName = "Testowy",
            Email = email,
            UserName = email,
            IsActive = true,
        };

        // Act
        // An obvious placeholder that happens to satisfy the policy above.
        // Nothing here is meant to look like a real credential.
        var result = await manager.CreateAsync(user, "Placeholder-1");

        // Assert
        Assert.True(
            result.Succeeded,
            string.Join("; ", result.Errors.Select(x => $"{x.Code}: {x.Description}")));
    }

    /// <summary>
    /// A UserManager built the way Program.cs builds it, over the test database,
    /// so the validators that run here are the ones registration will meet.
    /// </summary>
    private UserManager<User> CreateUserManager()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options =>
            options.UseOcwipPostgres(_database.ConnectionString!));
        services
            .AddIdentityCore<User>()
            .AddErrorDescriber<CustomPasswordErrorConfiguration>()
            .AddEntityFrameworkStores<AppDbContext>();
        services.AddIdentityConfiguration();

        return services.BuildServiceProvider().GetRequiredService<UserManager<User>>();
    }
}
