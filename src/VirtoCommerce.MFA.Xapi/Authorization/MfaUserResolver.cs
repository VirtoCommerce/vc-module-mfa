using System;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Security.Authorization;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Security.Authorization;

namespace VirtoCommerce.MFA.Xapi.Authorization;

/// <summary>
/// Finds the signed-in user of an xAPI request and decides whether they may manage the MFA of an organization.
/// </summary>
public class MfaUserResolver(
    Func<UserManager<ApplicationUser>> userManagerFactory,
    IAuthorizationService authorizationService,
    IMemberService memberService)
{
    /// <param name="isChange">A change can't be made in an impersonated session.</param>
    public virtual async Task<ApplicationUser> GetCurrentUserAsync(IResolveFieldContext context, bool isChange)
    {
        var userId = context.GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
        {
            throw AuthorizationError.AnonymousAccessDenied();
        }

        using var userManager = userManagerFactory();
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw AuthorizationError.AnonymousAccessDenied();
        }

        if (isChange && context.GetCurrentPrincipal().HasClaim(x => x.Type == PlatformConstants.Security.Claims.OperatorUserId))
        {
            throw AuthorizationError.Forbidden();
        }

        return user;
    }

    /// <summary>
    /// The user needs the frontend permission and must belong to the organization.
    /// </summary>
    public virtual async Task<bool> CanManageOrganizationAsync(IResolveFieldContext context, ApplicationUser user, string organizationId)
    {
        if (string.IsNullOrEmpty(organizationId))
        {
            return false;
        }

        // The principal of the request carries the permissions of the current organization (OrganizationIdClaimProvider)
        var authorizationResult = await authorizationService.AuthorizeAsync(context.GetCurrentPrincipal(), resource: null,
            new PermissionAuthorizationRequirement(ModuleConstants.Security.FrontendPermissions.ManageOrganizationMfa));
        if (!authorizationResult.Succeeded)
        {
            return false;
        }

        var currentOrganizationId = context.GetCurrentOrganizationId();
        if (!string.IsNullOrEmpty(currentOrganizationId))
        {
            return currentOrganizationId.EqualsIgnoreCase(organizationId);
        }

        if (string.IsNullOrEmpty(user.MemberId))
        {
            return false;
        }

        var contact = await memberService.GetByIdAsync(user.MemberId) as Contact;

        return contact?.Organizations?.Any(organizationId.EqualsIgnoreCase) == true;
    }
}
