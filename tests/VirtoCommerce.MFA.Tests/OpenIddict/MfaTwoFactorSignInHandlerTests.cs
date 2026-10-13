using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenIddict.Abstractions;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Data.OpenIddict;
using VirtoCommerce.Platform.Core.JsonConverters;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Security.OpenIddict;
using Xunit;

namespace VirtoCommerce.MFA.Tests.OpenIddict;

[Trait("Category", "Unit")]
public class MfaTwoFactorSignInHandlerTests : MfaTestsBase
{
    private readonly MfaTwoFactorSignInHandler _handler;

    public MfaTwoFactorSignInHandlerTests()
    {
        _handler = new MfaTwoFactorSignInHandler(MfaService);
    }

    [Theory]
    [InlineData(StoreId)]
    [InlineData(null)]
    public async Task ChallengeAsync_Should_ReturnTwoFactorRequired_When_CodeIsSent(string storeId)
    {
        // Arrange
        var requestContext = CreateRequestContext(ActiveUser, storeId);

        // Act
        var actionResult = await _handler.ChallengeAsync(requestContext);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        var response = Assert.IsType<TwoFactorRequiredResponse>(badRequest.Value);
        Assert.Equal(OpenIddictConstants.Errors.InvalidGrant, response.Error);
        Assert.Equal("two_factor_required", response.Code);
        Assert.Equal(storeId, TwoFactorTokenService.ReadToken(response.TwoFactorToken).StoreId);
        Assert.Equal(EmailProvider, response.Provider);
        Assert.Equal(MaskedActiveUserEmail, Assert.Single(response.Providers).Destination);
        Assert.Equal((int)MfaOptions.CodeResendInterval.TotalSeconds, response.ResendSecondsRemaining);
        Assert.Null(requestContext.FailureReason);
    }

    [Fact]
    public async Task ChallengeAsync_Should_ReturnResponseTheClientCanRead()
    {
        // Arrange
        var requestContext = CreateRequestContext(ActiveUser, StoreId);
        var settings = new JsonSerializerSettings { ContractResolver = new PolymorphJsonContractResolver() };

        // Act
        var actionResult = await _handler.ChallengeAsync(requestContext);

        // Assert
        // Serialized the way the platform writes MVC responses
        var json = JObject.Parse(JsonConvert.SerializeObject(((BadRequestObjectResult)actionResult).Value, settings));
        Assert.Equal("two_factor_required", (string)json["errors"]![0]!["code"]);
        Assert.False(string.IsNullOrEmpty((string)json["twoFactorToken"]));
        Assert.Equal(EmailProvider, (string)json["providers"]![0]!["name"]);
        Assert.Equal(MaskedActiveUserEmail, (string)json["providers"]![0]!["destination"]);
        Assert.Equal((int)MfaOptions.CodeResendInterval.TotalSeconds, (int)json["resendSecondsRemaining"]);
    }

    [Theory]
    [InlineData(true, "lockout_disabled")]
    [InlineData(false, "lockout_disabled")]
    public async Task ChallengeAsync_Should_RefuseSignIn_When_LockoutIsDisabled(bool detailedErrors, string expectedCode)
    {
        // Arrange
        var requestContext = CreateRequestContext(LockoutDisabledUser, StoreId, detailedErrors);

        // Act
        var actionResult = await _handler.ChallengeAsync(requestContext);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.Equal(expectedCode, Assert.IsType<TokenResponse>(badRequest.Value).Code);
        Assert.Equal(ModuleConstants.Security.FailureReason.LockoutDisabled, requestContext.FailureReason);
    }

    [Theory]
    [InlineData(true, "mfa_provider_not_available")]
    [InlineData(false, "mfa_provider_not_available")]
    public async Task ChallengeAsync_Should_RefuseSignIn_When_EmailIsNotConfirmed(bool detailedErrors, string expectedCode)
    {
        // Arrange
        var requestContext = CreateRequestContext(UnconfirmedEmailUser, StoreId, detailedErrors);

        // Act
        var actionResult = await _handler.ChallengeAsync(requestContext);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.Equal(expectedCode, Assert.IsType<TokenResponse>(badRequest.Value).Code);
        Assert.Equal(ModuleConstants.Security.FailureReason.ProviderNotAvailable, requestContext.FailureReason);
    }

    private static TokenRequestContext CreateRequestContext(ApplicationUser user, string storeId, bool detailedErrors = false)
    {
        var request = new OpenIddictRequest { GrantType = OpenIddictConstants.GrantTypes.Password };

        if (storeId != null)
        {
            request.SetParameter(ModuleConstants.Security.Parameters.StoreId, storeId);
        }

        return new TokenRequestContext
        {
            Request = request,
            User = user,
            DetailedErrors = detailedErrors,
        };
    }
}
