using System.Security.Claims;
using System.Threading.Tasks;
using OpenIddict.Abstractions;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.Platform.Security.Extensions;
using VirtoCommerce.Platform.Security.OpenIddict;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace VirtoCommerce.MFA.Data.OpenIddict;

public class MfaAuthenticationMethodClaimProvider : ITokenClaimProvider
{
    public virtual Task SetClaimsAsync(ClaimsPrincipal principal, TokenRequestContext context)
    {
        if (IsMfaSignIn(context))
        {
            principal.SetClaimWithDestinations(Claims.AuthenticationMethodReference, ModuleConstants.Security.AuthenticationMethod,
                [Destinations.AccessToken, Destinations.IdentityToken]);
        }

        return Task.CompletedTask;
    }

    protected virtual bool IsMfaSignIn(TokenRequestContext context)
    {
        return context.Request.GrantType == ModuleConstants.Security.GrantType ||
            context.Request.IsRefreshTokenGrantType() &&
            context.Principal?.HasClaim(Claims.AuthenticationMethodReference, ModuleConstants.Security.AuthenticationMethod) == true;
    }
}
