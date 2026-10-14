using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.MFA.Data.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Security.OpenIddict;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace VirtoCommerce.MFA.Data.OpenIddict;

public class MfaTwoFactorSignInHandler(IMfaService mfaService, MfaErrorDescriber errorDescriber) : ITwoFactorSignInHandler
{
    public virtual async Task<ActionResult> ChallengeAsync(TokenRequestContext context)
    {
        var provider = (string)context.Request.GetParameter(ModuleConstants.Security.Parameters.Provider);
        var result = await mfaService.ChallengeAsync(context.User, GetStoreId(context), provider);

        if (result.Outcome != MfaOutcome.Success)
        {
            return new BadRequestObjectResult(BuildErrorResponse(context, result));
        }

        var error = errorDescriber.TwoFactorRequired();
        var response = AbstractTypeFactory<TwoFactorRequiredResponse>.TryCreateInstance();
        response.Error = Errors.InvalidGrant;
        response.Code = error.Code;
        response.ErrorDescription = error.Description;
        response.TwoFactorToken = result.TwoFactorToken;
        response.Provider = result.Provider;
        response.Providers = result.Providers;
        response.ResendSecondsRemaining = result.ResendSecondsRemaining;

        return new BadRequestObjectResult(response);
    }

    protected virtual TokenResponse BuildErrorResponse(TokenRequestContext context, MfaResult result)
    {
        context.FailureReason = errorDescriber.GetFailureReason(result.Outcome);

        var error = result.Outcome == MfaOutcome.AccountLocked && !context.DetailedErrors
            ? errorDescriber.LoginFailed()
            : errorDescriber.GetError(result.Outcome, result.User);

        return errorDescriber.ToTokenResponse(error);
    }

    protected virtual string GetStoreId(TokenRequestContext context)
    {
        return (string)context.Request.GetParameter(ModuleConstants.Security.Parameters.StoreId);
    }
}
