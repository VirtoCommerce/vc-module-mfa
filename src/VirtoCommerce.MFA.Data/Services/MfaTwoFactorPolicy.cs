using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.MFA.Data.Services;

public class MfaTwoFactorPolicy(IMfaRequirementService requirementService, IOptions<MfaOptions> options) : ITwoFactorPolicy
{
    public virtual Task<bool> IsTwoFactorRequiredAsync(TwoFactorPolicyContext context)
    {
        if (options.Value.DisableEnforcement)
        {
            return Task.FromResult(false);
        }

        return requirementService.IsRequiredAsync(context.User, GetStoreId(context));
    }

    protected virtual string GetStoreId(TwoFactorPolicyContext context)
    {
        return context.Parameters.TryGetValue(ModuleConstants.Security.Parameters.StoreId, out var storeId) ? storeId : null;
    }
}
