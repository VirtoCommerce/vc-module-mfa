using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Moq;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Data.Services;
using VirtoCommerce.NotificationsModule.Core.Model;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Events;
using VirtoCommerce.Platform.Core.Security.SignInLog;
using VirtoCommerce.Platform.Security.Exceptions;
using Xunit;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public class BackofficeMfaSignInServiceTests : MfaTestsBase
{
    private readonly Mock<IEventPublisher> _eventPublisher = new();
    private readonly BackofficeMfaSignInService _service;

    public BackofficeMfaSignInServiceTests()
    {
        _service = new BackofficeMfaSignInService(SignInManager.Object, MfaService, CodeAttemptLimiter, _eventPublisher.Object, ErrorDescriber);

        UserManager
            .Setup(x => x.IsInRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser user, string role) => user.Roles?.Any(x => x.Name == role) == true);
    }

    [Fact]
    public async Task SendCodeAsync_Should_SendCodeToTheUserWaitingForTheSecondFactor()
    {
        // Arrange
        TwoFactorAuthenticationUser = BackofficeUser;

        // Act
        var result = await _service.SendCodeAsync();

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
        Assert.Equal(EmailProvider, result.Provider);
        Assert.Equal(EmailProvider, Assert.Single(result.Providers).Name);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Once);
    }

    [Fact]
    public async Task SendCodeAsync_Should_ReportInvalidSession_When_NoUserWaitsForTheSecondFactor()
    {
        // Act
        var result = await _service.SendCodeAsync();

        // Assert
        Assert.Equal(MfaOutcome.InvalidTwoFactorToken, result.Outcome);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task SendCodeAsync_Should_NotSendCodeToCustomers()
    {
        // Arrange
        ActiveUser.Roles = [new Role { Name = PlatformConstants.Security.SystemRoles.Customer }];
        TwoFactorAuthenticationUser = ActiveUser;

        // Act
        var result = await _service.SendCodeAsync();

        // Assert
        Assert.Equal(MfaOutcome.SignInNotAllowed, result.Outcome);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Never);
        _eventPublisher.Verify(x => x.Publish(It.Is<UserSignInAttemptEvent>(e =>
            !e.Succeeded && e.UserId == ActiveUserId && e.FailureReason == SignInFailureReason.NotAllowed)), Times.Once);
    }

    [Theory]
    [InlineData(LockoutDisabledUserId, MfaOutcome.LockoutDisabled, ModuleConstants.Security.FailureReason.LockoutDisabled)]
    [InlineData(UnconfirmedEmailUserId, MfaOutcome.ProviderNotAvailable, ModuleConstants.Security.FailureReason.ProviderNotAvailable)]
    public async Task SendCodeAsync_Should_LogTheReason_When_NoCodeCanBeSent(string userId, MfaOutcome expectedOutcome, string expectedReason)
    {
        // Arrange
        TwoFactorAuthenticationUser = await UserManager.Object.FindByIdAsync(userId);

        // Act
        var result = await _service.SendCodeAsync();

        // Assert
        Assert.Equal(expectedOutcome, result.Outcome);
        _eventPublisher.Verify(x => x.Publish(It.Is<UserSignInAttemptEvent>(e =>
            !e.Succeeded && e.UserId == userId && e.FailureReason == expectedReason)), Times.Once);
    }

    [Fact]
    public async Task SendCodeAsync_Should_NotLog_When_TheCodeIsSent()
    {
        // Arrange
        TwoFactorAuthenticationUser = BackofficeUser;

        // Act
        await _service.SendCodeAsync();

        // Assert
        _eventPublisher.Verify(x => x.Publish(It.IsAny<UserSignInAttemptEvent>()), Times.Never);
    }

    [Fact]
    public async Task SignInAsync_Should_SignInWithTheIdentityTwoFactorFlow_When_CodeIsValid()
    {
        // Arrange
        TwoFactorAuthenticationUser = BackofficeUser;

        // Act
        var result = await _service.SignInAsync(ValidCode, rememberMe: true);

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
        SignInManager.Verify(x => x.TwoFactorSignInAsync(EmailProvider, ValidCode, true, false), Times.Once);
        UserManager.Verify(x => x.UpdateAsync(It.Is<ApplicationUser>(u => u.Id == BackofficeUserId && u.LastLoginDate != null)), Times.Once);
        _eventPublisher.Verify(x => x.Publish(It.IsAny<UserLoginEvent>()), Times.Once);
        VerifySignInAttempt(succeeded: true, failureReason: null);
    }

    [Fact]
    public async Task SignInAsync_Should_RejectTheCode_When_ItWasAlreadyAccepted()
    {
        // Arrange
        TwoFactorAuthenticationUser = BackofficeUser;
        await _service.SignInAsync(ValidCode);

        // Act
        var result = await _service.SignInAsync(ValidCode);

        // Assert
        Assert.Equal(MfaOutcome.InvalidCode, result.Outcome);
        SignInManager.Verify(x => x.TwoFactorSignInAsync(EmailProvider, ValidCode, false, false), Times.Exactly(2));
    }

    [Fact]
    public async Task SendCodeAsync_Should_SendANewCodeAtOnce_When_TheUserSignsInAgainAfterTheCodeWasAccepted()
    {
        // Arrange
        TwoFactorAuthenticationUser = BackofficeUser;
        await _service.SendCodeAsync();
        await _service.SignInAsync(CurrentCode(BackofficeUser));

        // Act
        var sendResult = await _service.SendCodeAsync();
        var result = await _service.SignInAsync(CurrentCode(BackofficeUser));

        // Assert
        Assert.Equal(MfaOutcome.Success, sendResult.Outcome);
        Assert.Equal(MfaOutcome.Success, result.Outcome);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SignInAsync_Should_SignOutAndRejectTheCode_When_TheCodeStampIsNotUpdated()
    {
        // Arrange
        TwoFactorAuthenticationUser = BackofficeUser;
        CodeStampUpdateResult = IdentityResult.Failed();

        // Act
        var result = await _service.SignInAsync(ValidCode);

        // Assert
        Assert.Equal(MfaOutcome.AcceptedCodeNotRecorded, result.Outcome);
        SignInManager.Verify(x => x.SignOutAsync(), Times.Once);
        _eventPublisher.Verify(x => x.Publish(It.IsAny<UserLoginEvent>()), Times.Never);
        VerifySignInAttempt(succeeded: false, failureReason: ModuleConstants.Security.FailureReason.AcceptedCodeNotRecorded);
    }

    [Fact]
    public async Task SignInAsync_Should_SignOutAndReportDuplicateEmail_When_SeveralAccountsShareTheEmail()
    {
        // Arrange
        TwoFactorAuthenticationUser = BackofficeUser;
        UserManager
            .Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ThrowsAsync(new DuplicateEmailException("manager@acme.com"));

        // Act
        var result = await _service.SignInAsync(ValidCode);

        // Assert
        Assert.Equal(MfaOutcome.DuplicateEmail, result.Outcome);
        SignInManager.Verify(x => x.SignOutAsync(), Times.Once);
        VerifySignInAttempt(succeeded: false, failureReason: SignInFailureReason.DuplicateEmail);
    }

    [Fact]
    public async Task SignInAsync_Should_ReportInvalidCode_When_IdentityRejectsTheCode()
    {
        // Arrange
        TwoFactorAuthenticationUser = BackofficeUser;

        // Act
        var result = await _service.SignInAsync(InvalidCode);

        // Assert
        Assert.Equal(MfaOutcome.InvalidCode, result.Outcome);
        _eventPublisher.Verify(x => x.Publish(It.IsAny<UserLoginEvent>()), Times.Never);
        VerifySignInAttempt(succeeded: false, ModuleConstants.Security.FailureReason.InvalidCode);
    }

    [Fact]
    public async Task SignInAsync_Should_ReportLockout_When_TheWrongCodeLocksTheAccount()
    {
        // Arrange
        BackofficeUser.AccessFailedCount = IdentityOptions.Value.Lockout.MaxFailedAccessAttempts - 1;
        TwoFactorAuthenticationUser = BackofficeUser;

        // Act
        var result = await _service.SignInAsync(InvalidCode);

        // Assert
        Assert.Equal(MfaOutcome.AccountLocked, result.Outcome);
        VerifySignInAttempt(succeeded: false, SignInFailureReason.LockedOut);
    }

    [Fact]
    public async Task SignInAsync_Should_RejectAProviderTheModuleDoesNotSendCodesWith()
    {
        // Arrange
        TwoFactorAuthenticationUser = BackofficeUser;

        // Act
        var result = await _service.SignInAsync(ValidCode, "Authenticator");

        // Assert
        Assert.Equal(MfaOutcome.ProviderNotAvailable, result.Outcome);
        SignInManager.Verify(x => x.TwoFactorSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task SignInAsync_Should_NotSignInCustomers()
    {
        // Arrange
        ActiveUser.Roles = [new Role { Name = PlatformConstants.Security.SystemRoles.Customer }];
        TwoFactorAuthenticationUser = ActiveUser;

        // Act
        var result = await _service.SignInAsync(ValidCode);

        // Assert
        Assert.Equal(MfaOutcome.SignInNotAllowed, result.Outcome);
        SignInManager.Verify(x => x.TwoFactorSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
        VerifySignInAttempt(succeeded: false, SignInFailureReason.NotAllowed);
    }

    [Fact]
    public async Task SignInAsync_Should_NotCheckTheCode_When_LockoutIsDisabled()
    {
        // Arrange
        TwoFactorAuthenticationUser = LockoutDisabledUser;

        // Act
        var result = await _service.SignInAsync(ValidCode);

        // Assert
        Assert.Equal(MfaOutcome.LockoutDisabled, result.Outcome);
        SignInManager.Verify(x => x.TwoFactorSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task SignInAsync_Should_NotCheckTheCode_When_AccountIsAlreadyLocked()
    {
        // Arrange
        TwoFactorAuthenticationUser = LockedUser;

        // Act
        var result = await _service.SignInAsync(ValidCode);

        // Assert
        Assert.Equal(MfaOutcome.AccountLocked, result.Outcome);
        SignInManager.Verify(x => x.TwoFactorSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
        VerifySignInAttempt(succeeded: false, SignInFailureReason.LockedOut);
    }

    [Fact]
    public async Task SignInAsync_Should_ReportInvalidSession_When_NoUserWaitsForTheSecondFactor()
    {
        // Act
        var result = await _service.SignInAsync(ValidCode);

        // Assert
        Assert.Equal(MfaOutcome.InvalidTwoFactorToken, result.Outcome);
        SignInManager.Verify(x => x.TwoFactorSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task SignInAsync_Should_NotCheckTheCode_When_TheWrongCodeLimitIsReached()
    {
        // Arrange
        TwoFactorAuthenticationUser = BackofficeUser;
        for (var i = 0; i < MfaOptions.MaxCodeAttempts; i++)
        {
            await _service.SignInAsync(InvalidCode);
        }

        // Act
        var result = await _service.SignInAsync(ValidCode);

        // Assert
        Assert.Equal(MfaOutcome.CodeAttemptsExceeded, result.Outcome);
        SignInManager.Verify(x => x.TwoFactorSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Exactly(MfaOptions.MaxCodeAttempts));
    }

    [Fact]
    public async Task SignInAsync_Should_CountTheAttemptBeforeTheCodeIsChecked()
    {
        // Arrange
        TwoFactorAuthenticationUser = BackofficeUser;
        for (var i = 0; i < MfaOptions.MaxCodeAttempts - 1; i++)
        {
            await CodeAttemptLimiter.TryConsumeAttemptAsync(BackofficeUserId);
        }

        bool? hadAttemptsLeftWhileChecking = null;
        SignInManager
            .Setup(x => x.TwoFactorSignInAsync(EmailProvider, InvalidCode, false, false))
            .Returns(async () =>
            {
                hadAttemptsLeftWhileChecking = await CodeAttemptLimiter.HasAttemptsLeftAsync(BackofficeUserId);
                return IdentitySignInResult.Failed;
            });

        // Act
        await _service.SignInAsync(InvalidCode);

        // Assert
        Assert.False(hadAttemptsLeftWhileChecking);
    }

    [Fact]
    public async Task SignInAsync_Should_ShareTheWrongCodeLimitWithTheTokenEndpoint()
    {
        // Arrange
        TwoFactorAuthenticationUser = ActiveUser;
        for (var i = 0; i < MfaOptions.MaxCodeAttempts; i++)
        {
            await MfaService.VerifyCodeAsync(CreateTwoFactorToken(), InvalidCode, StoreId);
        }

        // Act
        var result = await _service.SignInAsync(ValidCode);

        // Assert
        Assert.Equal(MfaOutcome.CodeAttemptsExceeded, result.Outcome);
        SignInManager.Verify(x => x.TwoFactorSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
        VerifySignInAttempt(succeeded: false, ModuleConstants.Security.FailureReason.CodeAttemptsExceeded);
    }

    [Fact]
    public async Task SendCodeAsync_Should_NotSendCode_When_TheWrongCodeLimitIsReached()
    {
        // Arrange
        TwoFactorAuthenticationUser = BackofficeUser;
        for (var i = 0; i < MfaOptions.MaxCodeAttempts; i++)
        {
            await CodeAttemptLimiter.TryConsumeAttemptAsync(BackofficeUserId);
        }

        // Act
        var result = await _service.SendCodeAsync();

        // Assert
        Assert.Equal(MfaOutcome.CodeAttemptsExceeded, result.Outcome);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task SignInAsync_Should_RestoreTheAttempts_When_TheCodeIsAccepted()
    {
        // Arrange
        TwoFactorAuthenticationUser = BackofficeUser;
        await CodeAttemptLimiter.TryConsumeAttemptAsync(BackofficeUserId);
        await CodeAttemptLimiter.TryConsumeAttemptAsync(BackofficeUserId);

        // Act
        var result = await _service.SignInAsync(ValidCode);

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
        for (var i = 0; i < MfaOptions.MaxCodeAttempts; i++)
        {
            Assert.True(await CodeAttemptLimiter.TryConsumeAttemptAsync(BackofficeUserId));
        }
    }

    private void VerifySignInAttempt(bool succeeded, string failureReason)
    {
        _eventPublisher.Verify(x => x.Publish(It.Is<UserSignInAttemptEvent>(e =>
            e.Succeeded == succeeded &&
            e.FailureReason == failureReason &&
            e.SignInType == ModuleConstants.Security.SignInType)), Times.Once);
    }
}
