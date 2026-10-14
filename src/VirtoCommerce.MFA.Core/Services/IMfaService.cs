using System.Threading.Tasks;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.MFA.Core.Services;

public interface IMfaService
{
    Task<MfaResult> ValidateUserAsync(ApplicationUser user);

    Task<IMfaProvider> GetProviderAsync(ApplicationUser user, string provider = null);

    Task<MfaResult> SendCodeAsync(ApplicationUser user, string provider = null);

    Task<MfaResult> ChallengeAsync(ApplicationUser user, string storeId, string provider = null);

    Task<MfaResult> ResendCodeAsync(string twoFactorToken, string provider = null);

    Task<MfaResult> VerifyCodeAsync(string twoFactorToken, string code, string storeId);
}
