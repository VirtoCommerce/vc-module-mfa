using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Moq;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using VirtoCommerce.MFA.Core;
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
    private readonly Mock<IMfaService> _mfaService = new();
    private readonly DefaultHttpContext _httpContext = new();

    [Fact]
    public async Task IsTwoFactorRequiredAsync_Should_UseTheStoreOfTheTokenRequest()
    {
        // Arrange
        var request = new OpenIddictRequest { GrantType = OpenIddictConstants.GrantTypes.Password };
        request.SetParameter(ModuleConstants.Security.Parameters.StoreId, StoreId);
        _httpContext.Features.Set(new OpenIddictServerAspNetCoreFeature
        {
            Transaction = new OpenIddictServerTransaction { Request = request },
        });
        _mfaService.Setup(x => x.IsRequiredAsync(_user, StoreId)).ReturnsAsync(true);

        // Act
        var isRequired = await CreatePolicy().IsTwoFactorRequiredAsync(_user);

        // Assert
        Assert.True(isRequired);
    }

    [Fact]
    public async Task IsTwoFactorRequiredAsync_Should_UseNoStore_When_TheSignInIsNotATokenRequest()
    {
        // Arrange
        _mfaService.Setup(x => x.IsRequiredAsync(_user, null)).ReturnsAsync(true);

        // Act
        var isRequired = await CreatePolicy().IsTwoFactorRequiredAsync(_user);

        // Assert
        Assert.True(isRequired);
        _mfaService.Verify(x => x.IsRequiredAsync(_user, null), Times.Once);
    }

    private MfaTwoFactorPolicy CreatePolicy()
    {
        return new MfaTwoFactorPolicy(_mfaService.Object, new HttpContextAccessor { HttpContext = _httpContext });
    }
}
