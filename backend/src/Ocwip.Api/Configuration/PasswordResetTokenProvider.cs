using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Ocwip.Api.Configuration;

/// <summary>
/// Its own subclass of DataProtectionTokenProviderOptions, not the shared one
/// GenerateEmailConfirmationTokenAsync already uses. Both a password reset
/// token and an email confirmation token are, underneath, the same
/// DataProtectorTokenProvider, and Identity resolves its options through a
/// single unnamed IOptions&lt;DataProtectionTokenProviderOptions&gt; - configuring
/// TokenLifespan there would set ONE lifetime for both, and this card asks for
/// a short one (proposed 1h) while email confirmation keeps 24h. A distinct
/// options type gets its own IOptions&lt;T&gt; slot, so the two lifetimes stop
/// being the same setting under two names.
/// </summary>
public sealed class PasswordResetTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public PasswordResetTokenProviderOptions()
    {
        // Own purpose string for the data protector, so a reset token and an
        // email confirmation token are protected under different purposes,
        // not only distinguished by the "purpose" field inside the payload.
        Name = "PasswordResetTokenProvider";
    }
}

/// <summary>
/// Same behaviour as the built in DataProtectorTokenProvider, over the
/// options type above. Registered under Identity's PasswordReset provider
/// name (see IdentityConfiguration.AddIdentityConfiguration), so
/// GeneratePasswordResetTokenAsync/ResetPasswordAsync go through this
/// instance instead of the "Default" one.
/// </summary>
public sealed class PasswordResetTokenProvider<TUser>(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<PasswordResetTokenProviderOptions> options,
    ILogger<PasswordResetTokenProvider<TUser>> logger)
    : DataProtectorTokenProvider<TUser>(dataProtectionProvider, options, logger)
    where TUser : class;
