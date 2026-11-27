using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VirtoCommerce.MFA.Core.Services;

/// <summary>
/// Keeps user Backoffice exemptions and the MFA modes chosen for users and organizations: one of <see cref="ModuleConstants.Modes"/>.
/// <see cref="ModuleConstants.Modes.Enabled"/> is set by the user or the organization maintainer,
/// <see cref="ModuleConstants.Modes.Required"/> is set by an administrator and can't be changed in the storefront.
/// </summary>
public interface IMfaModeService
{
    Task<string> GetUserModeAsync(string userId);

    Task SetUserModeAsync(string userId, string mode);

    /// <summary>
    /// Whether the user is exempt from mandatory Backoffice MFA. Legacy implementations grant no exemption.
    /// </summary>
    Task<bool> IsBackofficeExemptAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return Task.FromResult(false);
    }

    /// <summary>
    /// Saves the user's Backoffice exemption. Implementations without exemption support reject the change.
    /// </summary>
    Task SetBackofficeExemptionAsync(string userId, bool isExempt)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return Task.FromException(new NotSupportedException("This MFA mode service does not support Backoffice exemptions."));
    }

    Task<string> GetOrganizationModeAsync(string organizationId);

    Task<IDictionary<string, string>> GetOrganizationModesAsync(IList<string> organizationIds);

    Task SetOrganizationModeAsync(string organizationId, string mode);
}
