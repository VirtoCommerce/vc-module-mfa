using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.SignInLog;
using VirtoCommerce.Platform.Security.OpenIddict;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace VirtoCommerce.MFA.Data.Services;

public class MfaErrorDescriber(TimeProvider timeProvider)
{
    public virtual IdentityError TwoFactorRequired() => new()
    {
        Code = "two_factor_required",
        Description = "Enter the sign-in code to complete the sign-in.",
    };

    public virtual IdentityError MissingParameters(IEnumerable<string> parameters) => new()
    {
        Code = "missing_parameter",
        Description = $"Missing required parameters: {string.Join(", ", parameters)}.",
    };

    public virtual IdentityError InvalidTwoFactorToken() => new()
    {
        Code = "invalid_two_factor_token",
        Description = "The sign-in session is invalid or has expired. Sign in again.",
    };

    public virtual IdentityError LockoutDisabled() => new()
    {
        Code = "lockout_disabled",
        Description = "Two-factor sign-in is not available for accounts without lockout protection.",
    };

    public virtual IdentityError ProviderNotAvailable() => new()
    {
        Code = "mfa_provider_not_available",
        Description = "No way to send a sign-in code is available for this account.",
    };

    public virtual IdentityError InvalidCode() => new()
    {
        Code = "invalid_code",
        Description = "The code is invalid or has expired.",
    };

    public virtual IdentityError CodeAttemptsExceeded() => new()
    {
        Code = "code_attempts_exceeded",
        Description = "Too many incorrect codes. Wait a few minutes and sign in again.",
    };

    public virtual IdentityError ResendTooSoon() => new()
    {
        Code = "resend_too_soon",
        Description = "A code has just been sent. Wait before requesting a new one.",
    };

    public virtual IdentityError LoginFailed() => ToIdentityError(SecurityErrorDescriber.LoginFailed());

    public virtual IdentityError SignInNotAllowed() => ToIdentityError(SecurityErrorDescriber.SignInNotAllowed());

    public virtual IdentityError GetError(MfaOutcome outcome, ApplicationUser user)
    {
        return outcome switch
        {
            MfaOutcome.Success => null,
            MfaOutcome.InvalidTwoFactorToken or MfaOutcome.FailedAttemptNotRecorded => InvalidTwoFactorToken(),
            MfaOutcome.InvalidCode => InvalidCode(),
            MfaOutcome.CodeAttemptsExceeded => CodeAttemptsExceeded(),
            MfaOutcome.AccountLocked => GetLockoutError(user),
            MfaOutcome.LockoutDisabled => LockoutDisabled(),
            MfaOutcome.ProviderNotAvailable => ProviderNotAvailable(),
            MfaOutcome.ResendTooSoon => ResendTooSoon(),
            MfaOutcome.SignInNotAllowed => SignInNotAllowed(),
            _ => LoginFailed(),
        };
    }

    public virtual IdentityError GetError(MfaResult result, bool reportLockout, IdentityError hiddenLockoutError = null)
    {
        return IsLockoutHidden(result, reportLockout)
            ? hiddenLockoutError ?? LoginFailed()
            : GetError(result.Outcome, result.User);
    }

    public virtual string GetFailureReason(MfaOutcome outcome)
    {
        return outcome switch
        {
            MfaOutcome.InvalidTwoFactorToken => ModuleConstants.Security.FailureReason.InvalidTwoFactorToken,
            MfaOutcome.CodeAttemptsExceeded => ModuleConstants.Security.FailureReason.CodeAttemptsExceeded,
            MfaOutcome.FailedAttemptNotRecorded => ModuleConstants.Security.FailureReason.FailedAttemptNotRecorded,
            MfaOutcome.AcceptedCodeNotRecorded => ModuleConstants.Security.FailureReason.AcceptedCodeNotRecorded,
            MfaOutcome.InvalidCode => ModuleConstants.Security.FailureReason.InvalidCode,
            MfaOutcome.AccountLocked => SignInFailureReason.LockedOut,
            MfaOutcome.LockoutDisabled => ModuleConstants.Security.FailureReason.LockoutDisabled,
            MfaOutcome.ProviderNotAvailable => ModuleConstants.Security.FailureReason.ProviderNotAvailable,
            MfaOutcome.SignInNotAllowed => SignInFailureReason.NotAllowed,
            MfaOutcome.DuplicateEmail => SignInFailureReason.DuplicateEmail,
            _ => SignInFailureReason.Unknown,
        };
    }

    public virtual IdentityError GetLockoutError(ApplicationUser user)
    {
        return user?.LockoutEnd == DateTime.MaxValue.ToUniversalTime()
            ? ToIdentityError(SecurityErrorDescriber.UserIsLockedOut())
            : ToIdentityError(SecurityErrorDescriber.UserIsTemporaryLockedOut());
    }

    public virtual int? GetLockoutSecondsRemaining(ApplicationUser user)
    {
        if (user?.LockoutEnd is null)
        {
            return null;
        }

        var seconds = Math.Ceiling((user.LockoutEnd.Value - timeProvider.GetUtcNow()).TotalSeconds);

        return (int)Math.Clamp(seconds, 0, int.MaxValue);
    }

    public virtual int? GetLockoutSecondsRemaining(MfaResult result, bool reportLockout)
    {
        return result.Outcome == MfaOutcome.AccountLocked && !IsLockoutHidden(result, reportLockout)
            ? GetLockoutSecondsRemaining(result.User)
            : null;
    }

    public virtual TokenResponse ToTokenResponse(IdentityError error, string errorType = Errors.InvalidGrant, int? lockoutSecondsRemaining = null)
    {
        var response = AbstractTypeFactory<TokenResponse>.TryCreateInstance();
        response.Error = errorType;
        response.Code = error.Code;
        response.ErrorDescription = error.Description;
        response.LockoutSecondsRemaining = lockoutSecondsRemaining;

        return response;
    }

    public virtual TokenResponse ToTokenResponse(MfaResult result, bool reportLockout, IdentityError hiddenLockoutError = null)
    {
        return ToTokenResponse(
            GetError(result, reportLockout, hiddenLockoutError),
            lockoutSecondsRemaining: GetLockoutSecondsRemaining(result, reportLockout));
    }

    protected virtual bool IsLockoutHidden(MfaResult result, bool reportLockout)
    {
        return result.Outcome == MfaOutcome.AccountLocked && !reportLockout;
    }

    protected virtual IdentityError ToIdentityError(TokenResponse response) => new()
    {
        Code = response.Code,
        Description = response.ErrorDescription,
    };
}
