using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.MFA.Web.Controllers.Api;

[ApiController]
[Route("api/mfa/users/{userId}/backoffice-exemption")]
public class MfaBackofficeExemptionsController(
    IMfaBackofficeExemptionService exemptionService,
    UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<MfaBackofficeExemption>> GetExemption([FromRoute] string userId)
    {
        if (await userManager.FindByIdAsync(userId) == null)
        {
            return NotFound();
        }

        var exemption = AbstractTypeFactory<MfaBackofficeExemption>.TryCreateInstance();
        exemption.IsExempt = await exemptionService.IsExemptAsync(userId);

        return Ok(exemption);
    }

    [HttpPut]
    [Authorize(ModuleConstants.Security.Permissions.Manage)]
    public async Task<ActionResult<MfaBackofficeExemption>> SetExemption(
        [FromRoute] string userId, [FromBody] MfaBackofficeExemption request)
    {
        if (request == null)
        {
            return BadRequest();
        }

        if (await userManager.FindByIdAsync(userId) == null)
        {
            return NotFound();
        }

        await exemptionService.SetExemptionAsync(userId, request.IsExempt);

        var exemption = AbstractTypeFactory<MfaBackofficeExemption>.TryCreateInstance();
        exemption.IsExempt = await exemptionService.IsExemptAsync(userId);

        return Ok(exemption);
    }
}
