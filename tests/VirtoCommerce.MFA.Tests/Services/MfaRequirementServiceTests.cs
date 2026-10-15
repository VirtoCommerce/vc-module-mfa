using System.Threading.Tasks;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public class MfaRequirementServiceTests : MfaTestsBase
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
    [InlineData(MfaDisabledStoreId, true, DisabledStoreManagerId, true)]
    public async Task IsRequiredAsync_Should_FollowTheGlobalAndTheStoreSettings(string storeId, bool isGloballyEnabled, string userId, bool expected)
    {
        // Arrange
        IsGloballyEnabled = isGloballyEnabled;
        var user = await UserManager.Object.FindByIdAsync(userId);

        // Act
        var isRequired = await RequirementService.IsRequiredAsync(user, storeId);

        // Assert
        Assert.Equal(expected, isRequired);
    }
}
