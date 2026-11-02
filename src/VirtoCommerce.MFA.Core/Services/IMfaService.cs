using System.Threading.Tasks;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.MFA.Core.Services;

public interface IMfaService
{
    Task<MfaResult> ValidateUserAsync(ApplicationUser user);

    Task<IMfaProvider> GetProviderAsync(ApplicationUser user, string provider = null);

    Task<MfaResult> SendCodeAsync(ApplicationUser user, string provider = null);

    /// <param name="purpose">Null for a sign-in, <see cref="ModuleConstants.Security.ChangeTicketPurpose"/> for confirming a change of the MFA mode.</param>
    Task<MfaResult> ChallengeAsync(ApplicationUser user, string storeId, string provider = null, string purpose = null);

    Task<MfaResult> ResendCodeAsync(string twoFactorToken, string provider = null);

    /// <param name="purpose">Must match the purpose the ticket was issued for.</param>
    Task<MfaResult> VerifyCodeAsync(string twoFactorToken, string code, string storeId, string purpose = null);

    Task<bool> AcceptCodeAsync(ApplicationUser user, IMfaProvider mfaProvider);
}
