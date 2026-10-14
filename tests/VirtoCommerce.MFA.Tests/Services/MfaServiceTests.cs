using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Moq;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Notifications;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.MFA.Data.Services;
using VirtoCommerce.NotificationsModule.Core.Model;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.StoreModule.Core.Model;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public class MfaServiceTests : MfaTestsBase
{
    [Fact]
    public async Task ChallengeAsync_Should_SendCodeAndIssueToken()
    {
        // Act
        var result = await MfaService.ChallengeAsync(ActiveUser, StoreId);

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
        var provider = Assert.Single(result.Providers);
        Assert.Equal(EmailProvider, provider.Name);
        Assert.Equal(MaskedActiveUserEmail, provider.Destination);
        Assert.Equal(EmailProvider, result.Provider);

        var ticket = TwoFactorTokenService.ReadToken(result.TwoFactorToken);
        Assert.Equal(ActiveUserId, ticket.UserId);
        Assert.Equal(StoreId, ticket.StoreId);
        Assert.Equal(SecurityStamp, ticket.SecurityStamp);
        Assert.Equal(EmailProvider, ticket.Provider);

        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.Is<Notification>(n =>
            n is MfaSignInEmailNotification &&
            ((MfaSignInEmailNotification)n).Code == ValidCode &&
            ((MfaSignInEmailNotification)n).To == ActiveUserEmail &&
            n.LanguageCode == ContactLanguage)), Times.Once);
    }

    [Fact]
    public async Task ResendCodeAsync_Should_KeepTheExpiryOfTheFirstToken()
    {
        // Arrange
        var twoFactorToken = CreateTwoFactorToken();

        // Act
        var result = await MfaService.ResendCodeAsync(twoFactorToken);

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
        Assert.Equal(
            TwoFactorTokenService.ReadToken(twoFactorToken).IssuedAt,
            TwoFactorTokenService.ReadToken(result.TwoFactorToken).IssuedAt);
    }

    [Fact]
    public async Task ChallengeAsync_Should_ReportTheResendInterval()
    {
        // Act
        var result = await MfaService.ChallengeAsync(ActiveUser, StoreId);

        // Assert
        Assert.Equal(ResendIntervalSeconds, result.ResendSecondsRemaining);
    }

    [Fact]
    public async Task ChallengeAsync_Should_IssueTokenWithoutSendingCode_When_ACodeWasJustSent()
    {
        // Arrange
        await MfaService.ChallengeAsync(ActiveUser, StoreId);
        TimeProvider.Advance(TimeSpan.FromSeconds(10));

        // Act
        var result = await MfaService.ChallengeAsync(ActiveUser, StoreId);

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
        Assert.Equal(ActiveUserId, TwoFactorTokenService.ReadToken(result.TwoFactorToken).UserId);
        Assert.Equal(ResendIntervalSeconds - 10, result.ResendSecondsRemaining);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Once);
    }

    [Fact]
    public async Task ResendCodeAsync_Should_NotSendCode_When_ACodeWasJustSent()
    {
        // Arrange
        var challenge = await MfaService.ChallengeAsync(ActiveUser, StoreId);
        TimeProvider.Advance(TimeSpan.FromSeconds(12.5));

        // Act
        var result = await MfaService.ResendCodeAsync(challenge.TwoFactorToken);

        // Assert
        Assert.Equal(MfaOutcome.ResendTooSoon, result.Outcome);
        Assert.Null(result.TwoFactorToken);
        Assert.Equal(EmailProvider, result.Provider);
        Assert.Equal(ResendIntervalSeconds - 12, result.ResendSecondsRemaining);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Once);
    }

    [Fact]
    public async Task ResendCodeAsync_Should_SendCode_When_TheResendIntervalHasPassed()
    {
        // Arrange
        var challenge = await MfaService.ChallengeAsync(ActiveUser, StoreId);
        TimeProvider.Advance(MfaOptions.CodeResendInterval);

        // Act
        var result = await MfaService.ResendCodeAsync(challenge.TwoFactorToken);

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
        Assert.NotNull(result.TwoFactorToken);
        Assert.Equal(ResendIntervalSeconds, result.ResendSecondsRemaining);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Exactly(2));
    }

    [Fact]
    public async Task SendCodeAsync_Should_NotSendCode_When_ACodeWasJustSent()
    {
        // Arrange
        await MfaService.SendCodeAsync(BackofficeUser);

        // Act
        var result = await MfaService.SendCodeAsync(BackofficeUser);

        // Assert
        Assert.Equal(MfaOutcome.ResendTooSoon, result.Outcome);
        Assert.Equal(EmailProvider, Assert.Single(result.Providers).Name);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Once);
    }

    [Fact]
    public async Task SendCodeAsync_Should_LimitEachUserSeparately()
    {
        // Arrange
        await MfaService.SendCodeAsync(BackofficeUser);

        // Act
        var result = await MfaService.SendCodeAsync(ActiveUser);

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ResendCodeAsync_Should_RejectToken_When_TheUserSignedInAfterItWasIssued()
    {
        // Arrange
        var challenge = await MfaService.ChallengeAsync(ActiveUser, StoreId);
        ActiveUser.LastLoginDate = DateTime.UtcNow;

        // Act
        var result = await MfaService.ResendCodeAsync(challenge.TwoFactorToken);

        // Assert
        Assert.Equal(MfaOutcome.InvalidTwoFactorToken, result.Outcome);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Once);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_Succeed_When_TheUserSignedInBeforeTheTokenWasIssued()
    {
        // Arrange
        ActiveUser.LastLoginDate = DateTime.UtcNow.AddDays(-1);
        var challenge = await MfaService.ChallengeAsync(ActiveUser, StoreId);

        // Act
        var result = await MfaService.VerifyCodeAsync(challenge.TwoFactorToken, ValidCode, StoreId);

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task ChallengeAsync_Should_IssueTokenWithoutStore_When_TheSignInHasNoStore()
    {
        // Act
        var result = await MfaService.ChallengeAsync(BackofficeUser, storeId: null);

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
        Assert.Null(TwoFactorTokenService.ReadToken(result.TwoFactorToken).StoreId);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.Is<Notification>(n => n.LanguageCode == EmployeeLanguage)), Times.Once);
    }

    [Fact]
    public async Task ChallengeAsync_Should_SendCodeWithTheGlobalTemplate_When_TheStoreIsUnknown()
    {
        // Act
        var result = await MfaService.ChallengeAsync(ActiveUser, UnknownStoreId);
        var verifyResult = await MfaService.VerifyCodeAsync(result.TwoFactorToken, ValidCode, UnknownStoreId);

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
        Assert.Equal(UnknownStoreId, TwoFactorTokenService.ReadToken(result.TwoFactorToken).StoreId);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Once);
        Assert.Equal(MfaOutcome.Success, verifyResult.Outcome);
    }

    [Fact]
    public async Task ChallengeAsync_Should_NotSendCode_When_LockoutIsDisabled()
    {
        // Act
        var result = await MfaService.ChallengeAsync(LockoutDisabledUser, StoreId);

        // Assert
        Assert.Equal(MfaOutcome.LockoutDisabled, result.Outcome);
        Assert.Null(result.TwoFactorToken);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Theory]
    [InlineData(NoEmailUserId)]
    [InlineData(UnconfirmedEmailUserId)]
    public async Task ChallengeAsync_Should_ReportProviderNotAvailable_When_IdentityHasNoValidProvider(string userId)
    {
        // Arrange
        var user = userId == NoEmailUserId ? NoEmailUser : UnconfirmedEmailUser;

        // Act
        var result = await MfaService.ChallengeAsync(user, StoreId);

        // Assert
        Assert.Equal(MfaOutcome.ProviderNotAvailable, result.Outcome);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task ChallengeAsync_Should_ReportProviderNotAvailable_When_ProviderIsUnknown()
    {
        // Act
        var result = await MfaService.ChallengeAsync(ActiveUser, StoreId, "Sms");

        // Assert
        Assert.Equal(MfaOutcome.ProviderNotAvailable, result.Outcome);
    }

    [Fact]
    public async Task SendCodeAsync_Should_SendCodeWithoutToken()
    {
        // Act
        var result = await MfaService.SendCodeAsync(BackofficeUser);

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
        Assert.Null(result.TwoFactorToken);
        Assert.Equal(EmailProvider, Assert.Single(result.Providers).Name);
        UserManager.Verify(x => x.GenerateTwoFactorTokenAsync(BackofficeUser, EmailProvider), Times.Once);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Once);
    }

    [Fact]
    public async Task SendCodeAsync_Should_NotSendCode_When_AccountIsLocked()
    {
        // Act
        var result = await MfaService.SendCodeAsync(LockedUser);

        // Assert
        Assert.Equal(MfaOutcome.AccountLocked, result.Outcome);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task ResendCodeAsync_Should_SendNewCodeAndIssueNewToken()
    {
        // Arrange
        var twoFactorToken = CreateTwoFactorToken();

        // Act
        var result = await MfaService.ResendCodeAsync(twoFactorToken);

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
        Assert.NotEqual(twoFactorToken, result.TwoFactorToken);
        Assert.Equal(ActiveUserId, TwoFactorTokenService.ReadToken(result.TwoFactorToken).UserId);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Once);
    }

    [Fact]
    public async Task ResendCodeAsync_Should_RejectMalformedToken()
    {
        // Act
        var result = await MfaService.ResendCodeAsync("not-a-token");

        // Assert
        Assert.Equal(MfaOutcome.InvalidTwoFactorToken, result.Outcome);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task ResendCodeAsync_Should_RejectToken_When_UserNoLongerExists()
    {
        // Act
        var result = await MfaService.ResendCodeAsync(CreateTwoFactorToken(userId: UnknownUserId));

        // Assert
        Assert.Equal(MfaOutcome.InvalidTwoFactorToken, result.Outcome);
    }

    [Fact]
    public async Task ResendCodeAsync_Should_RejectToken_When_SecurityStampChanged()
    {
        // Act
        var result = await MfaService.ResendCodeAsync(CreateTwoFactorToken(securityStamp: "old-stamp"));

        // Assert
        Assert.Equal(MfaOutcome.InvalidTwoFactorToken, result.Outcome);
    }

    [Fact]
    public async Task ResendCodeAsync_Should_NotSendCode_When_AccountIsLocked()
    {
        // Act
        var result = await MfaService.ResendCodeAsync(CreateTwoFactorToken(userId: LockedUserId));

        // Assert
        Assert.Equal(MfaOutcome.AccountLocked, result.Outcome);
        Assert.Same(LockedUser, result.User);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_Succeed_When_CodeIsValid()
    {
        // Act
        var result = await MfaService.VerifyCodeAsync(CreateTwoFactorToken(), ValidCode, StoreId);

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
        Assert.Same(ActiveUser, result.User);
        UserManager.Verify(x => x.AccessFailedAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_RejectToken_When_TheUserSignedInAfterItWasIssued()
    {
        // Arrange
        var twoFactorToken = CreateTwoFactorToken();
        ActiveUser.LastLoginDate = DateTime.UtcNow.AddSeconds(1);

        // Act
        var result = await MfaService.VerifyCodeAsync(twoFactorToken, ValidCode, StoreId);

        // Assert
        Assert.Equal(MfaOutcome.InvalidTwoFactorToken, result.Outcome);
        Assert.Equal(ActiveUserId, result.User?.Id);
        UserManager.Verify(x => x.VerifyTwoFactorTokenAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_RejectTokenWithTheUser_When_SecurityStampChanged()
    {
        // Act
        var result = await MfaService.VerifyCodeAsync(CreateTwoFactorToken(securityStamp: "old-stamp"), ValidCode, StoreId);

        // Assert
        Assert.Equal(MfaOutcome.InvalidTwoFactorToken, result.Outcome);
        Assert.Equal(ActiveUserId, result.User?.Id);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_RejectTokenWithoutUser_When_UserNoLongerExists()
    {
        // Act
        var result = await MfaService.VerifyCodeAsync(CreateTwoFactorToken(userId: UnknownUserId), ValidCode, StoreId);

        // Assert
        Assert.Equal(MfaOutcome.InvalidTwoFactorToken, result.Outcome);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_ReportLockout_When_TheWrongCodeLocksTheAccount()
    {
        // Arrange
        UserManager
            .Setup(x => x.AccessFailedAsync(ActiveUser))
            .Callback((ApplicationUser user) => user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        var result = await MfaService.VerifyCodeAsync(CreateTwoFactorToken(), InvalidCode, StoreId);

        // Assert
        Assert.Equal(MfaOutcome.AccountLocked, result.Outcome);
        Assert.Same(ActiveUser, result.User);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_IgnoreTheCaseOfTheStoreId()
    {
        // Act
        var result = await MfaService.VerifyCodeAsync(CreateTwoFactorToken(), ValidCode, StoreId.ToUpperInvariant());

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_Succeed_When_TheSignInHasNoStore()
    {
        // Act
        var result = await MfaService.VerifyCodeAsync(CreateTwoFactorToken(BackofficeUserId, storeId: null), ValidCode, storeId: null);

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_CountFailure_When_CodeIsWrong()
    {
        // Act
        var result = await MfaService.VerifyCodeAsync(CreateTwoFactorToken(), InvalidCode, StoreId);

        // Assert
        Assert.Equal(MfaOutcome.InvalidCode, result.Outcome);
        UserManager.Verify(x => x.AccessFailedAsync(ActiveUser), Times.Once);
    }

    [Theory]
    [InlineData(MfaDisabledStoreId, StoreId)]
    [InlineData(null, StoreId)]
    [InlineData(StoreId, null)]
    public async Task VerifyCodeAsync_Should_RejectToken_When_IssuedForAnotherSignIn(string requestStoreId, string ticketStoreId)
    {
        // Act
        var result = await MfaService.VerifyCodeAsync(CreateTwoFactorToken(storeId: ticketStoreId), ValidCode, requestStoreId);

        // Assert
        Assert.Equal(MfaOutcome.InvalidTwoFactorToken, result.Outcome);
        Assert.Equal(ActiveUserId, result.User?.Id);
        UserManager.Verify(x => x.VerifyTwoFactorTokenAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_RejectCode_When_ProviderItWasSentWithIsNotAvailable()
    {
        // Act
        var result = await MfaService.VerifyCodeAsync(CreateTwoFactorToken(provider: "Sms"), ValidCode, StoreId);

        // Assert
        Assert.Equal(MfaOutcome.ProviderNotAvailable, result.Outcome);
        UserManager.Verify(x => x.VerifyTwoFactorTokenAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_NotCheckCode_When_AccountIsLocked()
    {
        // Act
        var result = await MfaService.VerifyCodeAsync(CreateTwoFactorToken(userId: LockedUserId), ValidCode, StoreId);

        // Assert
        Assert.Equal(MfaOutcome.AccountLocked, result.Outcome);
        UserManager.Verify(x => x.VerifyTwoFactorTokenAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_RejectCode_When_LockoutIsDisabled()
    {
        // Act
        var result = await MfaService.VerifyCodeAsync(CreateTwoFactorToken(userId: LockoutDisabledUserId), ValidCode, StoreId);

        // Assert
        Assert.Equal(MfaOutcome.LockoutDisabled, result.Outcome);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_RefuseTheToken_When_TheWrongCodeLimitIsReached()
    {
        // Arrange
        var twoFactorToken = CreateTwoFactorToken();
        for (var i = 0; i < MfaOptions.MaxCodeAttempts; i++)
        {
            Assert.Equal(MfaOutcome.InvalidCode, (await MfaService.VerifyCodeAsync(twoFactorToken, InvalidCode, StoreId)).Outcome);
        }

        // Act
        var result = await MfaService.VerifyCodeAsync(twoFactorToken, ValidCode, StoreId);

        // Assert
        Assert.Equal(MfaOutcome.CodeAttemptsExceeded, result.Outcome);
        Assert.Equal(ActiveUserId, result.User.Id);
        Assert.Equal(EmailProvider, result.Provider);
        UserManager.Verify(x => x.VerifyTwoFactorTokenAsync(It.IsAny<ApplicationUser>(), EmailProvider, It.IsAny<string>()), Times.Exactly(MfaOptions.MaxCodeAttempts));
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_CountWrongCodesAcrossResends()
    {
        // Arrange
        var twoFactorToken = CreateTwoFactorToken();
        for (var i = 0; i < MfaOptions.MaxCodeAttempts - 1; i++)
        {
            await MfaService.VerifyCodeAsync(twoFactorToken, InvalidCode, StoreId);
        }

        var resent = await MfaService.ResendCodeAsync(twoFactorToken);
        await MfaService.VerifyCodeAsync(resent.TwoFactorToken, InvalidCode, StoreId);

        // Act
        var result = await MfaService.VerifyCodeAsync(resent.TwoFactorToken, ValidCode, StoreId);

        // Assert
        Assert.Equal(MfaOutcome.CodeAttemptsExceeded, result.Outcome);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_RefuseTheToken_When_ItWasAlreadyRedeemed()
    {
        // Arrange
        var twoFactorToken = CreateTwoFactorToken();
        Assert.Equal(MfaOutcome.Success, (await MfaService.VerifyCodeAsync(twoFactorToken, ValidCode, StoreId)).Outcome);

        // Act
        var result = await MfaService.VerifyCodeAsync(twoFactorToken, ValidCode, StoreId);

        // Assert
        Assert.Equal(MfaOutcome.InvalidTwoFactorToken, result.Outcome);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_RedeemTheTokenOnce_When_TwoRequestsUseItAtTheSameTime()
    {
        // Arrange
        var twoFactorToken = CreateTwoFactorToken();

        // Act
        var results = await Task.WhenAll(
            Task.Run(() => MfaService.VerifyCodeAsync(twoFactorToken, ValidCode, StoreId)),
            Task.Run(() => MfaService.VerifyCodeAsync(twoFactorToken, ValidCode, StoreId)));

        // Assert
        Assert.Single(results, x => x.Outcome == MfaOutcome.Success);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_RevokeTheToken_When_TheFailedAttemptIsNotRecorded()
    {
        // Arrange
        var twoFactorToken = CreateTwoFactorToken();
        UserManager
            .Setup(x => x.AccessFailedAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Code = "concurrency_failure" }));

        // Act
        var result = await MfaService.VerifyCodeAsync(twoFactorToken, InvalidCode, StoreId);
        var retry = await MfaService.VerifyCodeAsync(twoFactorToken, ValidCode, StoreId);

        // Assert
        Assert.Equal(MfaOutcome.FailedAttemptNotRecorded, result.Outcome);
        Assert.Equal(MfaOutcome.CodeAttemptsExceeded, retry.Outcome);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_RejectTokenWithTheUser_When_TokenExpired()
    {
        // Arrange
        var twoFactorToken = CreateTwoFactorToken();
        TimeProvider.Advance(MfaOptions.TwoFactorTokenLifetime);

        // Act
        var result = await MfaService.VerifyCodeAsync(twoFactorToken, ValidCode, StoreId);

        // Assert
        Assert.Equal(MfaOutcome.InvalidTwoFactorToken, result.Outcome);
        Assert.Equal(ActiveUserId, result.User.Id);
    }

    [Fact]
    public async Task ChallengeAsync_Should_ReportProviderNotAvailableAndKeepTheResendSlot_When_TheNotificationIsNotActive()
    {
        // Arrange
        IsNotificationActive = false;

        // Act
        var result = await MfaService.ChallengeAsync(ActiveUser, StoreId);
        IsNotificationActive = true;
        var retry = await MfaService.ChallengeAsync(ActiveUser, StoreId);

        // Assert
        Assert.Equal(MfaOutcome.ProviderNotAvailable, result.Outcome);
        Assert.Null(result.TwoFactorToken);
        Assert.Equal(MfaOutcome.Success, retry.Outcome);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Once);
    }

    [Fact]
    public async Task ChallengeAsync_Should_SendTheStoreTemplateFromTheStoreAddress_When_TheSignInNamesAStore()
    {
        // Act
        await MfaService.ChallengeAsync(ActiveUser, StoreId);

        // Assert
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.Is<Notification>(n =>
            n.TenantIdentity.Id == StoreId &&
            ((MfaSignInEmailNotification)n).From == StoreSender)), Times.Once);
        MemberService.Verify(x => x.GetByIdAsync(ContactId, "Default", null), Times.Once);
    }

    [Fact]
    public async Task SendCodeAsync_Should_SendTheGlobalTemplateFromTheDefaultAddress_When_TheSignInHasNoStore()
    {
        // Act
        await MfaService.SendCodeAsync(BackofficeUser);

        // Assert
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.Is<Notification>(n =>
            (n.TenantIdentity == null || n.TenantIdentity.Id == null) &&
            ((MfaSignInEmailNotification)n).From == null)), Times.Once);
    }

    [Fact]
    public async Task ResendCodeAsync_Should_RefuseTheToken_When_TheWrongCodeLimitIsReached()
    {
        // Arrange
        var twoFactorToken = CreateTwoFactorToken();
        for (var i = 0; i < MfaOptions.MaxCodeAttempts; i++)
        {
            await MfaService.VerifyCodeAsync(twoFactorToken, InvalidCode, StoreId);
        }

        // Act
        var result = await MfaService.ResendCodeAsync(twoFactorToken);

        // Assert
        Assert.Equal(MfaOutcome.CodeAttemptsExceeded, result.Outcome);
        Assert.Null(result.TwoFactorToken);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task ResendCodeAsync_Should_RefuseTheToken_When_ItWasRedeemed()
    {
        // Arrange
        var twoFactorToken = CreateTwoFactorToken();
        await MfaService.VerifyCodeAsync(twoFactorToken, ValidCode, StoreId);

        // Act
        var result = await MfaService.ResendCodeAsync(twoFactorToken);

        // Assert
        Assert.Equal(MfaOutcome.InvalidTwoFactorToken, result.Outcome);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task SendCodeAsync_Should_FreeTheResendSlot_When_SendingFails()
    {
        // Arrange
        NotificationSender
            .SetupSequence(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()))
            .ThrowsAsync(new InvalidOperationException("Delivery failed"))
            .Returns(Task.CompletedTask);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => MfaService.SendCodeAsync(BackofficeUser));
        var retry = await MfaService.SendCodeAsync(BackofficeUser);

        // Assert
        Assert.Equal(MfaOutcome.Success, retry.Outcome);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ChallengeAsync_Should_IssueTokenWithoutSendingCode_When_TheProviderSendsNothing()
    {
        // Arrange
        var authenticator = new Mock<IMfaProvider>();
        authenticator.SetupGet(x => x.Name).Returns(EmailProvider);
        authenticator.SetupGet(x => x.SendsCode).Returns(false);
        var mfaService = new MfaService(UserManager.Object, StoreService.Object, TwoFactorTokenService, TicketStore, [authenticator.Object],
            CodeDeliveryLimiter, Options.Create(MfaOptions), TimeProvider);

        // Act
        var result = await mfaService.ChallengeAsync(ActiveUser, StoreId);

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
        Assert.NotNull(result.TwoFactorToken);
        Assert.Null(result.ResendSecondsRemaining);
        authenticator.Verify(x => x.SendCodeAsync(It.IsAny<Store>(), It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        UserManager.Verify(x => x.GenerateTwoFactorTokenAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ChallengeAsync_Should_ShareTheResendLimitWithTheBackofficeCode()
    {
        // Arrange
        await MfaService.SendCodeAsync(ActiveUser);

        // Act
        var result = await MfaService.ChallengeAsync(ActiveUser, StoreId);

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Once);
    }
}
