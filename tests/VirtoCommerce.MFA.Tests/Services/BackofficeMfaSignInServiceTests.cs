using System.Threading.Tasks;
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
        _service = new BackofficeMfaSignInService(SignInManager.Object, MfaService, _eventPublisher.Object);

        UserManager
            .Setup(x => x.IsInRoleAsync(It.IsAny<ApplicationUser>(), PlatformConstants.Security.SystemRoles.Customer))
            .ReturnsAsync(false);
    }

    [Fact]
    public async Task SendCodeAsync_Should_SendCodeToTheUserWaitingForTheSecondFactor()
    {
        // Arrange
        WaitForSecondFactor(BackofficeUser);

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
        WaitForSecondFactor(ActiveUser);
        UserManager.Setup(x => x.IsInRoleAsync(ActiveUser, PlatformConstants.Security.SystemRoles.Customer)).ReturnsAsync(true);

        // Act
        var result = await _service.SendCodeAsync();

        // Assert
        Assert.Equal(MfaOutcome.SignInNotAllowed, result.Outcome);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task SignInAsync_Should_SignInWithTheIdentityTwoFactorFlow_When_CodeIsValid()
    {
        // Arrange
        WaitForSecondFactor(BackofficeUser);
        SignInManager
            .Setup(x => x.TwoFactorSignInAsync(EmailProvider, ValidCode, true, false))
            .ReturnsAsync(IdentitySignInResult.Success);

        // Act
        var result = await _service.SignInAsync(ValidCode, rememberMe: true);

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
        UserManager.Verify(x => x.UpdateAsync(It.Is<ApplicationUser>(u => u.Id == BackofficeUserId && u.LastLoginDate != null)), Times.Once);
        _eventPublisher.Verify(x => x.Publish(It.IsAny<UserLoginEvent>()), Times.Once);
        VerifySignInAttempt(succeeded: true, failureReason: null);
    }

    [Fact]
    public async Task SignInAsync_Should_SignOutAndReportDuplicateEmail_When_SeveralAccountsShareTheEmail()
    {
        // Arrange
        WaitForSecondFactor(BackofficeUser);
        SignInManager
            .Setup(x => x.TwoFactorSignInAsync(EmailProvider, ValidCode, false, false))
            .ReturnsAsync(IdentitySignInResult.Success);
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
        WaitForSecondFactor(BackofficeUser);
        SignInManager
            .Setup(x => x.TwoFactorSignInAsync(EmailProvider, InvalidCode, false, false))
            .ReturnsAsync(IdentitySignInResult.Failed);

        // Act
        var result = await _service.SignInAsync(InvalidCode);

        // Assert
        Assert.Equal(MfaOutcome.InvalidCode, result.Outcome);
        _eventPublisher.Verify(x => x.Publish(It.IsAny<UserLoginEvent>()), Times.Never);
        VerifySignInAttempt(succeeded: false, ModuleConstants.Security.FailureReason.InvalidCode);
    }

    [Fact]
    public async Task SignInAsync_Should_ReportLockout_When_IdentityLocksTheAccount()
    {
        // Arrange
        WaitForSecondFactor(BackofficeUser);
        SignInManager
            .Setup(x => x.TwoFactorSignInAsync(EmailProvider, InvalidCode, false, false))
            .ReturnsAsync(IdentitySignInResult.LockedOut);

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
        WaitForSecondFactor(BackofficeUser);

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
        WaitForSecondFactor(ActiveUser);
        UserManager.Setup(x => x.IsInRoleAsync(ActiveUser, PlatformConstants.Security.SystemRoles.Customer)).ReturnsAsync(true);

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
        WaitForSecondFactor(LockoutDisabledUser);

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
        WaitForSecondFactor(LockedUser);

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

    private void WaitForSecondFactor(ApplicationUser user)
    {
        SignInManager.Setup(x => x.GetTwoFactorAuthenticationUserAsync()).ReturnsAsync(user);
    }

    private void VerifySignInAttempt(bool succeeded, string failureReason)
    {
        _eventPublisher.Verify(x => x.Publish(It.Is<UserSignInAttemptEvent>(e =>
            e.Succeeded == succeeded &&
            e.FailureReason == failureReason &&
            e.SignInType == ModuleConstants.Security.SignInType)), Times.Once);
    }
}
