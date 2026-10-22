using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.MFA.Core.Services;

public interface IMfaRequirementService
{
    Task<bool> IsRequiredAsync(ApplicationUser user, string storeId);

    Task<bool> IsRequiredForBackofficeAsync(ApplicationUser user);
}
