using System.Threading.Tasks;
using Moq;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Data.Services;
using VirtoCommerce.NotificationsModule.Core.Model;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public class MfaSelfServiceTests : MfaTestsBase
{
    private readonly MfaSelfService _selfService;

    public MfaSelfServiceTests()
    {
        _selfService = new MfaSelfService(MfaService, TwoFactorTokenService, RequirementService, ModeService);
    }

    [Fact]
    public async Task ChangeUserModeAsync_Should_TurnMfaOnWithTheCode()
    {
        // Arrange
        var challenge = await _selfService.SendChangeCodeAsync(ActiveUser, CustomerChoiceStoreId, organizationId: null, canManageOrganization: false);

        // Act
        var result = await _selfService.ChangeUserModeAsync(ActiveUser, CustomerChoiceStoreId, enabled: true, challenge.CodeResult.TwoFactorToken, CurrentCode(ActiveUser));

        // Assert
        Assert.Equal(MfaChangeOutcome.Success, result.Outcome);
        Assert.Equal(ModuleConstants.Modes.Enabled, result.Settings.UserMode);
        Assert.True(result.Settings.IsRequired);
        Assert.Equal(ModuleConstants.RequirementSources.User, result.Settings.RequiredBy);
        Assert.Equal(MaskedActiveUserEmail, result.Settings.Destination);
        Assert.True(await RequirementService.IsRequiredAsync(ActiveUser, CustomerChoiceStoreId));
    }

    [Fact]
    public async Task ChangeUserModeAsync_Should_TurnMfaOffWithTheCode()
    {
        // Arrange
        await ModeService.SetUserModeAsync(ActiveUserId, ModuleConstants.Modes.Enabled);
        var challenge = await _selfService.SendChangeCodeAsync(ActiveUser, CustomerChoiceStoreId, organizationId: null, canManageOrganization: false);

        // Act
        var result = await _selfService.ChangeUserModeAsync(ActiveUser, CustomerChoiceStoreId, enabled: false, challenge.CodeResult.TwoFactorToken, CurrentCode(ActiveUser));

        // Assert
        Assert.Equal(MfaChangeOutcome.Success, result.Outcome);
        Assert.Equal(ModuleConstants.Modes.Off, await ModeService.GetUserModeAsync(ActiveUserId));
        Assert.False(result.Settings.IsRequired);
    }

    [Theory]
    [InlineData(StoreId)]
    [InlineData(MfaEnabledStoreId)]
    [InlineData(MfaDisabledStoreId)]
    [InlineData(AdminManagedStoreId)]
    public async Task ChangeUserModeAsync_Should_Refuse_When_TheStoreDoesNotLetCustomersChoose(string storeId)
    {
        // Arrange
        var signIn = await MfaService.ChallengeAsync(ActiveUser, CustomerChoiceStoreId, purpose: ModuleConstants.Security.ChangeTicketPurpose);

        // Act
        var result = await _selfService.ChangeUserModeAsync(ActiveUser, storeId, enabled: true, signIn.TwoFactorToken, CurrentCode(ActiveUser));

        // Assert
        Assert.Equal(MfaChangeOutcome.SelfServiceNotAllowed, result.Outcome);
        Assert.Equal(ModuleConstants.Modes.Off, await ModeService.GetUserModeAsync(ActiveUserId));
    }

    [Theory]
    [InlineData(StoreId)]
    [InlineData(MfaEnabledStoreId)]
    [InlineData(MfaDisabledStoreId)]
    [InlineData(AdminManagedStoreId)]
    public async Task SendChangeCodeAsync_Should_SendNothing_When_TheStoreDoesNotLetCustomersChoose(string storeId)
    {
        // Act
        var result = await _selfService.SendChangeCodeAsync(ActiveUser, storeId, organizationId: null, canManageOrganization: false);

        // Assert
        Assert.Equal(MfaChangeOutcome.SelfServiceNotAllowed, result.Outcome);
        Assert.Null(result.CodeResult);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Never);

        // the resend slot of the sign-in stays free
        var signIn = await MfaService.ChallengeAsync(ActiveUser, storeId);
        Assert.False(string.IsNullOrEmpty(signIn.TwoFactorToken));
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Once);
    }

    [Theory]
    [InlineData(StoreId)]
    [InlineData(MfaEnabledStoreId)]
    [InlineData(MfaDisabledStoreId)]
    [InlineData(AdminManagedStoreId)]
    public async Task SendChangeCodeAsync_Should_RefuseTheStore_When_TheAdministratorRequiresMfaInAStoreThatDoesNotLetCustomersChoose(string storeId)
    {
        // Arrange
        await ModeService.SetUserModeAsync(ActiveUserId, ModuleConstants.Modes.Required);
        await ModeService.SetOrganizationModeAsync(OrganizationId, ModuleConstants.Modes.Required);

        // Act
        var userResult = await _selfService.SendChangeCodeAsync(ActiveUser, storeId, organizationId: null, canManageOrganization: false);
        var organizationResult = await _selfService.SendChangeCodeAsync(ActiveUser, storeId, OrganizationId, canManageOrganization: true);

        // Assert
        Assert.Equal(MfaChangeOutcome.SelfServiceNotAllowed, userResult.Outcome);
        Assert.Equal(MfaChangeOutcome.SelfServiceNotAllowed, organizationResult.Outcome);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task ChangeUserModeAsync_Should_Refuse_When_TheAdministratorRequiresMfa()
    {
        // Arrange
        await ModeService.SetUserModeAsync(ActiveUserId, ModuleConstants.Modes.Required);
        var challenge = await MfaService.ChallengeAsync(ActiveUser, CustomerChoiceStoreId, purpose: ModuleConstants.Security.ChangeTicketPurpose);

        // Act
        var result = await _selfService.ChangeUserModeAsync(ActiveUser, CustomerChoiceStoreId, enabled: false, challenge.TwoFactorToken, CurrentCode(ActiveUser));

        // Assert
        Assert.Equal(MfaChangeOutcome.LockedByAdministrator, result.Outcome);
        Assert.Equal(ModuleConstants.Modes.Required, await ModeService.GetUserModeAsync(ActiveUserId));
    }

    [Fact]
    public async Task SendChangeCodeAsync_Should_SendNothing_When_TheAdministratorRequiresMfa()
    {
        // Arrange
        await ModeService.SetUserModeAsync(ActiveUserId, ModuleConstants.Modes.Required);

        // Act
        var result = await _selfService.SendChangeCodeAsync(ActiveUser, CustomerChoiceStoreId, organizationId: null, canManageOrganization: false);

        // Assert
        Assert.Equal(MfaChangeOutcome.LockedByAdministrator, result.Outcome);
        Assert.Null(result.CodeResult);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task SendChangeCodeAsync_Should_SendNothing_When_TheAdministratorRequiresMfaForTheOrganization()
    {
        // Arrange
        await ModeService.SetOrganizationModeAsync(OrganizationId, ModuleConstants.Modes.Required);

        // Act
        var result = await _selfService.SendChangeCodeAsync(ActiveUser, CustomerChoiceStoreId, OrganizationId, canManageOrganization: true);

        // Assert
        Assert.Equal(MfaChangeOutcome.LockedByAdministrator, result.Outcome);
        Assert.Null(result.CodeResult);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task SendChangeCodeAsync_Should_SendNothing_WithoutThePermissionToManageTheOrganization()
    {
        // Act
        var result = await _selfService.SendChangeCodeAsync(ActiveUser, CustomerChoiceStoreId, OrganizationId, canManageOrganization: false);

        // Assert
        Assert.Equal(MfaChangeOutcome.OrganizationNotAllowed, result.Outcome);
        Assert.Null(result.CodeResult);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Never);
    }

    [Fact]
    public async Task SendChangeCodeAsync_Should_SendTheCode_ForTheOrganization_When_TheAdministratorRequiresMfaForTheUser()
    {
        // Arrange
        await ModeService.SetUserModeAsync(ActiveUserId, ModuleConstants.Modes.Required);

        // Act
        var result = await _selfService.SendChangeCodeAsync(ActiveUser, CustomerChoiceStoreId, OrganizationId, canManageOrganization: true);

        // Assert
        Assert.Equal(MfaChangeOutcome.Success, result.Outcome);
        Assert.False(string.IsNullOrEmpty(result.CodeResult.TwoFactorToken));
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Once);
    }

    [Fact]
    public async Task SendChangeCodeAsync_Should_SendNoSecondEmail_WithinTheResendInterval()
    {
        // Arrange
        var first = await _selfService.SendChangeCodeAsync(ActiveUser, CustomerChoiceStoreId, organizationId: null, canManageOrganization: false);

        // Act
        var second = await _selfService.SendChangeCodeAsync(ActiveUser, CustomerChoiceStoreId, organizationId: null, canManageOrganization: false);

        // Assert
        Assert.Equal(MfaChangeOutcome.Success, second.Outcome);
        Assert.NotEqual(first.CodeResult.TwoFactorToken, second.CodeResult.TwoFactorToken);
        Assert.True(second.CodeResult.ResendSecondsRemaining > 0);
        NotificationSender.Verify(x => x.ScheduleSendNotificationAsync(It.IsAny<Notification>()), Times.Once);

        // the code sent first confirms the change with the new token
        var result = await _selfService.ChangeUserModeAsync(ActiveUser, CustomerChoiceStoreId, enabled: true, second.CodeResult.TwoFactorToken, CurrentCode(ActiveUser));
        Assert.Equal(MfaChangeOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task ChangeUserModeAsync_Should_Refuse_AWrongCode()
    {
        // Arrange
        var challenge = await _selfService.SendChangeCodeAsync(ActiveUser, CustomerChoiceStoreId, organizationId: null, canManageOrganization: false);

        // Act
        var result = await _selfService.ChangeUserModeAsync(ActiveUser, CustomerChoiceStoreId, enabled: true, challenge.CodeResult.TwoFactorToken, InvalidCode);

        // Assert
        Assert.Equal(MfaChangeOutcome.CodeRefused, result.Outcome);
        Assert.Equal(MfaOutcome.InvalidCode, result.CodeResult.Outcome);
        Assert.Equal(ModuleConstants.Modes.Off, await ModeService.GetUserModeAsync(ActiveUserId));
    }

    [Fact]
    public async Task ChangeUserModeAsync_Should_Refuse_ASignInToken()
    {
        // Arrange
        var signInChallenge = await MfaService.ChallengeAsync(ActiveUser, CustomerChoiceStoreId);

        // Act
        var result = await _selfService.ChangeUserModeAsync(ActiveUser, CustomerChoiceStoreId, enabled: true, signInChallenge.TwoFactorToken, CurrentCode(ActiveUser));

        // Assert
        Assert.Equal(MfaChangeOutcome.CodeRefused, result.Outcome);
        Assert.Equal(MfaOutcome.InvalidTwoFactorToken, result.CodeResult.Outcome);
    }

    [Fact]
    public async Task VerifyCodeAsync_Should_Refuse_AChangeTokenAtSignIn()
    {
        // Arrange
        var challenge = await _selfService.SendChangeCodeAsync(ActiveUser, CustomerChoiceStoreId, organizationId: null, canManageOrganization: false);

        // Act
        var result = await MfaService.VerifyCodeAsync(challenge.CodeResult.TwoFactorToken, CurrentCode(ActiveUser), CustomerChoiceStoreId);

        // Assert
        Assert.Equal(MfaOutcome.InvalidTwoFactorToken, result.Outcome);
    }

    [Fact]
    public async Task ChangeUserModeAsync_Should_Refuse_TheTokenOfAnotherUser()
    {
        // Arrange
        var challenge = await _selfService.SendChangeCodeAsync(BackofficeUser, CustomerChoiceStoreId, organizationId: null, canManageOrganization: false);

        // Act
        var result = await _selfService.ChangeUserModeAsync(ActiveUser, CustomerChoiceStoreId, enabled: true, challenge.CodeResult.TwoFactorToken, CurrentCode(BackofficeUser));

        // Assert
        Assert.Equal(MfaChangeOutcome.CodeRefused, result.Outcome);
        Assert.Equal(MfaOutcome.InvalidTwoFactorToken, result.CodeResult.Outcome);
        Assert.Equal(ModuleConstants.Modes.Off, await ModeService.GetUserModeAsync(ActiveUserId));

        // The code of the other user is still valid for that user
        var otherUserResult = await _selfService.ChangeUserModeAsync(BackofficeUser, CustomerChoiceStoreId, enabled: true, challenge.CodeResult.TwoFactorToken, CurrentCode(BackofficeUser));
        Assert.Equal(MfaChangeOutcome.Success, otherUserResult.Outcome);
    }

    [Fact]
    public async Task ChangeOrganizationModeAsync_Should_RequireMfaForTheMembers()
    {
        // Arrange
        var challenge = await _selfService.SendChangeCodeAsync(ActiveUser, CustomerChoiceStoreId, OrganizationId, canManageOrganization: true);

        // Act
        var result = await _selfService.ChangeOrganizationModeAsync(ActiveUser, CustomerChoiceStoreId, OrganizationId, canManageOrganization: true,
            enabled: true, challenge.CodeResult.TwoFactorToken, CurrentCode(ActiveUser));

        // Assert
        Assert.Equal(MfaChangeOutcome.Success, result.Outcome);
        Assert.Equal(ModuleConstants.Modes.Enabled, result.Settings.OrganizationMode);
        Assert.Equal(ModuleConstants.RequirementSources.Organization, result.Settings.RequiredBy);
        Assert.False(await RequirementService.IsRequiredAsync(ActiveUser, AdminManagedStoreId));
    }

    [Fact]
    public async Task ChangeOrganizationModeAsync_Should_Refuse_WithoutThePermission()
    {
        // Act
        var result = await _selfService.ChangeOrganizationModeAsync(ActiveUser, CustomerChoiceStoreId, OrganizationId, canManageOrganization: false,
            enabled: true, twoFactorToken: "token", code: ValidCode);

        // Assert
        Assert.Equal(MfaChangeOutcome.OrganizationNotAllowed, result.Outcome);
        Assert.Equal(ModuleConstants.Modes.Off, await ModeService.GetOrganizationModeAsync(OrganizationId));
    }

    [Fact]
    public async Task GetSettingsAsync_Should_LockTheModesSetByTheAdministrator()
    {
        // Arrange
        await ModeService.SetUserModeAsync(ActiveUserId, ModuleConstants.Modes.Required);
        await ModeService.SetOrganizationModeAsync(OrganizationId, ModuleConstants.Modes.Required);

        // Act
        var settings = await _selfService.GetSettingsAsync(ActiveUser, CustomerChoiceStoreId, OrganizationId, canManageOrganization: true);

        // Assert
        Assert.Equal(ModuleConstants.StoreModes.CustomerChoice, settings.StoreMode);
        Assert.False(settings.CanChangeUserMode);
        Assert.False(settings.CanChangeOrganizationMode);
        Assert.True(settings.IsRequired);
        Assert.Equal(ModuleConstants.RequirementSources.Organization, settings.RequiredBy);
    }

    [Fact]
    public async Task GetSettingsAsync_Should_ReportAnUnavailableProvider()
    {
        // Act
        var settings = await _selfService.GetSettingsAsync(UnconfirmedEmailUser, CustomerChoiceStoreId, organizationId: null, canManageOrganization: false);

        // Assert
        Assert.False(settings.IsProviderAvailable);
        Assert.Null(settings.Destination);
        Assert.True(settings.CanChangeUserMode);
        Assert.Null(settings.OrganizationMode);
    }
}
