using System.Threading.Tasks;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.MFA.Core.Services;

public interface IMfaService
{
    Task<bool> IsRequiredAsync(ApplicationUser user, string storeId);

    Task<MfaResult> ValidateUserAsync(ApplicationUser user);

    Task<IMfaProvider> GetProviderAsync(ApplicationUser user, string provider = null);

    Task<MfaResult> SendCodeAsync(ApplicationUser user, string provider = null);

    Task<MfaResult> ChallengeAsync(string storeId, ApplicationUser user, string provider = null);

    Task<MfaResult> ResendCodeAsync(string twoFactorToken, string provider = null);

    Task<MfaResult> VerifyCodeAsync(string storeId, string twoFactorToken, string code);
}
