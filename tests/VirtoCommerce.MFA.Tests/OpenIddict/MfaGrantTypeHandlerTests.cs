using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OpenIddict.Abstractions;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Data.OpenIddict;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Events;
using VirtoCommerce.Platform.Core.Security.SignInLog;
using VirtoCommerce.Platform.Security.OpenIddict;
using Xunit;
using SignInResult = Microsoft.AspNetCore.Mvc.SignInResult;

namespace VirtoCommerce.MFA.Tests.OpenIddict;

[Trait("Category", "Unit")]
public class MfaGrantTypeHandlerTests : MfaTestsBase
{
    private readonly Mock<IEventPublisher> _eventPublisher = new();
    private readonly MfaGrantTypeHandler _handler;

    public MfaGrantTypeHandlerTests()
    {
        _handler = CreateHandler();
    }

    [Fact]
    public void GrantType_Should_Be_Mfa()
    {
        Assert.Equal(ModuleConstants.Security.GrantType, _handler.GrantType);
    }

    [Fact]
    public async Task HandleAsync_Should_NameTheMissingParameters_When_RequiredParametersAreMissing()
    {
        // Arrange
        var requestContext = CreateRequestContext(StoreId, twoFactorToken: null, code: null);

        // Act
        var actionResult = await _handler.HandleAsync(requestContext);

        // Assert
        var error = Assert.IsType<TokenResponse>(Assert.IsType<BadRequestObjectResult>(actionResult).Value);
        Assert.Equal(OpenIddictConstants.Errors.InvalidRequest, error.Error);
        Assert.Equal("missing_parameter", error.Code);
        Assert.Equal("Missing required parameters: two_factor_token, code.", error.ErrorDescription);
        Assert.Equal(ModuleConstants.Security.FailureReason.MissingParameter, requestContext.FailureReason);
    }

