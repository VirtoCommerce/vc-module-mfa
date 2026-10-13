using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Security.OpenIddict;

namespace VirtoCommerce.MFA.Data.Services;

public static class MfaErrorDescriber
{
    public static IdentityError TwoFactorRequired() => new()
    {
        Code = "two_factor_required",
        Description = "A sign-in code has been sent. Enter it to complete the sign-in.",
    };

    public static IdentityError MissingParameters(IEnumerable<string> parameters) => new()
    {
        Code = "missing_parameter",
        Description = $"Missing required parameters: {string.Join(", ", parameters)}.",
    };

    public static IdentityError InvalidTwoFactorToken() => new()
    {
        Code = "invalid_two_factor_token",
        Description = "The sign-in session is invalid or has expired. Sign in again.",
    };

    public static IdentityError LockoutDisabled() => new()
    {
        Code = "lockout_disabled",
        Description = "Two-factor sign-in is not available for accounts without lockout protection.",
    };

    public static IdentityError ProviderNotAvailable() => new()
    {
        Code = "mfa_provider_not_available",
        Description = "No way to send a sign-in code is available for this account.",
    };

    public static IdentityError InvalidCode() => new()
    {
        Code = "invalid_code",
        Description = "The code is invalid or has expired.",
    };

    public static IdentityError ResendTooSoon() => new()
    {
        Code = "resend_too_soon",
        Description = "A code has just been sent. Wait before requesting a new one.",
    };

    public static IdentityError LoginFailed() => ToIdentityError(SecurityErrorDescriber.LoginFailed());

    public static IdentityError SignInNotAllowed() => ToIdentityError(SecurityErrorDescriber.SignInNotAllowed());

    public static IdentityError GetLockoutError(ApplicationUser user)
    {
        return user.LockoutEnd == DateTime.MaxValue.ToUniversalTime()
            ? ToIdentityError(SecurityErrorDescriber.UserIsLockedOut())
            : ToIdentityError(SecurityErrorDescriber.UserIsTemporaryLockedOut());
    }

    public static int? GetLockoutSecondsRemaining(ApplicationUser user)
    {
        if (user.LockoutEnd is null)
        {
            return null;
        }

        var seconds = Math.Ceiling((user.LockoutEnd.Value - DateTimeOffset.UtcNow).TotalSeconds);

        return (int)Math.Clamp(seconds, 0, int.MaxValue);
    }

    private static IdentityError ToIdentityError(TokenResponse response) => new()
    {
        Code = response.Code,
        Description = response.ErrorDescription,
    };
}
