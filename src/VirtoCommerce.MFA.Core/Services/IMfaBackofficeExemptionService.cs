using System.Threading.Tasks;

namespace VirtoCommerce.MFA.Core.Services;

public interface IMfaBackofficeExemptionService
{
    Task<bool> IsExemptAsync(string userId);

    Task SetExemptionAsync(string userId, bool isExempt);
}
