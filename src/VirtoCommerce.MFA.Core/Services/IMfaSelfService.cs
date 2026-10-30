using System.Threading.Tasks;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.MFA.Core.Services;

/// <summary>
/// Lets a storefront user turn MFA on or off for their account, and an organization maintainer for the organization,
/// when the store mode is <see cref="ModuleConstants.StoreModes.CustomerChoice"/>. Every change is confirmed with a code.
/// </summary>
public interface IMfaSelfService
{
    /// <param name="canManageOrganization">Whether the user has the permission to manage the MFA of the organization.</param>
    Task<MfaSettings> GetSettingsAsync(ApplicationUser user, string storeId, string organizationId, bool canManageOrganization);

    /// <summary>
    /// Sends a code that confirms a change; the result carries the two-factor token to pass with the change.
    /// </summary>
    Task<MfaResult> SendChangeCodeAsync(ApplicationUser user, string storeId);

    Task<MfaChangeResult> ChangeUserModeAsync(ApplicationUser user, string storeId, bool enabled, string twoFactorToken, string code);

    Task<MfaChangeResult> ChangeOrganizationModeAsync(ApplicationUser user, string storeId, string organizationId, bool canManageOrganization,
        bool enabled, string twoFactorToken, string code);
}
