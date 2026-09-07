using Microsoft.AspNetCore.Identity;

namespace Ocwip.Api.Configuration;

/// <summary>
/// Identity's options, and only the ones this card owns.
///
/// The password policy, unique addresses and the username character filter are
/// here because registration (T-12.1) enforces them on its first write. Lockout
/// thresholds and whether an unconfirmed address may sign in are login's
/// decisions (T-12.3) and are deliberately left at Identity's defaults rather
/// than guessed here: the lockout COLUMNS exist and are enabled in the store,
/// so that card sets numbers, not infrastructure.
/// </summary>
public static class IdentityConfiguration
{
    // Kept in sync with the "valid for N hours" wording in the
    // verification email (see EmailVerificationService) - if this
    // changes, that message should change with it.
    public const int DefaultTokenLifetimeHours = 24;

    // Configuration is optional: callers that only care about the fixed
    // password/username policy (e.g. Configuration/IdentityConfigurationTests.cs
    // building a UserManager over a test database) have no IConfiguration
    // of their own to pass, and the only thing configuration ever
    // supplies is the token lifetime override below, which already has a
    // documented default.
    public static IServiceCollection AddIdentityConfiguration(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        services.Configure<IdentityOptions>(options =>
        {
            // Eight characters with four character classes. The messages that
            // come back when a password fails are Polish, see
            // CustomPasswordErrorConfiguration.
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireNonAlphanumeric = true;

            // Belt as well as braces: the unique index on the normalized
            // address is what actually enforces this (UserConfiguration.cs).
            // Identity checking it too turns the second registration on one
            // address into a validation result instead of a 23505 the caller
            // has to unwrap, and T-12.1 has to handle BOTH regardless, because
            // this check loses the race against a concurrent insert.
            options.User.RequireUniqueEmail = true;

            // Empty, which switches the check off, and that belongs to this
            // card rather than to T-12.1. An account here is identified by its
            // address and UserName mirrors it (UserConfiguration.cs), so this
            // filter, which Identity means for usernames, ends up deciding
            // which ADDRESSES may register. Its default allows only
            // a-zA-Z0-9-._@+, so a legal address such as o'brien@example.org is
            // refused, with the English "User name is invalid, can only contain
            // letters or digits" that CustomPasswordErrorConfiguration exists
            // to avoid, for a rule nobody wrote down.
            //
            // What an address has to look like is validated at the API edge
            // (RegisterRequestValidator.cs), in one place, in Polish, against
            // the address rather than against a username we do not have.
            options.User.AllowedUserNameCharacters = string.Empty;
        });

        // Governs every Identity data-protection token, including the
        // email confirmation token generated in EmailVerificationService.
        // Left unset, it silently follows whatever the framework's
        // current default is, which is not what the verification email
        // promises the user.
        var tokenLifetimeHours = configuration?.GetValue<int?>(
            "EmailVerification:TokenLifetimeHours")
            ?? DefaultTokenLifetimeHours;

        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromHours(tokenLifetimeHours);
        });

        return services;
    }
}
