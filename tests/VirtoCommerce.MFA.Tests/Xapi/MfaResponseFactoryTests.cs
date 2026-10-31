using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Xapi;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Xapi;

[Trait("Category", "Unit")]
public class MfaResponseFactoryTests : MfaTestsBase
{
    [Theory]
    [InlineData(MfaChangeOutcome.LockedByAdministrator, "mfa_locked_by_administrator")]
    [InlineData(MfaChangeOutcome.OrganizationNotAllowed, "mfa_organization_not_allowed")]
    [InlineData(MfaChangeOutcome.SelfServiceNotAllowed, "mfa_self_service_not_allowed")]
    public void ToChangeResponse_Should_ReportWhyTheChangeIsRefused(MfaChangeOutcome outcome, string expectedCode)
    {
        // Act
        var response = CreateFactory().ToChangeResponse(new MfaChangeResult { Outcome = outcome }, ActiveUser);

        // Assert
        Assert.False(response.Succeeded);
        Assert.Equal(expectedCode, response.ErrorCode);
        Assert.False(string.IsNullOrEmpty(response.ErrorDescription));
    }

    [Fact]
    public void ToChangeResponse_Should_ReportTheSignInErrorCode_When_TheCodeIsRefused()
    {
        // Arrange
        var result = new MfaChangeResult
        {
            Outcome = MfaChangeOutcome.CodeRefused,
            CodeResult = MfaResult.Fail(MfaOutcome.InvalidCode, ActiveUser),
        };

        // Act
        var response = CreateFactory().ToChangeResponse(result, ActiveUser);

        // Assert
        Assert.False(response.Succeeded);
        Assert.Equal("invalid_code", response.ErrorCode);
    }

    [Fact]
    public void ToChangeResponse_Should_ReturnTheNewSettings_When_TheChangeSucceeds()
    {
        // Arrange
        var settings = new MfaSettings { UserMode = "Enabled" };

        // Act
        var response = CreateFactory().ToChangeResponse(new MfaChangeResult { Outcome = MfaChangeOutcome.Success, Settings = settings }, ActiveUser);

        // Assert
        Assert.True(response.Succeeded);
        Assert.Null(response.ErrorCode);
        Assert.Same(settings, response.Settings);
    }

    [Fact]
    public void ToCodeChallenge_Should_ReturnTheTokenAndTheMaskedDestinationOfTheProvider()
    {
        // Arrange
        var result = MfaResult.Succeed(ActiveUser, EmailProvider, [new MfaProviderInfo { Name = EmailProvider, Destination = MaskedActiveUserEmail }]);
        result.TwoFactorToken = "token";
        result.ResendSecondsRemaining = 30;

        // Act
        var challenge = CreateFactory().ToCodeChallenge(result);

        // Assert
        Assert.True(challenge.Succeeded);
        Assert.Equal("token", challenge.TwoFactorToken);
        Assert.Equal(EmailProvider, challenge.Provider);
        Assert.Equal(MaskedActiveUserEmail, challenge.Destination);
        Assert.Equal(30, challenge.ResendSecondsRemaining);
    }

    private MfaResponseFactory CreateFactory()
    {
        return new MfaResponseFactory(ErrorDescriber);
    }
}
