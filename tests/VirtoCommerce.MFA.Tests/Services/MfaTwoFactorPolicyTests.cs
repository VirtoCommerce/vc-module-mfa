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
    private const string MfaStoreId = "B2B-store";

    private readonly ApplicationUser _user = new() { Id = "user-1" };
    private readonly Mock<IMfaRequirementService> _requirementService = new();
    private readonly MfaOptions _options = new();
    private readonly DefaultHttpContext _httpContext = new();
    private bool _isRequiredForBackoffice;

    public MfaTwoFactorPolicyTests()
    {
        // The store named in the token request requires MFA, a token request without a store doesn't.
        _requirementService
            .Setup(x => x.IsRequiredAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser _, string storeId) => storeId == MfaStoreId);

        _requirementService
            .Setup(x => x.IsRequiredForBackofficeAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(() => _isRequiredForBackoffice);
    }

    [Fact]
    public async Task IsTwoFactorRequiredAsync_Should_UseTheStoreOfTheTokenRequest()
    {
        // Arrange
        var request = new OpenIddictRequest { GrantType = "password" };
        request.SetParameter(ModuleConstants.Security.Parameters.StoreId, MfaStoreId);
        SetTokenRequest(request);
        var policy = CreatePolicy();

        // Act
        var isRequired = await policy.IsTwoFactorRequiredAsync(_user);

        // Assert
        Assert.True(isRequired);
    }

    [Fact]
    public async Task IsTwoFactorRequiredAsync_Should_UseNoStore_When_TheTokenRequestNamesNone()
    {
        // Arrange
        SetTokenRequest(new OpenIddictRequest { GrantType = "password" });
        var policy = CreatePolicy();

        // Act
        var isRequired = await policy.IsTwoFactorRequiredAsync(_user);

        // Assert
        Assert.False(isRequired);
        _requirementService.Verify(x => x.IsRequiredAsync(_user, null), Times.Once);
    }

    [Fact]
    public async Task IsTwoFactorRequiredAsync_Should_FollowTheBackofficeRule_When_TheSignInIsNotATokenRequest()
    {
        // Arrange
        _isRequiredForBackoffice = true;
        var policy = CreatePolicy();

        // Act
        var isRequired = await policy.IsTwoFactorRequiredAsync(_user);

        // Assert
        Assert.True(isRequired);
        _requirementService.Verify(x => x.IsRequiredAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task IsTwoFactorRequiredAsync_Should_FollowTheBackofficeRule_When_ThereIsNoHttpContext()
    {
        // Arrange
        _isRequiredForBackoffice = true;
        var policy = new MfaTwoFactorPolicy(_requirementService.Object, Options.Create(_options), new HttpContextAccessor());

        // Act
        var isRequired = await policy.IsTwoFactorRequiredAsync(_user);

        // Assert
        Assert.True(isRequired);
        _requirementService.Verify(x => x.IsRequiredAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task IsTwoFactorRequiredAsync_Should_RequireNothing_When_EnforcementIsDisabled()
    {
        // Arrange
        _options.DisableEnforcement = true;
        var request = new OpenIddictRequest { GrantType = "password" };
        request.SetParameter(ModuleConstants.Security.Parameters.StoreId, MfaStoreId);
        SetTokenRequest(request);
        var policy = CreatePolicy();

        // Act
        var isRequired = await policy.IsTwoFactorRequiredAsync(_user);

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
