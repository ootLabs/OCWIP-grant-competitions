using Microsoft.AspNetCore.Identity;

namespace Ocwip.Api.Configuration
{
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
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;

                // One address, one account (UserConfiguration.cs's unique index
                // on the normalized address is the enforcement; this is what
                // makes Identity itself agree with it rather than just the DB).
                options.User.RequireUniqueEmail = true;

                // UserName mirrors the address (UserConfiguration.cs), so
                // Identity's username character filter, meant for usernames,
                // would decide which ADDRESSES may register. Its default
                // allows only a-zA-Z0-9-._@+, which refuses a legal address
                // such as o'brien@example.org. Empty switches the filter off;
                // see Configuration/IdentityConfigurationTests.cs.
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
}
