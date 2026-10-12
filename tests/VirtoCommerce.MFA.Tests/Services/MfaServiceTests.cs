using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Moq;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Notifications;
using VirtoCommerce.NotificationsModule.Core.Model;
using VirtoCommerce.Platform.Core.Security;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public class MfaServiceTests : MfaTestsBase
{
    [Theory]
    [InlineData(null, false, ActiveUserId, false)]
    [InlineData(null, true, ActiveUserId, true)]
    [InlineData(null, false, BackofficeUserId, false)]
    [InlineData(null, true, BackofficeUserId, true)]
    [InlineData(UnknownStoreId, true, ActiveUserId, true)]
    [InlineData(UnknownStoreId, false, ActiveUserId, false)]
    [InlineData(StoreId, false, ActiveUserId, false)]
    [InlineData(StoreId, true, ActiveUserId, true)]
    [InlineData(MfaEnabledStoreId, false, ActiveUserId, true)]
    [InlineData(MfaEnabledStoreId, false, BackofficeUserId, true)]
    [InlineData(null, false, EnabledStoreCustomerId, true)]
    [InlineData(StoreId, false, EnabledStoreCustomerId, true)]
    [InlineData(MfaDisabledStoreId, false, EnabledStoreCustomerId, true)]
    [InlineData(MfaDisabledStoreId, true, DisabledStoreCustomerId, false)]
    [InlineData(MfaDisabledStoreId, true, DisabledStoreAdministratorId, true)]
    [InlineData(null, true, DisabledStoreCustomerId, true)]
    [InlineData(MfaDisabledStoreId, true, ActiveUserId, true)]
    [InlineData(MfaDisabledStoreId, true, BackofficeUserId, true)]
    [InlineData(MfaDisabledStoreId, false, BackofficeUserId, false)]
    public async Task IsRequiredAsync_Should_FollowTheGlobalAndTheStoreSettings(string storeId, bool isGloballyEnabled, string userId, bool expected)
    {
        // Arrange
        IsGloballyEnabled = isGloballyEnabled;
        var user = await UserManager.Object.FindByIdAsync(userId);

        // Act
        var isRequired = await MfaService.IsRequiredAsync(user, storeId);

        // Assert
        Assert.Equal(expected, isRequired);
    }

    [Fact]
    public async Task ChallengeAsync_Should_SendCodeAndIssueToken()
    {
        // Act
        var result = await MfaService.ChallengeAsync(StoreId, ActiveUser);

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
        var challenge = await MfaService.ChallengeAsync(StoreId, ActiveUser);

        // Act
        var result = await MfaService.ResendCodeAsync(challenge.TwoFactorToken);

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
        Assert.Equal(
            TwoFactorTokenService.ReadToken(challenge.TwoFactorToken).IssuedAt,
            TwoFactorTokenService.ReadToken(result.TwoFactorToken).IssuedAt);
    }

    [Fact]
    public async Task ResendCodeAsync_Should_RejectToken_When_TheUserSignedInAfterItWasIssued()
    {
        // Arrange
        var challenge = await MfaService.ChallengeAsync(StoreId, ActiveUser);
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
        var challenge = await MfaService.ChallengeAsync(StoreId, ActiveUser);

        // Act
        var result = await MfaService.VerifyCodeAsync(StoreId, challenge.TwoFactorToken, ValidCode);

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task ChallengeAsync_Should_IssueTokenWithoutStore_When_TheSignInHasNoStore()
    {
        // Act
        var result = await MfaService.ChallengeAsync(storeId: null, BackofficeUser);

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
        Assert.Null(TwoFactorTokenService.ReadToken(result.TwoFactorToken).StoreId);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.Is<Notification>(n => n.LanguageCode == EmployeeLanguage)), Times.Once);
    }

    [Fact]
    public async Task ChallengeAsync_Should_SendCodeWithTheGlobalTemplate_When_TheStoreIsUnknown()
    {
        // Act
        var result = await MfaService.ChallengeAsync(UnknownStoreId, ActiveUser);
        var verifyResult = await MfaService.VerifyCodeAsync(UnknownStoreId, result.TwoFactorToken, ValidCode);

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
        var result = await MfaService.ChallengeAsync(StoreId, LockoutDisabledUser);

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
        var result = await MfaService.ChallengeAsync(StoreId, user);

        // Assert
        Assert.Equal(MfaOutcome.ProviderNotAvailable, result.Outcome);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task ChallengeAsync_Should_ReportProviderNotAvailable_When_ProviderIsUnknown()
    {
        // Act
        var result = await MfaService.ChallengeAsync(StoreId, ActiveUser, "Sms");

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
        var result = await MfaService.VerifyCodeAsync(StoreId, CreateTwoFactorToken(), ValidCode);

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
        var result = await MfaService.VerifyCodeAsync(StoreId, twoFactorToken, ValidCode);

        // Assert
        Assert.Equal(MfaOutcome.InvalidTwoFactorToken, result.Outcome);
        UserManager.Verify(x => x.VerifyTwoFactorTokenAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
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
        var result = await MfaService.VerifyCodeAsync(StoreId, CreateTwoFactorToken(), InvalidCode);

        // Assert
        Assert.Equal(MfaOutcome.AccountLocked, result.Outcome);
        Assert.Same(ActiveUser, result.User);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_IgnoreTheCaseOfTheStoreId()
    {
        // Act
        var result = await MfaService.VerifyCodeAsync(StoreId.ToUpperInvariant(), CreateTwoFactorToken(), ValidCode);

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_Succeed_When_TheSignInHasNoStore()
    {
        // Act
        var result = await MfaService.VerifyCodeAsync(storeId: null, CreateTwoFactorToken(BackofficeUserId, storeId: null), ValidCode);

        // Assert
        Assert.Equal(MfaOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_CountFailure_When_CodeIsWrong()
    {
        // Act
        var result = await MfaService.VerifyCodeAsync(StoreId, CreateTwoFactorToken(), InvalidCode);

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
        var result = await MfaService.VerifyCodeAsync(requestStoreId, CreateTwoFactorToken(storeId: ticketStoreId), ValidCode);

        // Assert
        Assert.Equal(MfaOutcome.InvalidTwoFactorToken, result.Outcome);
        UserManager.Verify(x => x.VerifyTwoFactorTokenAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_RejectCode_When_ProviderItWasSentWithIsNotAvailable()
    {
        // Act
        var result = await MfaService.VerifyCodeAsync(StoreId, CreateTwoFactorToken(provider: "Sms"), ValidCode);

        // Assert
        Assert.Equal(MfaOutcome.ProviderNotAvailable, result.Outcome);
        UserManager.Verify(x => x.VerifyTwoFactorTokenAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_NotCheckCode_When_AccountIsLocked()
    {
        // Act
        var result = await MfaService.VerifyCodeAsync(StoreId, CreateTwoFactorToken(userId: LockedUserId), ValidCode);

        // Assert
        Assert.Equal(MfaOutcome.AccountLocked, result.Outcome);
        UserManager.Verify(x => x.VerifyTwoFactorTokenAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_RejectCode_When_LockoutIsDisabled()
    {
        // Act
        var result = await MfaService.VerifyCodeAsync(StoreId, CreateTwoFactorToken(userId: LockoutDisabledUserId), ValidCode);

        // Assert
        Assert.Equal(MfaOutcome.LockoutDisabled, result.Outcome);
    }
}
