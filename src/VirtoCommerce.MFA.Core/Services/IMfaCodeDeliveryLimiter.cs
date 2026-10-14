using System.Threading.Tasks;
using VirtoCommerce.MFA.Core.Models;

namespace VirtoCommerce.MFA.Core.Services;

public interface IMfaCodeDeliveryLimiter
{
    Task<MfaCodeDeliveryResult> TryAcquireAsync(string key);

    Task ReleaseAsync(string key);
}
