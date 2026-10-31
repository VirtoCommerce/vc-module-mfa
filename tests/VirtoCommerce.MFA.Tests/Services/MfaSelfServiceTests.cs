using System.Threading.Tasks;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Data.Services;
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
        var challenge = await _selfService.SendChangeCodeAsync(ActiveUser, CustomerChoiceStoreId);

        // Act
        var result = await _selfService.ChangeUserModeAsync(ActiveUser, CustomerChoiceStoreId, enabled: true, challenge.TwoFactorToken, CurrentCode(ActiveUser));

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
        var challenge = await _selfService.SendChangeCodeAsync(ActiveUser, CustomerChoiceStoreId);

        // Act
        var result = await _selfService.ChangeUserModeAsync(ActiveUser, CustomerChoiceStoreId, enabled: false, challenge.TwoFactorToken, CurrentCode(ActiveUser));

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
        var challenge = await _selfService.SendChangeCodeAsync(ActiveUser, storeId);

        // Act
        var result = await _selfService.ChangeUserModeAsync(ActiveUser, storeId, enabled: true, challenge.TwoFactorToken, CurrentCode(ActiveUser));

        // Assert
        Assert.Equal(MfaChangeOutcome.SelfServiceNotAllowed, result.Outcome);
        Assert.Equal(ModuleConstants.Modes.Off, await ModeService.GetUserModeAsync(ActiveUserId));
    }

    [Fact]
    public async Task ChangeUserModeAsync_Should_Refuse_When_TheAdministratorRequiresMfa()
    {
        // Arrange
        await ModeService.SetUserModeAsync(ActiveUserId, ModuleConstants.Modes.Required);
        var challenge = await _selfService.SendChangeCodeAsync(ActiveUser, CustomerChoiceStoreId);

        // Act
        var result = await _selfService.ChangeUserModeAsync(ActiveUser, CustomerChoiceStoreId, enabled: false, challenge.TwoFactorToken, CurrentCode(ActiveUser));

        // Assert
        Assert.Equal(MfaChangeOutcome.LockedByAdministrator, result.Outcome);
        Assert.Equal(ModuleConstants.Modes.Required, await ModeService.GetUserModeAsync(ActiveUserId));
    }

    [Fact]
    public async Task ChangeUserModeAsync_Should_Refuse_AWrongCode()
    {
        // Arrange
        var challenge = await _selfService.SendChangeCodeAsync(ActiveUser, CustomerChoiceStoreId);

        // Act
        var result = await _selfService.ChangeUserModeAsync(ActiveUser, CustomerChoiceStoreId, enabled: true, challenge.TwoFactorToken, InvalidCode);

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
        var challenge = await _selfService.SendChangeCodeAsync(ActiveUser, CustomerChoiceStoreId);

        // Act
        var result = await MfaService.VerifyCodeAsync(challenge.TwoFactorToken, CurrentCode(ActiveUser), CustomerChoiceStoreId);

        // Assert
        Assert.Equal(MfaOutcome.InvalidTwoFactorToken, result.Outcome);
    }

    [Fact]
    public async Task ChangeUserModeAsync_Should_Refuse_TheTokenOfAnotherUser()
    {
        // Arrange
        var challenge = await _selfService.SendChangeCodeAsync(BackofficeUser, CustomerChoiceStoreId);

        // Act
        var result = await _selfService.ChangeUserModeAsync(ActiveUser, CustomerChoiceStoreId, enabled: true, challenge.TwoFactorToken, CurrentCode(BackofficeUser));

        // Assert
        Assert.Equal(MfaChangeOutcome.CodeRefused, result.Outcome);
        Assert.Equal(MfaOutcome.InvalidTwoFactorToken, result.CodeResult.Outcome);
        Assert.Equal(ModuleConstants.Modes.Off, await ModeService.GetUserModeAsync(ActiveUserId));

        // The code of the other user is still valid for that user
        var otherUserResult = await _selfService.ChangeUserModeAsync(BackofficeUser, CustomerChoiceStoreId, enabled: true, challenge.TwoFactorToken, CurrentCode(BackofficeUser));
        Assert.Equal(MfaChangeOutcome.Success, otherUserResult.Outcome);
    }

    [Fact]
    public async Task ChangeOrganizationModeAsync_Should_RequireMfaForTheMembers()
    {
        // Arrange
        var challenge = await _selfService.SendChangeCodeAsync(ActiveUser, CustomerChoiceStoreId);

        // Act
        var result = await _selfService.ChangeOrganizationModeAsync(ActiveUser, CustomerChoiceStoreId, OrganizationId, canManageOrganization: true,
            enabled: true, challenge.TwoFactorToken, CurrentCode(ActiveUser));

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
