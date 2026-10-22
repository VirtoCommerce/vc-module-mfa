using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.MFA.Data.Providers;

public class MfaEmailTokenProvider : EmailTokenProvider<ApplicationUser>
{
    public override async Task<string> GetUserModifierAsync(string purpose, UserManager<ApplicationUser> manager, ApplicationUser user)
    {
        var modifier = await base.GetUserModifierAsync(purpose, manager, user);

        if (purpose != ModuleConstants.Security.IdentityTwoFactorPurpose)
        {
            return modifier;
        }

        var codeStamp = await manager.GetAuthenticationTokenAsync(user, ModuleConstants.Security.CodeStampLoginProvider, ModuleConstants.Security.CodeStampName);

        return $"{modifier}:{codeStamp}";
    }
}
