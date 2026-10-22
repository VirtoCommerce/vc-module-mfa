using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Moq;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Data.Providers;
using VirtoCommerce.Platform.Core.Security;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Providers;

[Trait("Category", "Unit")]
public class MfaEmailTokenProviderTests
{
    private const string TwoFactorPurpose = ModuleConstants.Security.IdentityTwoFactorPurpose;
    private const string OtherPurpose = "EmailConfirmation";

    private readonly ApplicationUser _user = new() { Id = "user-1", Email = "john.smith@acme.com", SecurityStamp = "security-stamp" };
    private readonly Dictionary<string, string> _codeStamps = [];
    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly MfaEmailTokenProvider _provider = new();

    public MfaEmailTokenProviderTests()
    {
        _userManager = new Mock<UserManager<ApplicationUser>>(Mock.Of<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);

        _userManager
            .Setup(x => x.GetEmailAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync((ApplicationUser user) => user.Email);

        _userManager
            .Setup(x => x.CreateSecurityTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync((ApplicationUser user) => Encoding.Unicode.GetBytes(user.SecurityStamp));

        _userManager
            .Setup(x => x.GetAuthenticationTokenAsync(It.IsAny<ApplicationUser>(), ModuleConstants.Security.CodeStampLoginProvider, ModuleConstants.Security.CodeStampName))
            .ReturnsAsync((ApplicationUser user, string _, string _) => _codeStamps.GetValueOrDefault(user.Id));
    }

    [Fact]
    public async Task ValidateAsync_Should_AcceptTheCode_When_TheCodeStampIsTheSame()
    {
        // Arrange
        _codeStamps[_user.Id] = "first-stamp";
        var code = await _provider.GenerateAsync(TwoFactorPurpose, _userManager.Object, _user);

        // Act
        var isValid = await _provider.ValidateAsync(TwoFactorPurpose, code, _userManager.Object, _user);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task ValidateAsync_Should_RejectTheCode_When_TheCodeStampChanged()
    {
        // Arrange
        _codeStamps[_user.Id] = "first-stamp";
        var code = await _provider.GenerateAsync(TwoFactorPurpose, _userManager.Object, _user);
        _codeStamps[_user.Id] = "second-stamp";

        // Act
        var isValid = await _provider.ValidateAsync(TwoFactorPurpose, code, _userManager.Object, _user);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task GetUserModifierAsync_Should_AddTheCodeStamp_When_ThePurposeIsTwoFactor()
    {
        // Arrange
        _codeStamps[_user.Id] = "first-stamp";

        // Act
        var modifier = await _provider.GetUserModifierAsync(TwoFactorPurpose, _userManager.Object, _user);

        // Assert
        Assert.Equal($"Email:{TwoFactorPurpose}:{_user.Email}:first-stamp", modifier);
    }

    [Fact]
    public async Task GetUserModifierAsync_Should_KeepTheIdentityModifier_When_ThePurposeIsNotTwoFactor()
    {
        // Arrange
        _codeStamps[_user.Id] = "first-stamp";

        // Act
        var modifier = await _provider.GetUserModifierAsync(OtherPurpose, _userManager.Object, _user);

        // Assert
        Assert.Equal($"Email:{OtherPurpose}:{_user.Email}", modifier);
        _userManager.Verify(x => x.GetAuthenticationTokenAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
