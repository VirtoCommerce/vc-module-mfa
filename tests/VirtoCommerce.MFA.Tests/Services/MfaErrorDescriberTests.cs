using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.Platform.Core.Security.SignInLog;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public class MfaErrorDescriberTests : MfaTestsBase
{
    [Theory]
    [InlineData(MfaOutcome.Success, null)]
    [InlineData(MfaOutcome.InvalidTwoFactorToken, "invalid_two_factor_token")]
    [InlineData(MfaOutcome.FailedAttemptNotRecorded, "invalid_two_factor_token")]
    [InlineData(MfaOutcome.CodeAttemptsExceeded, "code_attempts_exceeded")]
    [InlineData(MfaOutcome.InvalidCode, "invalid_code")]
    [InlineData(MfaOutcome.AcceptedCodeNotRecorded, "login_failed")]
    [InlineData(MfaOutcome.AccountLocked, "user_is_temporary_locked_out")]
    [InlineData(MfaOutcome.LockoutDisabled, "lockout_disabled")]
    [InlineData(MfaOutcome.ProviderNotAvailable, "mfa_provider_not_available")]
    [InlineData(MfaOutcome.ResendTooSoon, "resend_too_soon")]
    [InlineData(MfaOutcome.SignInNotAllowed, "sign_in_not_allowed")]
    [InlineData(MfaOutcome.DuplicateEmail, "login_failed")]
    [InlineData(MfaOutcome.Undefined, "login_failed")]
    public void GetError_Should_DescribeEachOutcome(MfaOutcome outcome, string expectedCode)
    {
        // Act
        var error = ErrorDescriber.GetError(outcome, LockedUser);

        // Assert
        Assert.Equal(expectedCode, error?.Code);
    }

    [Theory]
    [InlineData(MfaOutcome.InvalidTwoFactorToken, ModuleConstants.Security.FailureReason.InvalidTwoFactorToken)]
    [InlineData(MfaOutcome.FailedAttemptNotRecorded, ModuleConstants.Security.FailureReason.FailedAttemptNotRecorded)]
    [InlineData(MfaOutcome.AcceptedCodeNotRecorded, ModuleConstants.Security.FailureReason.AcceptedCodeNotRecorded)]
    [InlineData(MfaOutcome.CodeAttemptsExceeded, ModuleConstants.Security.FailureReason.CodeAttemptsExceeded)]
    [InlineData(MfaOutcome.InvalidCode, ModuleConstants.Security.FailureReason.InvalidCode)]
    [InlineData(MfaOutcome.AccountLocked, SignInFailureReason.LockedOut)]
    [InlineData(MfaOutcome.LockoutDisabled, ModuleConstants.Security.FailureReason.LockoutDisabled)]
    [InlineData(MfaOutcome.ProviderNotAvailable, ModuleConstants.Security.FailureReason.ProviderNotAvailable)]
    [InlineData(MfaOutcome.SignInNotAllowed, SignInFailureReason.NotAllowed)]
    [InlineData(MfaOutcome.DuplicateEmail, SignInFailureReason.DuplicateEmail)]
    [InlineData(MfaOutcome.Undefined, SignInFailureReason.Unknown)]
    public void GetFailureReason_Should_DescribeEachOutcome(MfaOutcome outcome, string expectedReason)
    {
        // Act
        var reason = ErrorDescriber.GetFailureReason(outcome);

        // Assert
        Assert.Equal(expectedReason, reason);
    }

    [Theory]
    [InlineData(true, "user_is_temporary_locked_out", LockoutSeconds)]
    [InlineData(false, "invalid_code", null)]
    public void ToTokenResponse_Should_HideTheLockout_Unless_ItIsReported(bool reportLockout, string expectedCode, int? expectedSeconds)
    {
        // Arrange
        var result = MfaResult.Fail(MfaOutcome.AccountLocked, LockedUser);

        // Act
        var response = ErrorDescriber.ToTokenResponse(result, reportLockout, ErrorDescriber.InvalidCode());

        // Assert
        Assert.Equal(expectedCode, response.Code);
        Assert.Equal(expectedSeconds, response.LockoutSecondsRemaining);
    }
}
