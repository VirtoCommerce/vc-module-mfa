using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.MFA.Data.Services;
using VirtoCommerce.Platform.Core.Security.SignInLog;
using VirtoCommerce.Platform.Security.OpenIddict;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace VirtoCommerce.MFA.Data.OpenIddict;

public class MfaTwoFactorSignInHandler(IMfaService mfaService) : ITwoFactorSignInHandler
{
    public virtual async Task<ActionResult> ChallengeAsync(TokenRequestContext context)
    {
        var provider = (string)context.Request.GetParameter(ModuleConstants.Security.Parameters.Provider);
        var result = await mfaService.ChallengeAsync(GetStoreId(context), context.User, provider);

        if (result.Outcome != MfaOutcome.Success)
        {
            return new BadRequestObjectResult(BuildErrorResponse(context, result));
        }

        var error = MfaErrorDescriber.TwoFactorRequired();

        return new BadRequestObjectResult(new TwoFactorRequiredResponse
        {
            Error = Errors.InvalidGrant,
            Code = error.Code,
            ErrorDescription = error.Description,
            TwoFactorToken = result.TwoFactorToken,
            Provider = result.Provider,
            Providers = result.Providers,
            ResendSecondsRemaining = result.ResendSecondsRemaining,
        });
    }

    protected virtual TokenResponse BuildErrorResponse(TokenRequestContext context, MfaResult result)
    {
        context.FailureReason = result.Outcome switch
        {
            MfaOutcome.LockoutDisabled => ModuleConstants.Security.FailureReason.LockoutDisabled,
            MfaOutcome.ProviderNotAvailable => ModuleConstants.Security.FailureReason.ProviderNotAvailable,
            _ => SignInFailureReason.Unknown,
        };

        var error = result.Outcome switch
        {
            MfaOutcome.LockoutDisabled => MfaErrorDescriber.LockoutDisabled(),
            MfaOutcome.ProviderNotAvailable => MfaErrorDescriber.ProviderNotAvailable(),
            _ => MfaErrorDescriber.LoginFailed(),
        };

        return CreateTokenResponse(error);
    }

    protected virtual TokenResponse CreateTokenResponse(IdentityError error)
    {
        return new TokenResponse
        {
            Error = Errors.InvalidGrant,
            Code = error.Code,
            ErrorDescription = error.Description,
        };
    }

    protected virtual string GetStoreId(TokenRequestContext context)
    {
        return (string)context.Request.GetParameter(ModuleConstants.Security.Parameters.StoreId);
    }
}
