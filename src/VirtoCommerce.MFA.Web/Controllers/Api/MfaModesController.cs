using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;

namespace VirtoCommerce.MFA.Web.Controllers.Api;

/// <summary>
/// Lets an administrator read and set the MFA mode of a user or an organization, including Required, which the storefront can't change.
/// </summary>
[ApiController]
[Route("api/mfa/modes")]
public class MfaModesController(IMfaModeService modeService, IMfaStatusService statusService) : Controller
{
    [HttpGet]
    [Route("users/{userId}")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<MfaModeInfo>> GetUserMode([FromRoute] string userId)
    {
        return Ok(new MfaModeInfo { Mode = await modeService.GetUserModeAsync(userId) });
    }

    [HttpPut]
    [Route("users/{userId}")]
    [Authorize(ModuleConstants.Security.Permissions.Manage)]
    public async Task<ActionResult<MfaModeInfo>> SetUserMode([FromRoute] string userId, [FromBody] MfaModeInfo request)
    {
        if (!IsKnownMode(request?.Mode))
        {
            return BadRequest($"Unknown MFA mode '{request?.Mode}'.");
        }

        await modeService.SetUserModeAsync(userId, request.Mode);

        return Ok(new MfaModeInfo { Mode = await modeService.GetUserModeAsync(userId) });
    }

    /// <summary>
    /// Shows in every store whether the user's sign-in needs the code, with the given mode instead of the saved one if it is set.
    /// </summary>
    [HttpGet]
    [Route("users/{userId}/status")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<MfaStatus>> GetUserStatus([FromRoute] string userId, [FromQuery] string mode = null)
    {
        if (mode != null && !IsKnownMode(mode))
        {
            return BadRequest($"Unknown MFA mode '{mode}'.");
        }

        var status = await statusService.GetUserStatusAsync(userId, NormalizeMode(mode));

        return status == null ? NotFound() : Ok(status);
    }

    [HttpGet]
    [Route("organizations/{organizationId}")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<MfaModeInfo>> GetOrganizationMode([FromRoute] string organizationId)
    {
        return Ok(new MfaModeInfo { Mode = await modeService.GetOrganizationModeAsync(organizationId) });
    }

    [HttpPut]
    [Route("organizations/{organizationId}")]
    [Authorize(ModuleConstants.Security.Permissions.Manage)]
    public async Task<ActionResult<MfaModeInfo>> SetOrganizationMode([FromRoute] string organizationId, [FromBody] MfaModeInfo request)
    {
        if (!IsKnownMode(request?.Mode))
        {
            return BadRequest($"Unknown MFA mode '{request?.Mode}'.");
        }

        await modeService.SetOrganizationModeAsync(organizationId, request.Mode);

        return Ok(new MfaModeInfo { Mode = await modeService.GetOrganizationModeAsync(organizationId) });
    }

    /// <summary>
    /// Shows in every store whether the organization requires the code from its members, with the given mode instead of the saved one if it is set.
    /// </summary>
    [HttpGet]
    [Route("organizations/{organizationId}/status")]
    [Authorize(ModuleConstants.Security.Permissions.Read)]
    public async Task<ActionResult<MfaStatus>> GetOrganizationStatus([FromRoute] string organizationId, [FromQuery] string mode = null)
    {
        if (mode != null && !IsKnownMode(mode))
        {
            return BadRequest($"Unknown MFA mode '{mode}'.");
        }

        return Ok(await statusService.GetOrganizationStatusAsync(organizationId, NormalizeMode(mode)));
    }

    private static string NormalizeMode(string mode)
    {
        return ModuleConstants.Modes.AllModes.FirstOrDefault(x => x.Equals(mode, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsKnownMode(string mode)
    {
        return ModuleConstants.Modes.AllModes.Any(x => x.Equals(mode, StringComparison.OrdinalIgnoreCase));
    }
}