    [Theory]
    [InlineData(ActiveUserId, InvalidCode, true, "invalid_code", ModuleConstants.Security.FailureReason.InvalidCode)]
    [InlineData(ActiveUserId, InvalidCode, false, "invalid_code", ModuleConstants.Security.FailureReason.InvalidCode)]
    [InlineData(UnknownUserId, ValidCode, true, "invalid_two_factor_token", ModuleConstants.Security.FailureReason.InvalidTwoFactorToken)]
    [InlineData(UnknownUserId, ValidCode, false, "invalid_two_factor_token", ModuleConstants.Security.FailureReason.InvalidTwoFactorToken)]
    [InlineData(LockedUserId, ValidCode, true, "user_is_temporary_locked_out", SignInFailureReason.LockedOut)]
    [InlineData(LockedUserId, ValidCode, false, "invalid_code", SignInFailureReason.LockedOut)]
    [InlineData(LockoutDisabledUserId, ValidCode, true, "lockout_disabled", ModuleConstants.Security.FailureReason.LockoutDisabled)]
    [InlineData(LockoutDisabledUserId, ValidCode, false, "lockout_disabled", ModuleConstants.Security.FailureReason.LockoutDisabled)]
    [InlineData(UnconfirmedEmailUserId, ValidCode, true, "mfa_provider_not_available", ModuleConstants.Security.FailureReason.ProviderNotAvailable)]
    [InlineData(UnconfirmedEmailUserId, ValidCode, false, "mfa_provider_not_available", ModuleConstants.Security.FailureReason.ProviderNotAvailable)]
    public async Task HandleAsync_Should_ReturnExpectedError(string userId, string code, bool detailedErrors, string expectedErrorCode, string expectedFailureReason)
    {
        // Arrange
        var requestContext = CreateRequestContext(StoreId, CreateTwoFactorToken(userId), code, detailedErrors);

        // Act
        var actionResult = await _handler.HandleAsync(requestContext);

        // Assert
        var error = Assert.IsType<TokenResponse>(Assert.IsType<BadRequestObjectResult>(actionResult).Value);
        Assert.Equal(expectedErrorCode, error.Code);
        Assert.Equal(expectedFailureReason, requestContext.FailureReason);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task HandleAsync_Should_ReportLockoutSeconds_Only_When_DetailedErrorsEnabled(bool detailedErrors, bool expectSeconds)
    {
        // Arrange
        var requestContext = CreateRequestContext(StoreId, CreateTwoFactorToken(LockedUserId), ValidCode, detailedErrors);

        // Act
        var actionResult = await _handler.HandleAsync(requestContext);

        // Assert
        var error = Assert.IsType<TokenResponse>(Assert.IsType<BadRequestObjectResult>(actionResult).Value);
        Assert.Equal(expectSeconds, error.LockoutSecondsRemaining > 0);
        Assert.True(requestContext.SignInResult.IsLockedOut);
    }

    [Fact]
    public async Task HandleAsync_Should_RejectToken_When_IssuedForAnotherStore()
    {
        // Arrange
        var requestContext = CreateRequestContext(MfaDisabledStoreId, CreateTwoFactorToken(), ValidCode);

        // Act
        var actionResult = await _handler.HandleAsync(requestContext);

        // Assert
        var error = Assert.IsType<TokenResponse>(Assert.IsType<BadRequestObjectResult>(actionResult).Value);
        Assert.Equal("invalid_two_factor_token", error.Code);
    }

    [Fact]
    public async Task HandleAsync_Should_LogTheFailedAttemptWithTheUser_When_CodeIsWrong()
    {
        // Arrange
        var requestContext = CreateRequestContext(StoreId, CreateTwoFactorToken(), InvalidCode);

        // Act
        await _handler.HandleAsync(requestContext);

        // Assert
        _eventPublisher.Verify(x => x.Publish(It.Is<UserSignInAttemptEvent>(e =>
            !e.Succeeded &&
            e.UserId == ActiveUserId &&
            e.SignInType == ModuleConstants.Security.SignInType &&
            e.FailureReason == ModuleConstants.Security.FailureReason.InvalidCode)), Times.Once);
    }

    [Theory]
    [InlineData(StoreId, ActiveUserId)]
    [InlineData(null, BackofficeUserId)]
    public async Task HandleAsync_Should_SignIn_When_CodeIsValid(string storeId, string userId)
    {
        // Arrange
        var requestContext = CreateRequestContext(storeId, CreateTwoFactorToken(userId, storeId), ValidCode);

        // Act
        var actionResult = await _handler.HandleAsync(requestContext);

        // Assert
        var signInResult = Assert.IsType<SignInResult>(actionResult);
        Assert.Equal(ModuleConstants.Security.AuthenticationMethod, signInResult.Principal.FindFirst(OpenIddictConstants.Claims.AuthenticationMethodReference)?.Value);
        Assert.True(requestContext.SignInResult.Succeeded);
        UserManager.Verify(x => x.ResetAccessFailedCountAsync(It.Is<ApplicationUser>(u => u.Id == userId)), Times.Once);
        _eventPublisher.Verify(x => x.Publish(It.IsAny<UserLoginEvent>()), Times.Once);
        _eventPublisher.Verify(x => x.Publish(It.Is<UserSignInAttemptEvent>(e =>
            e.Succeeded && e.UserId == userId && e.SignInType == ModuleConstants.Security.SignInType)), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnBadRequest_When_ARequestValidatorRejects()
    {
        // Arrange
        var validator = new Mock<ITokenRequestValidator>();
        validator
            .Setup(x => x.ValidateAsync(It.IsAny<TokenRequestContext>()))
            .ReturnsAsync([new TokenResponse { Code = "user_cannot_login_in_store" }]);

        var handler = CreateHandler(validator.Object);
        var requestContext = CreateRequestContext(StoreId, CreateTwoFactorToken(), ValidCode);

        // Act
        var actionResult = await handler.HandleAsync(requestContext);

        // Assert
        var error = Assert.IsType<TokenResponse>(Assert.IsType<BadRequestObjectResult>(actionResult).Value);
        Assert.Equal("user_cannot_login_in_store", error.Code);
        UserManager.Verify(x => x.ResetAccessFailedCountAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Theory]
    [InlineData(true, "user_is_temporary_locked_out")]
    [InlineData(false, "invalid_code")]
    public async Task HandleAsync_Should_ReportLockout_When_TheWrongCodeLocksTheAccount(bool detailedErrors, string expectedErrorCode)
    {
        // Arrange
        UserManager
            .Setup(x => x.AccessFailedAsync(It.Is<ApplicationUser>(u => u.Id == ActiveUserId)))
            .Callback((ApplicationUser user) => user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15))
            .ReturnsAsync(IdentityResult.Success);
        var requestContext = CreateRequestContext(StoreId, CreateTwoFactorToken(), InvalidCode, detailedErrors);

        // Act
        var actionResult = await _handler.HandleAsync(requestContext);

        // Assert
        var error = Assert.IsType<TokenResponse>(Assert.IsType<BadRequestObjectResult>(actionResult).Value);
        Assert.Equal(expectedErrorCode, error.Code);
        Assert.True(requestContext.SignInResult.IsLockedOut);
    }

    private MfaGrantTypeHandler CreateHandler(params ITokenRequestValidator[] requestValidators)
    {
        return new MfaGrantTypeHandler(SignInManager.Object, IdentityOptions, requestValidators, [new MfaAuthenticationMethodClaimProvider()], [], _eventPublisher.Object, MfaService, UserManager.Object);
    }

    private static TokenRequestContext CreateRequestContext(string storeId, string twoFactorToken, string code, bool detailedErrors = false)
    {
        var request = new OpenIddictRequest { GrantType = ModuleConstants.Security.GrantType };
        request.SetParameter(ModuleConstants.Security.Parameters.StoreId, storeId);
        request.SetParameter(ModuleConstants.Security.Parameters.TwoFactorToken, twoFactorToken);
        request.SetParameter(ModuleConstants.Security.Parameters.Code, code);

        return new TokenRequestContext
        {
            AuthenticationScheme = "test-scheme",
            Request = request,
            Properties = new AuthenticationProperties(),
            DetailedErrors = detailedErrors,
        };
    }
}
