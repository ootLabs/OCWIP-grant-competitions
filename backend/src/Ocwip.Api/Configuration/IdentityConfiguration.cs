using Microsoft.AspNetCore.Identity;

namespace Ocwip.Api.Configuration;

/// <summary>
/// Identity's options, and only the ones this card owns.
///
/// The password policy, unique addresses and the username character filter are
/// here because registration (T-12.1) enforces them on its first write.
///
/// One thing is still at Identity's default and that is deliberate.
/// SignIn.RequireConfirmedEmail stays OFF although login does refuse an
/// unconfirmed address: turning it on would make Identity check confirmation
/// BEFORE the password, which answers differently for an address that has an
/// account, so login checks it itself afterwards instead (Services/SessionService.cs).
/// The lockout thresholds (T-12.5) ARE set here: the columns already existed,
/// enabled in the store, waiting for this card's numbers.
/// </summary>
public static class IdentityConfiguration
{
    // Kept in sync with the "valid for N hours" wording in the
    // verification email (see EmailVerificationService) - if this
    // changes, that message should change with it.
    public const int DefaultTokenLifetimeHours = 24;

    // Short on purpose (T-12.4): a password reset link is worth less time than
    // an email confirmation link, because it is the one link that can hand
    // over an account someone else is currently using. Kept in sync with the
    // "valid for N hours" wording in PasswordResetService.
    public const int DefaultPasswordResetTokenLifetimeHours = 1;

    // The name Identity's IdentityOptions.Tokens.PasswordResetTokenProvider is
    // pointed at below, so GeneratePasswordResetTokenAsync/ResetPasswordAsync
    // resolve PasswordResetTokenProvider<User> instead of the "Default"
    // DataProtectorTokenProvider that email confirmation uses.
    public const string PasswordResetTokenProviderName = "PasswordReset";

    // T-12.5, the account half of brute force protection. Five is the OWASP
    // baseline: low enough that guessing a password is impractical, high
    // enough that someone who mistypes a password twice does not lock
    // themselves out on the third honest try.
    public const int DefaultMaxFailedLoginAttempts = 5;

    // Fifteen minutes, not Identity's default five. There is no unlock screen
    // in this product yet, so the ONLY way out of a lockout is waiting, and
    // five minutes is short enough that the same automated attempt that
    // caused it just resumes after the wait. Long enough to make a sustained
    // attempt expensive, short enough that a real applicant locked out by
    // their own typos is not blocked for the length of a working day.
    public const int DefaultLockoutMinutes = 15;

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
        var maxFailedLoginAttempts = configuration?.GetValue<int?>(
            "Auth:MaxFailedLoginAttempts")
            ?? DefaultMaxFailedLoginAttempts;

        var lockoutMinutes = configuration?.GetValue<int?>(
            "Auth:LockoutMinutes")
            ?? DefaultLockoutMinutes;

        if (maxFailedLoginAttempts <= 0)
        {
            throw new InvalidOperationException(
                "Auth:MaxFailedLoginAttempts must be greater than zero, "
                + $"and is {maxFailedLoginAttempts}.");
        }

        if (lockoutMinutes <= 0)
        {
            throw new InvalidOperationException(
                "Auth:LockoutMinutes must be greater than zero, "
                + $"and is {lockoutMinutes}.");
        }

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

            // See PasswordResetTokenProviderOptions for why this needs a
            // provider of its own rather than reusing "Default".
            options.Tokens.PasswordResetTokenProvider = PasswordResetTokenProviderName;

            // Identity's own default is already true, restated here because
            // this card is the one that turns lockout from dormant columns
            // into a decision. SessionService.LoginAsync is the caller that
            // actually asks for lockoutOnFailure; without AllowedForNewUsers
            // that request would do nothing for any account.
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = maxFailedLoginAttempts;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(lockoutMinutes);
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

        var passwordResetTokenLifetimeHours = configuration?.GetValue<int?>(
            "PasswordReset:TokenLifetimeHours")
            ?? DefaultPasswordResetTokenLifetimeHours;

        services.Configure<PasswordResetTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromHours(passwordResetTokenLifetimeHours);
        });

        return services;
    }
}
