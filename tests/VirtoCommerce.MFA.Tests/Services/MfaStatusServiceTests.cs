using System.Linq;
using System.Threading.Tasks;
using Moq;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.MFA.Data.Services;
using VirtoCommerce.StoreModule.Core.Model.Search;
using VirtoCommerce.StoreModule.Core.Services;
using Xunit;
using Modes = VirtoCommerce.MFA.Core.ModuleConstants.Modes;
using Sources = VirtoCommerce.MFA.Core.ModuleConstants.RequirementSources;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public class MfaStatusServiceTests : MfaTestsBase
{
    private readonly IMfaStatusService _statusService;

    public MfaStatusServiceTests()
    {
        var storeSearchService = new Mock<IStoreSearchService>();

        storeSearchService
            .Setup(x => x.SearchAsync(It.IsAny<StoreSearchCriteria>(), It.IsAny<bool>()))
            .ReturnsAsync((StoreSearchCriteria criteria, bool _) => new StoreSearchResult
            {
                TotalCount = Stores.Length,
                Results = Stores.Skip(criteria.Skip).Take(criteria.Take).ToList(),
            });

        _statusService = new MfaStatusService(UserManager.Object, storeSearchService.Object, SettingsManager.Object, RequirementService, ModeService);
    }

    [Theory]
    [InlineData(StoreId, false, null, null, null, false, Sources.None)]
    [InlineData(StoreId, true, null, null, null, true, Sources.Global)]
    [InlineData(MfaEnabledStoreId, false, null, null, null, true, Sources.Store)]
    [InlineData(MfaDisabledStoreId, true, Modes.Required, Modes.Required, null, false, Sources.None)]
    [InlineData(AdminManagedStoreId, false, Modes.Enabled, null, null, false, Sources.None)]
    [InlineData(AdminManagedStoreId, false, Modes.Required, null, null, true, Sources.User)]
    [InlineData(AdminManagedStoreId, false, null, null, Modes.Required, true, Sources.User)]
    [InlineData(CustomerChoiceStoreId, false, null, null, null, false, Sources.None)]
    [InlineData(CustomerChoiceStoreId, false, null, null, Modes.Enabled, true, Sources.User)]
    [InlineData(CustomerChoiceStoreId, false, Modes.Enabled, null, Modes.Off, false, Sources.None)]
    [InlineData(CustomerChoiceStoreId, false, null, Modes.Enabled, Modes.Off, true, Sources.Organization)]
    public async Task GetUserStatusAsync_Should_ShowTheRequirementInEveryStoreForTheSavedOrPreviewedMode(
        string storeId, bool isGloballyEnabled, string userMode, string organizationMode, string previewMode, bool expectedRequired, string expectedSource)
    {
        // Arrange
        IsGloballyEnabled = isGloballyEnabled;
        await SetModesAsync(userMode, organizationMode);

        // Act
        var status = await _statusService.GetUserStatusAsync(ActiveUserId, previewMode);

        // Assert
        var store = Assert.Single(status.Stores, x => x.StoreId == storeId);
        Assert.Equal(expectedRequired, store.IsRequired);
        Assert.Equal(expectedSource, store.RequiredBy);
        Assert.Equal(Stores.Length, status.Stores.Count);
        Assert.Equal(isGloballyEnabled, status.IsGloballyEnabled);
        Assert.Equal(userMode ?? Modes.Off, await ModeService.GetUserModeAsync(ActiveUserId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetUserStatusAsync_Should_ShowTheBackofficeSignInOnlyForABackofficeUser(bool isGloballyEnabled)
    {
        // Arrange
        IsGloballyEnabled = isGloballyEnabled;

        // Act
        var backofficeStatus = await _statusService.GetUserStatusAsync(BackofficeUserId);
        var customerStatus = await _statusService.GetUserStatusAsync(ActiveUserId);

        // Assert
        Assert.Equal(isGloballyEnabled, backofficeStatus.IsRequiredForBackoffice);
        Assert.Null(customerStatus.IsRequiredForBackoffice);
    }

    [Fact]
    public async Task GetUserStatusAsync_Should_ReturnNull_When_TheUserDoesNotExist()
    {
        // Act
        var status = await _statusService.GetUserStatusAsync(UnknownUserId);

        // Assert
        Assert.Null(status);
    }

    [Theory]
    [InlineData(AdminManagedStoreId, null, null, false, Sources.None)]
    [InlineData(AdminManagedStoreId, Modes.Enabled, null, false, Sources.None)]
    [InlineData(AdminManagedStoreId, Modes.Required, null, true, Sources.Organization)]
    [InlineData(CustomerChoiceStoreId, Modes.Enabled, null, true, Sources.Organization)]
    [InlineData(CustomerChoiceStoreId, null, Modes.Enabled, true, Sources.Organization)]
    [InlineData(CustomerChoiceStoreId, Modes.Required, Modes.Off, false, Sources.None)]
    [InlineData(MfaEnabledStoreId, null, null, true, Sources.Store)]
    [InlineData(MfaDisabledStoreId, Modes.Required, null, false, Sources.None)]
    public async Task GetOrganizationStatusAsync_Should_ShowWhatTheOrganizationRequiresInEveryStore(
        string storeId, string organizationMode, string previewMode, bool expectedRequired, string expectedSource)
    {
        // Arrange
        await SetModesAsync(userMode: Modes.Required, organizationMode);

        // Act
        var status = await _statusService.GetOrganizationStatusAsync(OrganizationId, previewMode);

        // Assert
        var store = Assert.Single(status.Stores, x => x.StoreId == storeId);
        Assert.Equal(expectedRequired, store.IsRequired);
        Assert.Equal(expectedSource, store.RequiredBy);
        Assert.Null(status.IsRequiredForBackoffice);
    }

    private async Task SetModesAsync(string userMode, string organizationMode)
    {
        if (userMode != null)
        {
            await ModeService.SetUserModeAsync(ActiveUserId, userMode);
        }

        if (organizationMode != null)
        {
            await ModeService.SetOrganizationModeAsync(OrganizationId, organizationMode);
        }
    }
}
