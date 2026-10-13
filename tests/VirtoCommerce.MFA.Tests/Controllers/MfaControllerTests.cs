using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moq;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.MFA.Web.Controllers.Api;
using VirtoCommerce.Platform.Core.Security;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Controllers;

[Trait("Category", "Unit")]
public class MfaControllerTests : MfaTestsBase
{
    private readonly Mock<IBackofficeMfaSignInService> _backofficeSignInService = new();

    [Fact]
    public async Task ResendCode_Should_ReturnNewTokenAndProviders_When_TokenIsValid()
    {
        // Act
        var result = await ResendCodeAsync(CreateController(), CreateTwoFactorToken());

        // Assert
        Assert.True(result.Succeeded);
        Assert.False(string.IsNullOrEmpty(result.TwoFactorToken));
        Assert.Equal(MaskedActiveUserEmail, Assert.Single(result.Providers).Destination);
    }

    [Fact]
    public async Task ResendCode_Should_ReportTheSecondsBeforeANewCode_When_ACodeWasJustSent()
    {
        // Arrange
        var controller = CreateController();
        var twoFactorToken = CreateTwoFactorToken();
        await ResendCodeAsync(controller, twoFactorToken);

        // Act
        var result = await ResendCodeAsync(controller, twoFactorToken);

        // Assert
        Assert.Equal("resend_too_soon", result.Error.Code);
        Assert.Null(result.TwoFactorToken);
        Assert.True(result.ResendSecondsRemaining > 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ResendCode_Should_ReportInvalidToken(bool detailedErrors)
    {
        // Act
        var result = await ResendCodeAsync(CreateController(detailedErrors), "not-a-token");

        // Assert
        Assert.Equal("invalid_two_factor_token", result.Error.Code);
        Assert.Null(result.TwoFactorToken);
    }

    [Theory]
    [InlineData(true, "user_is_temporary_locked_out", true)]
    [InlineData(false, "login_failed", false)]
    public async Task ResendCode_Should_ReportLockout_Only_When_DetailedErrorsEnabled(bool detailedErrors, string expectedCode, bool expectSeconds)
    {
        // Act
        var result = await ResendCodeAsync(CreateController(detailedErrors), CreateTwoFactorToken(LockedUserId));

        // Assert
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Equal(expectSeconds, result.LockoutSecondsRemaining > 0);
    }

    [Fact]
    public async Task SendSignInCode_Should_ReturnTheProviders()
    {
        // Arrange
        var providers = new[] { new MfaProviderInfo { Name = EmailProvider, Destination = "m•••r@acme.com" } };
        _backofficeSignInService.Setup(x => x.SendCodeAsync(null)).ReturnsAsync(MfaResult.Succeed(BackofficeUser, providers: providers));

        // Act
        var actionResult = await CreateController().SendSignInCode(new MfaSendCodeRequest());

        // Assert
        var result = Assert.IsType<MfaSendCodeResult>(Assert.IsType<OkObjectResult>(actionResult.Result).Value);
        Assert.True(result.Succeeded);
        Assert.Same(providers, result.Providers);
    }

    [Fact]
    public async Task SendSignInCode_Should_ReportLockout_EvenWithoutDetailedErrors()
    {
        // Arrange
        var lockedUser = new ApplicationUser { Id = LockedUserId, LockoutEnd = DateTimeOffset.UtcNow.AddSeconds(LockoutSeconds) };
        _backofficeSignInService.Setup(x => x.SendCodeAsync(null)).ReturnsAsync(MfaResult.Fail(MfaOutcome.AccountLocked, lockedUser));

        // Act
        var actionResult = await CreateController(detailedErrors: false).SendSignInCode(new MfaSendCodeRequest());

        // Assert
        var result = Assert.IsType<MfaSendCodeResult>(Assert.IsType<OkObjectResult>(actionResult.Result).Value);
        Assert.Equal("user_is_temporary_locked_out", result.Error.Code);
        Assert.True(result.LockoutSecondsRemaining > 0);
    }

    [Theory]
    [InlineData(MfaOutcome.Success, null)]
    [InlineData(MfaOutcome.InvalidCode, "invalid_code")]
    [InlineData(MfaOutcome.InvalidTwoFactorToken, "invalid_two_factor_token")]
    [InlineData(MfaOutcome.SignInNotAllowed, "sign_in_not_allowed")]
    [InlineData(MfaOutcome.LockoutDisabled, "lockout_disabled")]
    [InlineData(MfaOutcome.ProviderNotAvailable, "mfa_provider_not_available")]
    [InlineData(MfaOutcome.DuplicateEmail, "login_failed")]
    public async Task VerifySignInCode_Should_ReportTheOutcome(MfaOutcome outcome, string expectedCode)
    {
        // Arrange
        _backofficeSignInService
            .Setup(x => x.SignInAsync(ValidCode, null, true))
            .ReturnsAsync(new MfaResult { Outcome = outcome, User = BackofficeUser });

        // Act
        var result = await VerifySignInCodeAsync(CreateController(), new MfaSignInRequest { Code = ValidCode, RememberMe = true });

        // Assert
        Assert.Equal(expectedCode, result.Error?.Code);
    }

    [Fact]
    public async Task VerifySignInCode_Should_ReportLockout_EvenWithoutDetailedErrors()
    {
        // Arrange
        var lockedUser = new ApplicationUser { Id = LockedUserId, LockoutEnd = DateTimeOffset.UtcNow.AddSeconds(LockoutSeconds) };
        _backofficeSignInService
            .Setup(x => x.SignInAsync(InvalidCode, null, false))
            .ReturnsAsync(MfaResult.Fail(MfaOutcome.AccountLocked, lockedUser));

        // Act
        var result = await VerifySignInCodeAsync(CreateController(detailedErrors: false), new MfaSignInRequest { Code = InvalidCode });

        // Assert
        Assert.Equal("user_is_temporary_locked_out", result.Error.Code);
        Assert.True(result.LockoutSecondsRemaining > 0);
    }

    private MfaController CreateController(bool detailedErrors = false)
    {
        return new MfaController(MfaService, _backofficeSignInService.Object, Options.Create(new PasswordLoginOptions { DetailedErrors = detailedErrors }));
    }

    private static async Task<MfaSendCodeResult> ResendCodeAsync(MfaController controller, string twoFactorToken)
    {
        var actionResult = await controller.ResendCode(new MfaResendCodeRequest { TwoFactorToken = twoFactorToken });

        return Assert.IsType<MfaSendCodeResult>(Assert.IsType<OkObjectResult>(actionResult.Result).Value);
    }

    private static async Task<MfaSignInResult> VerifySignInCodeAsync(MfaController controller, MfaSignInRequest request)
    {
        var actionResult = await controller.VerifySignInCode(request);

        return Assert.IsType<MfaSignInResult>(Assert.IsType<OkObjectResult>(actionResult.Result).Value);
    }
}
