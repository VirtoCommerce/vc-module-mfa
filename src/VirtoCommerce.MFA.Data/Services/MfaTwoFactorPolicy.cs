using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.MFA.Data.Services;

public class MfaTwoFactorPolicy(IMfaRequirementService requirementService, IOptions<MfaOptions> options, IHttpContextAccessor httpContextAccessor) : ITwoFactorPolicy
{
    public virtual Task<bool> IsTwoFactorRequiredAsync(ApplicationUser user)
    {
        if (options.Value.DisableEnforcement)
        {
            return Task.FromResult(false);
        }

        return requirementService.IsRequiredAsync(user, GetStoreId());
    }

    protected virtual string GetStoreId()
    {
        var request = httpContextAccessor.HttpContext?.GetOpenIddictServerRequest();

        return (string)request?.GetParameter(ModuleConstants.Security.Parameters.StoreId);
    }
}
