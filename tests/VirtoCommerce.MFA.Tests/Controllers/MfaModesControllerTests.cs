using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Web.Controllers.Api;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Controllers;

[Trait("Category", "Unit")]
public class MfaModesControllerTests
{
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
}
