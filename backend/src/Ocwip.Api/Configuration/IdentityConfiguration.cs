using Microsoft.AspNetCore.Identity;

namespace Ocwip.Api.Configuration;

public static class IdentityConfiguration
{
    /// <summary>
    /// Identity's options, and only the ones this card owns.
    ///
    /// The password policy and unique addresses are here because registration
    /// (T-12.1) enforces them on its first write. Lockout thresholds and whether
    /// an unconfirmed address may sign in are login's decisions (T-12.3) and are
    /// deliberately left at Identity's defaults rather than guessed here: the
    /// lockout COLUMNS exist and are enabled in the store, so that card sets
    /// numbers, not infrastructure.
    /// </summary>
    public static IServiceCollection AddIdentityConfiguration(
        this IServiceCollection services)
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
        });

        return services;
    }
}
