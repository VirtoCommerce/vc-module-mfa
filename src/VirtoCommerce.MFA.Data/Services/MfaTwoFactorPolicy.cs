using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.MFA.Data.Services;

public class MfaTwoFactorPolicy(IMfaService mfaService, IHttpContextAccessor httpContextAccessor) : ITwoFactorPolicy
{
    public virtual Task<bool> IsTwoFactorRequiredAsync(ApplicationUser user)
    {
        return mfaService.IsRequiredAsync(user, GetStoreId());
    }

    protected virtual string GetStoreId()
    {
        var request = httpContextAccessor.HttpContext?.GetOpenIddictServerRequest();

        return (string)request?.GetParameter(ModuleConstants.Security.Parameters.StoreId);
    }
}
