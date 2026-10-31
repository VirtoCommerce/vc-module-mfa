using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.MFA.Web.Controllers.Api;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Controllers;

[Trait("Category", "Unit")]
public class MfaModesControllerTests
{
    private readonly InMemoryMfaModeService _modeService = new();
    private readonly Mock<IMfaStatusService> _statusService = new();

    [Theory]
    [InlineData(nameof(MfaModesController.GetUserMode), ModuleConstants.Security.Permissions.Read)]
    [InlineData(nameof(MfaModesController.GetOrganizationMode), ModuleConstants.Security.Permissions.Read)]
    [InlineData(nameof(MfaModesController.GetUserStatus), ModuleConstants.Security.Permissions.Read)]
    [InlineData(nameof(MfaModesController.GetOrganizationStatus), ModuleConstants.Security.Permissions.Read)]
    [InlineData(nameof(MfaModesController.SetUserMode), ModuleConstants.Security.Permissions.Manage)]
    [InlineData(nameof(MfaModesController.SetOrganizationMode), ModuleConstants.Security.Permissions.Manage)]
    public void MfaModesController_Should_ReadWithTheReadPermissionAndChangeWithTheManagePermission(string methodName, string expectedPermission)
    {
        // Act
        var classPolicy = typeof(MfaModesController).GetCustomAttribute<AuthorizeAttribute>()?.Policy;
        var methodPolicy = typeof(MfaModesController).GetMethod(methodName)?.GetCustomAttribute<AuthorizeAttribute>()?.Policy;

        // Assert
        Assert.Null(classPolicy);
        Assert.Equal(expectedPermission, methodPolicy);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Bogus")]
    [InlineData("")]
    public async Task SetUserMode_Should_RefuseAnUnknownModeOrNoBody(string mode)
    {
        // Arrange
        var request = mode == null ? null : new MfaModeInfo { Mode = mode };

        // Act
        var userResult = await CreateController().SetUserMode("user-1", request);
        var organizationResult = await CreateController().SetOrganizationMode("organization-1", request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(userResult.Result);
        Assert.IsType<BadRequestObjectResult>(organizationResult.Result);
        Assert.Equal(ModuleConstants.Modes.Off, await _modeService.GetUserModeAsync("user-1"));
        Assert.Equal(ModuleConstants.Modes.Off, await _modeService.GetOrganizationModeAsync("organization-1"));
    }

    [Fact]
    public async Task SetUserMode_Should_SaveAKnownModeInAnyCase()
    {
        // Act
        var result = await CreateController().SetUserMode("user-1", new MfaModeInfo { Mode = "required" });

        // Assert
        var mode = Assert.IsType<MfaModeInfo>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(ModuleConstants.Modes.Required, mode.Mode);
    }

    [Fact]
    public async Task GetUserStatus_Should_RefuseAnUnknownMode()
    {
        // Act
        var result = await CreateController().GetUserStatus("user-1", "Bogus");

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
        _statusService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetUserStatus_Should_ReturnNotFound_When_TheUserDoesNotExist()
    {
        // Act
        var result = await CreateController().GetUserStatus("unknown-user");

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetOrganizationStatus_Should_PassTheKnownModeInItsOwnCase()
    {
        // Arrange
        _statusService.Setup(x => x.GetOrganizationStatusAsync("organization-1", ModuleConstants.Modes.Enabled)).ReturnsAsync(new MfaStatus());

        // Act
        var result = await CreateController().GetOrganizationStatus("organization-1", "enabled");

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
        _statusService.Verify(x => x.GetOrganizationStatusAsync("organization-1", ModuleConstants.Modes.Enabled));
    }

    private MfaModesController CreateController()
    {
        return new MfaModesController(_modeService, _statusService.Object);
    }
}
