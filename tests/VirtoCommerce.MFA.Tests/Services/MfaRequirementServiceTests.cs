using System.Threading.Tasks;
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
}
