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
    /// Sends a code that confirms a change; <see cref="MfaChangeResult.CodeResult"/> carries the two-factor token to pass with the change.
    /// The change is checked before the code is sent: when it would be refused, nothing is sent and the result has the same outcome as the change itself.
    /// </summary>
    /// <param name="organizationId">The organization whose mode is changed, or <c>null</c> for the mode of the user.</param>
    /// <param name="canManageOrganization">Whether the user has the permission to manage the MFA of the organization.</param>
    Task<MfaChangeResult> SendChangeCodeAsync(ApplicationUser user, string storeId, string organizationId, bool canManageOrganization);

    Task<MfaChangeResult> ChangeUserModeAsync(ApplicationUser user, string storeId, bool enabled, string twoFactorToken, string code);

    Task<MfaChangeResult> ChangeOrganizationModeAsync(ApplicationUser user, string storeId, string organizationId, bool canManageOrganization,
        bool enabled, string twoFactorToken, string code);
}
