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
public class MfaBackofficeExemptionsControllerTests : MfaTestsBase
{
    private readonly Mock<IMfaModeService> _modeService = new();
    [Theory]
    [InlineData(nameof(MfaBackofficeExemptionsController.GetExemption), ModuleConstants.Security.Permissions.Read)]
    [InlineData(nameof(MfaBackofficeExemptionsController.SetExemption), ModuleConstants.Security.Permissions.Manage)]
    public void ExemptionEndpoints_Should_RequireTheCorrespondingPermission(string method, string permission)
    {
        var action = typeof(MfaBackofficeExemptionsController).GetMethod(method);
        Assert.Equal(permission, action.GetCustomAttribute<AuthorizeAttribute>()?.Policy);
        Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Null(typeof(MfaBackofficeExemptionsController).GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Fact]
    public async Task ExemptionEndpoints_Should_RejectAnUnknownUserWithoutWritingSettings()
    {
        var controller = new MfaBackofficeExemptionsController(_modeService.Object, UserManager.Object);

        Assert.IsType<NotFoundResult>((await controller.GetExemption(UnknownUserId)).Result);
        Assert.IsType<NotFoundResult>((await controller.SetExemption(UnknownUserId, new MfaBackofficeExemption { IsExempt = true })).Result);
        _modeService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SetExemption_Should_RejectANullBody()
    {
        var controller = new MfaBackofficeExemptionsController(_modeService.Object, UserManager.Object);

        Assert.IsType<BadRequestResult>((await controller.SetExemption(BackofficeUserId, null)).Result);
        _modeService.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SetExemption_Should_ReturnTheSavedSetting(bool isExempt)
    {
        _modeService.Setup(x => x.IsBackofficeExemptAsync(BackofficeUserId)).ReturnsAsync(isExempt);
        var controller = new MfaBackofficeExemptionsController(_modeService.Object, UserManager.Object);

        var response = await controller.SetExemption(BackofficeUserId, new MfaBackofficeExemption { IsExempt = isExempt });

        Assert.Equal(isExempt, Assert.IsType<MfaBackofficeExemption>(Assert.IsType<OkObjectResult>(response.Result).Value).IsExempt);
        _modeService.Verify(x => x.SetBackofficeExemptionAsync(BackofficeUserId, isExempt), Times.Once);
    }
}
