using System.Threading.Tasks;
using VirtoCommerce.MFA.Core;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public class MfaRequirementServiceTests : MfaTestsBase
{
    [Theory]
    [InlineData(null, false, ActiveUserId, false)]
    [InlineData(null, true, ActiveUserId, true)]
    [InlineData(null, false, EnabledStoreCustomerId, false)]
    [InlineData(null, true, DisabledStoreCustomerId, true)]
    [InlineData(UnknownStoreId, false, ActiveUserId, false)]
    [InlineData(UnknownStoreId, true, ActiveUserId, true)]
    [InlineData(StoreId, false, ActiveUserId, false)]
    [InlineData(StoreId, true, ActiveUserId, true)]
    [InlineData(StoreId, false, EnabledStoreCustomerId, false)]
    [InlineData(MfaEnabledStoreId, false, ActiveUserId, true)]
    [InlineData(MfaEnabledStoreId, false, BackofficeUserId, true)]
    [InlineData(MfaDisabledStoreId, true, DisabledStoreCustomerId, false)]
    [InlineData(MfaDisabledStoreId, true, DisabledStoreAdministratorId, false)]
    [InlineData(MfaDisabledStoreId, true, DisabledStoreManagerId, false)]
    [InlineData(MfaDisabledStoreId, true, EnabledStoreCustomerId, false)]
    [InlineData(MfaDisabledStoreId, true, BackofficeUserId, false)]
    public async Task IsRequiredAsync_Should_FollowTheNamedStoreOrTheGlobalSetting(string storeId, bool isGloballyEnabled, string userId, bool expected)
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
