using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.MFA.Core.Services;

public interface IMfaRequirementService
{
    Task<bool> IsRequiredAsync(ApplicationUser user, string storeId);

    Task<bool> IsRequiredForBackofficeAsync(ApplicationUser user);

    /// <summary>
    /// Tells whether the password sign-in of the user to the store requires the code and what requires it.
    /// </summary>
    Task<MfaRequirement> GetRequirementAsync(ApplicationUser user, string storeId);

    /// <summary>
    /// Applies the same rules to the given modes instead of reading them.
    /// </summary>
    /// <param name="storeMode">One of <see cref="ModuleConstants.StoreModes"/>.</param>
    /// <param name="organizationModes">The modes of the user's organizations.</param>
    /// <param name="userMode">The mode of the user; null leaves the choice to each member of the organizations.</param>
    Task<MfaRequirement> GetRequirementAsync(string storeMode, IEnumerable<string> organizationModes, string userMode);

    /// <summary>
    /// Returns the organizations of the user's contact: their modes apply to the user.
    /// </summary>
    Task<IList<string>> GetOrganizationIdsAsync(ApplicationUser user);

    /// <summary>
    /// Returns the MFA mode of the store: one of <see cref="ModuleConstants.StoreModes"/>.
    /// </summary>
    Task<string> GetStoreModeAsync(string storeId);
}
