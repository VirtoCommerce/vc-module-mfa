using System.Collections.Generic;
using System.Threading.Tasks;

namespace VirtoCommerce.MFA.Core.Services;

/// <summary>
/// Keeps the MFA modes chosen for users and organizations: one of <see cref="ModuleConstants.Modes"/>.
/// <see cref="ModuleConstants.Modes.Enabled"/> is set by the user or the organization maintainer,
/// <see cref="ModuleConstants.Modes.Required"/> is set by an administrator and can't be changed in the storefront.
/// </summary>
public interface IMfaModeService
{
    Task<string> GetUserModeAsync(string userId);

    Task SetUserModeAsync(string userId, string mode);

    Task<string> GetOrganizationModeAsync(string organizationId);

    Task<IDictionary<string, string>> GetOrganizationModesAsync(IList<string> organizationIds);

    Task SetOrganizationModeAsync(string organizationId, string mode);
}
