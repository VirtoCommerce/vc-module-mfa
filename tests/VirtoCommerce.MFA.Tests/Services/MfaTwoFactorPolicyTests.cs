using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
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
    private readonly DefaultHttpContext _httpContext = new();

    [Fact]
    public async Task IsTwoFactorRequiredAsync_Should_UseTheStoreOfTheTokenRequest()
    {
        // Arrange
        var request = new OpenIddictRequest { GrantType = "password" };
        request.SetParameter(ModuleConstants.Security.Parameters.StoreId, StoreId);
        SetTokenRequest(request);
        _requirementService.Setup(x => x.IsRequiredAsync(_user, StoreId)).ReturnsAsync(true);

        // Act
        var isRequired = await CreatePolicy().IsTwoFactorRequiredAsync(_user);

        // Assert
        Assert.True(isRequired);
    }

    [Fact]
    public async Task IsTwoFactorRequiredAsync_Should_UseNoStore_When_TheSignInIsNotATokenRequest()
    {
        // Arrange
        _requirementService.Setup(x => x.IsRequiredAsync(_user, null)).ReturnsAsync(true);

        // Act
        var isRequired = await CreatePolicy().IsTwoFactorRequiredAsync(_user);

        // Assert
        Assert.True(isRequired);
        _requirementService.Verify(x => x.IsRequiredAsync(_user, null), Times.Once);
    }

    [Fact]
    public async Task IsTwoFactorRequiredAsync_Should_UseNoStore_When_ThereIsNoHttpContext()
    {
        // Arrange
        _requirementService.Setup(x => x.IsRequiredAsync(_user, null)).ReturnsAsync(true);
        var policy = new MfaTwoFactorPolicy(_requirementService.Object, Options.Create(_options), new HttpContextAccessor());

        // Act
        var isRequired = await policy.IsTwoFactorRequiredAsync(_user);

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
        var isRequired = await CreatePolicy().IsTwoFactorRequiredAsync(_user);

        // Assert
        Assert.False(isRequired);
        _requirementService.Verify(x => x.IsRequiredAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    private void SetTokenRequest(OpenIddictRequest request)
    {
        _httpContext.Features.Set(new OpenIddictServerAspNetCoreFeature
        {
            Transaction = new OpenIddictServerTransaction { Request = request },
        });
    }

    private MfaTwoFactorPolicy CreatePolicy()
    {
        return new MfaTwoFactorPolicy(_requirementService.Object, Options.Create(_options), new HttpContextAccessor { HttpContext = _httpContext });
    }
}
