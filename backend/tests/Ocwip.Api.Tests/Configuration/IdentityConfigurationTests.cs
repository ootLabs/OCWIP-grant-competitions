using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
///
/// Also covers the token lifespan AddIdentityConfiguration derives from
/// EmailVerification:TokenLifetimeHours, since it governs every Identity
/// data-protection token including the email confirmation token generated in
/// EmailVerificationService - one card, one test class.
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
    public void Lockout_thresholds_are_set_by_this_card()
    {
        // T-12.5 turns the dormant lockout columns into a decision: five
        // failed attempts, fifteen minutes, every account. The confirmed
        // email check stays at Identity's own default (off), which is a
        // SEPARATE, deliberate absence covered by its own comment in
        // IdentityConfiguration.cs.
        var lockout = Options().Lockout;

        Assert.Equal(
            IdentityConfiguration.DefaultMaxFailedLoginAttempts,
            lockout.MaxFailedAccessAttempts);
        Assert.Equal(
            TimeSpan.FromMinutes(IdentityConfiguration.DefaultLockoutMinutes),
            lockout.DefaultLockoutTimeSpan);
        Assert.True(lockout.AllowedForNewUsers);
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

    private static TimeSpan TokenLifespanFor(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddIdentityConfiguration(configuration);

        using var provider = services.BuildServiceProvider();
        return provider
            .GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>()
            .Value
            .TokenLifespan;
    }

    [Fact]
    public void Falls_back_to_the_documented_default_when_unconfigured()
    {
        var configuration = new ConfigurationBuilder().Build();

        var lifespan = TokenLifespanFor(configuration);

        Assert.Equal(
            TimeSpan.FromHours(IdentityConfiguration.DefaultTokenLifetimeHours),
            lifespan);
    }

    [Fact]
    public void Honors_a_configured_token_lifetime()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EmailVerification:TokenLifetimeHours"] = "2",
            })
            .Build();

        var lifespan = TokenLifespanFor(configuration);

        Assert.Equal(TimeSpan.FromHours(2), lifespan);
    }

    private static LockoutOptions LockoutFor(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddIdentityConfiguration(configuration);

        using var provider = services.BuildServiceProvider();
        return provider
            .GetRequiredService<IOptions<IdentityOptions>>()
            .Value
            .Lockout;
    }

    [Fact]
    public void Honors_a_configured_lockout_threshold_and_duration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:MaxFailedLoginAttempts"] = "3",
                ["Auth:LockoutMinutes"] = "30",
            })
            .Build();

        var lockout = LockoutFor(configuration);

        Assert.Equal(3, lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(30), lockout.DefaultLockoutTimeSpan);
    }

    [Theory]
    [InlineData("Auth:MaxFailedLoginAttempts", "0")]
    [InlineData("Auth:MaxFailedLoginAttempts", "-1")]
    [InlineData("Auth:LockoutMinutes", "0")]
    [InlineData("Auth:LockoutMinutes", "-1")]
    public void Refuses_to_start_with_a_non_positive_lockout_setting(string key, string value)
    {
        // A limit of zero or less does not turn lockout off, it locks every
        // account out on its first wrong password, forever if the duration is
        // also non positive. Refusing to start is the cheap version of that
        // outage, the same reasoning AuthenticationConfiguration applies to
        // Auth:SessionLifetimeHours.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })
            .Build();

        Assert.Throws<InvalidOperationException>(() => LockoutFor(configuration));
    }
}
