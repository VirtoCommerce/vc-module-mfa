using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Moq;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.MFA.Data.Services;
using VirtoCommerce.Platform.Core.Security;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public class MfaTwoFactorPolicyTests
{
    private const string StoreId = "B2B-store";

    private readonly ApplicationUser _user = new() { Id = "user-1" };
    private readonly Mock<IMfaRequirementService> _requirementService = new();
    private readonly MfaOptions _options = new();

    [Fact]
    public async Task IsTwoFactorRequiredAsync_Should_UseTheStoreOfTheTokenRequest()
    {
        // Arrange
        var context = new TwoFactorPolicyContext { User = _user, GrantType = "password" };
        context.Parameters[ModuleConstants.Security.Parameters.StoreId] = StoreId;
        _requirementService.Setup(x => x.IsRequiredAsync(_user, StoreId)).ReturnsAsync(true);

        // Act
        var isRequired = await CreatePolicy().IsTwoFactorRequiredAsync(context);

        // Assert
        Assert.True(isRequired);
    }

    [Fact]
    public async Task IsTwoFactorRequiredAsync_Should_UseNoStore_When_TheSignInIsNotATokenRequest()
    {
        // Arrange
        _requirementService.Setup(x => x.IsRequiredAsync(_user, null)).ReturnsAsync(true);

        // Act
        var isRequired = await CreatePolicy().IsTwoFactorRequiredAsync(new TwoFactorPolicyContext { User = _user });

        // Assert
        Assert.True(isRequired);
        _requirementService.Verify(x => x.IsRequiredAsync(_user, null), Times.Once);
    }

    [Fact]
    public async Task IsTwoFactorRequiredAsync_Should_RequireNothing_When_EnforcementIsDisabled()
    {
        // Arrange
        _options.DisableEnforcement = true;
        _requirementService.Setup(x => x.IsRequiredAsync(_user, It.IsAny<string>())).ReturnsAsync(true);

        // Act
        var isRequired = await CreatePolicy().IsTwoFactorRequiredAsync(new TwoFactorPolicyContext { User = _user });

        // Assert
        Assert.False(isRequired);
        _requirementService.Verify(x => x.IsRequiredAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    private MfaTwoFactorPolicy CreatePolicy()
    {
        return new MfaTwoFactorPolicy(_requirementService.Object, Options.Create(_options));
    }
}
