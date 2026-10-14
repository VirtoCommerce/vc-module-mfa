using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.MFA.Data.Services;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.SignInLog;
using VirtoCommerce.Platform.Security.OpenIddict;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace VirtoCommerce.MFA.Data.OpenIddict;

public class MfaGrantTypeHandler(
    SignInManager<ApplicationUser> signInManager,
    IOptions<IdentityOptions> identityOptions,
    IEnumerable<ITokenRequestValidator> requestValidators,
    IEnumerable<ITokenClaimProvider> claimProviders,
    IEnumerable<ITokenRequestHandler> requestHandlers,
    IEventPublisher eventPublisher,
    IMfaService mfaService,
    MfaErrorDescriber errorDescriber)
    : GrantTypeHandlerBase(signInManager, identityOptions, requestValidators, claimProviders, requestHandlers, eventPublisher)
{
    private string _provider;

    public override string GrantType => ModuleConstants.Security.GrantType;

    protected override string SignInType => ModuleConstants.Security.SignInType;

    protected override Task<TokenResponse> UpdateLastLoginAsync(TokenRequestContext context)
    {
        context.User.AccessFailedCount = 0;

        return base.UpdateLastLoginAsync(context);
    }

    protected override async Task<GrantValidationResult> ValidateGrantAsync(TokenRequestContext context)
    {
        List<string> missingParameters = [];

        var storeId = (string)context.Request.GetParameter(ModuleConstants.Security.Parameters.StoreId);
        var twoFactorToken = GetRequiredParameter(context.Request, ModuleConstants.Security.Parameters.TwoFactorToken, missingParameters);
        var code = GetRequiredParameter(context.Request, ModuleConstants.Security.Parameters.Code, missingParameters);

        if (missingParameters.Count > 0)
        {
            return GrantValidationResult.Fail(BuildErrorResponse(context, missingParameters));
        }

        var verifyResult = await mfaService.VerifyCodeAsync(twoFactorToken, code, storeId);
        _provider = verifyResult.Provider;
        context.SignInResult = GetSignInResult(verifyResult.Outcome);

        if (verifyResult.Outcome != MfaOutcome.Success)
        {
            return GrantValidationResult.Fail(BuildErrorResponse(context, verifyResult), verifyResult.User);
        }

        return GrantValidationResult.Succeed(verifyResult.User);
    }

    protected override UserSignInAttemptEvent BuildSignInAttemptEvent(TokenRequestContext context, bool succeeded)
    {
        var attempt = base.BuildSignInAttemptEvent(context, succeeded);
        attempt.UserName = context.User?.UserName;
        attempt.Provider = _provider;

        return attempt;
    }

    protected virtual string GetRequiredParameter(OpenIddictRequest request, string name, List<string> missingParameters)
    {
        var value = (string)request.GetParameter(name);
        if (string.IsNullOrEmpty(value))
        {
            missingParameters.Add(name);
        }

        return value;
    }

    protected virtual SignInResult GetSignInResult(MfaOutcome outcome)
    {
        return outcome switch
        {
            MfaOutcome.Success => SignInResult.Success,
            MfaOutcome.AccountLocked => SignInResult.LockedOut,
            _ => SignInResult.Failed,
        };
    }

    protected virtual TokenResponse BuildErrorResponse(TokenRequestContext context, List<string> missingParameters)
    {
        context.FailureReason = ModuleConstants.Security.FailureReason.MissingParameter;

        return errorDescriber.ToTokenResponse(errorDescriber.MissingParameters(missingParameters), Errors.InvalidRequest);
    }

    protected virtual TokenResponse BuildErrorResponse(TokenRequestContext context, MfaResult verifyResult)
    {
        context.FailureReason = errorDescriber.GetFailureReason(verifyResult.Outcome);

        return errorDescriber.ToTokenResponse(verifyResult, context.DetailedErrors, errorDescriber.InvalidCode());
    }
}
