using System.Threading.Tasks;
using VirtoCommerce.MFA.Core.Models;

namespace VirtoCommerce.MFA.Core.Services;

/// <summary>
/// Shows in every store whether the sign-in needs the code, so an administrator sees the effect of a mode before saving it.
/// </summary>
public interface IMfaStatusService
{
    /// <param name="mode">The mode to evaluate instead of the saved one; null for the saved mode.</param>
    /// <returns>Null when the user doesn't exist.</returns>
    Task<MfaStatus> GetUserStatusAsync(string userId, string mode = null);

    /// <param name="mode">The mode to evaluate instead of the saved one; null for the saved mode.</param>
    Task<MfaStatus> GetOrganizationStatusAsync(string organizationId, string mode = null);
}
