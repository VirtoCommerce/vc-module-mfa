using System.Threading.Tasks;
using VirtoCommerce.MFA.Core.Models;

namespace VirtoCommerce.MFA.Core.Services;

public interface IBackofficeMfaSignInService
{
    Task<MfaResult> SendCodeAsync(string provider = null);

    Task<MfaResult> SignInAsync(string code, string provider = null, bool rememberMe = false);
}
