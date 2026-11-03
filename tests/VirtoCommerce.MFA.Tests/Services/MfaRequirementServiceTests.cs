using System.Threading.Tasks;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.Platform.Core.Security;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public class MfaRequirementServiceTests : MfaTestsBase
{
    [Theory]
    [InlineData(null, false, ActiveUserId, false)]
    [InlineData(null, true, ActiveUserId, true)]
    [InlineData(null, false, EnabledStoreCustomerId, true)]
    [InlineData(null, false, DisabledStoreCustomerId, false)]
    [InlineData(null, true, DisabledStoreCustomerId, true)]
    [InlineData(UnknownStoreId, false, ActiveUserId, false)]
    [InlineData(UnknownStoreId, true, ActiveUserId, true)]
    [InlineData(UnknownStoreId, false, EnabledStoreCustomerId, true)]
    [InlineData(StoreId, false, ActiveUserId, false)]
    [InlineData(StoreId, true, ActiveUserId, true)]
    [InlineData(StoreId, false, EnabledStoreCustomerId, true)]
    [InlineData(MfaEnabledStoreId, false, ActiveUserId, true)]
    [InlineData(MfaEnabledStoreId, false, BackofficeUserId, true)]
    [InlineData(MfaEnabledStoreId, false, DisabledStoreCustomerId, true)]
    [InlineData(MfaDisabledStoreId, true, DisabledStoreCustomerId, false)]
    [InlineData(MfaDisabledStoreId, false, DisabledStoreAdministratorId, false)]
    [InlineData(MfaDisabledStoreId, true, DisabledStoreAdministratorId, true)]
    [InlineData(MfaDisabledStoreId, false, DisabledStoreManagerId, false)]
    [InlineData(MfaDisabledStoreId, true, DisabledStoreManagerId, true)]
    [InlineData(MfaDisabledStoreId, false, EnabledStoreCustomerId, true)]
    [InlineData(MfaDisabledStoreId, true, EnabledStoreCustomerId, true)]
    [InlineData(MfaDisabledStoreId, false, BackofficeUserId, false)]
    [InlineData(MfaDisabledStoreId, true, BackofficeUserId, true)]
    public async Task IsRequiredAsync_Should_FollowTheNamedStoreAndTheUserStore(string storeId, bool isGloballyEnabled, string userId, bool expected)
    {
        // Arrange
        IsGloballyEnabled = isGloballyEnabled;
        var user = await UserManager.Object.FindByIdAsync(userId);

        // Act
        var isRequired = await RequirementService.IsRequiredAsync(user, storeId);

        // Assert
        Assert.Equal(expected, isRequired);
    }

    [Theory]
    [InlineData(null, false, false)]
    [InlineData(null, true, true)]
    [InlineData(StoreId, false, false)]
    [InlineData(MfaEnabledStoreId, false, true)]
    [InlineData(MfaDisabledStoreId, false, false)]
    [InlineData(MfaDisabledStoreId, true, true)]
    public async Task IsRequiredAsync_Should_FollowTheNamedStore_When_TheUserHasNoStore(string storeId, bool isGloballyEnabled, bool expected)
    {
        // Arrange
        IsGloballyEnabled = isGloballyEnabled;
        var user = new ApplicationUser { Id = "no-store-user" };

        // Act
        var isRequired = await RequirementService.IsRequiredAsync(user, storeId);

        // Assert
        Assert.Equal(expected, isRequired);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("order:read", true)]
    public async Task IsRequiredAsync_Should_RequireTheCodeInADisabledStore_When_ACustomerOfTheStoreHasABackofficePermission(string permission, bool expected)
    {
        // Arrange
        IsGloballyEnabled = true;
        var user = await UserManager.Object.FindByIdAsync(DisabledStoreCustomerId);
        if (permission != null)
        {
            user.Roles = [new Role { Name = "Order manager", Permissions = [new Permission { Name = permission }] }];
        }

        // Act
        var isRequired = await RequirementService.IsRequiredAsync(user, MfaDisabledStoreId);

        // Assert
        Assert.Equal(expected, isRequired);
    }

    [Fact]
    public async Task IsRequiredAsync_Should_FollowTheAdminUIAccessOfThePlatform_When_ItAllowsOnlySomeAccountTypes()
    {
        // Arrange
        IsGloballyEnabled = true;
        PlatformUIOptions.Access.AllowedAccountTypes = [nameof(UserType.Administrator), nameof(UserType.Manager)];
        var user = await UserManager.Object.FindByIdAsync(DisabledStoreCustomerId);
        user.Roles = [new Role { Name = "Order manager", Permissions = [new Permission { Name = "order:read" }] }];

        // Act
        var isRequired = await RequirementService.IsRequiredAsync(user, MfaDisabledStoreId);

        // Assert
        Assert.False(isRequired);
    }

    [Theory]
    [InlineData(EnabledStoreCustomerId, StoreId, false, true, true, false)]
    [InlineData(EnabledStoreCustomerId, MfaEnabledStoreId, false, true, false, false)]
    [InlineData(DisabledStoreManagerId, MfaDisabledStoreId, true, true, false, true)]
    [InlineData(DisabledStoreManagerId, MfaDisabledStoreId, false, false, false, true)]
    [InlineData(DisabledStoreCustomerId, MfaDisabledStoreId, true, false, false, false)]
    public async Task GetRequirementAsync_Should_TellWhetherTheUserStoreOrTheSkippedDisabledModeDecided(
        string userId, string storeId, bool isGloballyEnabled, bool expectedRequired, bool expectedByUserStore, bool expectedDisabledModeSkipped)
    {
        // Arrange
        IsGloballyEnabled = isGloballyEnabled;
        var user = await UserManager.Object.FindByIdAsync(userId);

        // Act
        var requirement = await RequirementService.GetRequirementAsync(user, storeId);

        // Assert
        Assert.Equal(expectedRequired, requirement.IsRequired);
        Assert.Equal(expectedByUserStore, requirement.IsRequiredByUserStore);
        Assert.Equal(expectedDisabledModeSkipped, requirement.IsDisabledModeSkipped);
    }

    [Fact]
    public async Task GetRequirementAsync_Should_UseTheGivenUserModeInsteadOfTheSavedOne()
    {
        // Arrange
        var user = await UserManager.Object.FindByIdAsync(ActiveUserId);

        // Act
        var saved = await RequirementService.GetRequirementAsync(user, CustomerChoiceStoreId);
        var previewed = await RequirementService.GetRequirementAsync(user, CustomerChoiceStoreId, ModuleConstants.Modes.Enabled);

        // Assert
        Assert.False(saved.IsRequired);
        Assert.True(previewed.IsRequired);
        Assert.Equal(ModuleConstants.RequirementSources.User, previewed.Source);
        Assert.Equal(ModuleConstants.Modes.Off, await ModeService.GetUserModeAsync(ActiveUserId));
    }

    [Theory]
    [InlineData(CustomerChoiceStoreId, ModuleConstants.Modes.Enabled, null, ModuleConstants.RequirementSources.User)]
    [InlineData(CustomerChoiceStoreId, null, ModuleConstants.Modes.Enabled, ModuleConstants.RequirementSources.Organization)]
    [InlineData(AdminManagedStoreId, ModuleConstants.Modes.Required, null, ModuleConstants.RequirementSources.User)]
    [InlineData(AdminManagedStoreId, null, ModuleConstants.Modes.Required, ModuleConstants.RequirementSources.Organization)]
    public async Task GetRequirementAsync_Should_ApplyTheModesOfTheUserStore_When_TheRequestNamesAStoreWithoutMfa(
        string userStoreId, string userMode, string organizationMode, string expectedSource)
    {
        // Arrange
        if (userMode != null)
        {
            await ModeService.SetUserModeAsync(ActiveUserId, userMode);
        }

        if (organizationMode != null)
        {
            await ModeService.SetOrganizationModeAsync(OrganizationId, organizationMode);
        }

        var user = await UserManager.Object.FindByIdAsync(ActiveUserId);
        user.StoreId = userStoreId;

        // Act
        var requirement = await RequirementService.GetRequirementAsync(user, MfaDisabledStoreId);

        // Assert
        Assert.True(requirement.IsRequired);
        Assert.Equal(expectedSource, requirement.Source);
    }

    [Theory]
    [InlineData(false, BackofficeUserId, false)]
    [InlineData(true, BackofficeUserId, true)]
    [InlineData(false, EnabledStoreCustomerId, false)]
    [InlineData(true, DisabledStoreAdministratorId, true)]
    [InlineData(true, DisabledStoreCustomerId, true)]
    public async Task IsRequiredForBackofficeAsync_Should_FollowOnlyTheGlobalSetting(bool isGloballyEnabled, string userId, bool expected)
    {
        // Arrange
        IsGloballyEnabled = isGloballyEnabled;
        var user = await UserManager.Object.FindByIdAsync(userId);

        // Act
        var isRequired = await RequirementService.IsRequiredForBackofficeAsync(user);

        // Assert
        Assert.Equal(expected, isRequired);
    }

    [Theory]
    [InlineData(AdminManagedStoreId, null, null, false, ModuleConstants.RequirementSources.None)]
    [InlineData(AdminManagedStoreId, ModuleConstants.Modes.Enabled, ModuleConstants.Modes.Enabled, false, ModuleConstants.RequirementSources.None)]
    [InlineData(AdminManagedStoreId, ModuleConstants.Modes.Required, null, true, ModuleConstants.RequirementSources.User)]
    [InlineData(AdminManagedStoreId, null, ModuleConstants.Modes.Required, true, ModuleConstants.RequirementSources.Organization)]
    [InlineData(CustomerChoiceStoreId, null, null, false, ModuleConstants.RequirementSources.None)]
    [InlineData(CustomerChoiceStoreId, ModuleConstants.Modes.Enabled, null, true, ModuleConstants.RequirementSources.User)]
    [InlineData(CustomerChoiceStoreId, ModuleConstants.Modes.Required, null, true, ModuleConstants.RequirementSources.User)]
    [InlineData(CustomerChoiceStoreId, null, ModuleConstants.Modes.Enabled, true, ModuleConstants.RequirementSources.Organization)]
    [InlineData(CustomerChoiceStoreId, ModuleConstants.Modes.Enabled, ModuleConstants.Modes.Required, true, ModuleConstants.RequirementSources.Organization)]
    [InlineData(MfaDisabledStoreId, ModuleConstants.Modes.Required, ModuleConstants.Modes.Required, false, ModuleConstants.RequirementSources.None)]
    [InlineData(MfaEnabledStoreId, null, null, true, ModuleConstants.RequirementSources.Store)]
    [InlineData(StoreId, ModuleConstants.Modes.Required, ModuleConstants.Modes.Required, false, ModuleConstants.RequirementSources.None)]
    public async Task GetRequirementAsync_Should_ApplyTheUserAndOrganizationModesByTheStoreMode(
        string storeId, string userMode, string organizationMode, bool expectedRequired, string expectedSource)
    {
        // Arrange
        if (userMode != null)
        {
            await ModeService.SetUserModeAsync(ActiveUserId, userMode);
        }

        if (organizationMode != null)
        {
            await ModeService.SetOrganizationModeAsync(OrganizationId, organizationMode);
        }

        // Act
        var requirement = await RequirementService.GetRequirementAsync(ActiveUser, storeId);

        // Assert
        Assert.Equal(expectedRequired, requirement.IsRequired);
        Assert.Equal(expectedSource, requirement.Source);
        Assert.Equal(expectedRequired, await RequirementService.IsRequiredAsync(ActiveUser, storeId));
    }
}
