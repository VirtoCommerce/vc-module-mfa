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
    UserManager<ApplicationUser> userManager)
    : GrantTypeHandlerBase(signInManager, identityOptions, requestValidators, claimProviders, requestHandlers, eventPublisher)
{
    public override string GrantType => ModuleConstants.Security.GrantType;

    protected override string SignInType => ModuleConstants.Security.SignInType;

    protected override async Task AfterSignInAsync(TokenRequestContext context)
    {
        await userManager.ResetAccessFailedCountAsync(context.User);
        await base.AfterSignInAsync(context);
    }

    protected override async Task<GrantValidationResult> ValidateGrantAsync(TokenRequestContext context)
    {
        var missingParameters = new List<string>();

        var storeId = (string)context.Request.GetParameter(ModuleConstants.Security.Parameters.StoreId);
        var twoFactorToken = GetRequiredParameter(context.Request, ModuleConstants.Security.Parameters.TwoFactorToken, missingParameters);
        var code = GetRequiredParameter(context.Request, ModuleConstants.Security.Parameters.Code, missingParameters);

        if (missingParameters.Count > 0)
        {
            return GrantValidationResult.Fail(BuildErrorResponse(context, missingParameters));
        }

        var verifyResult = await mfaService.VerifyCodeAsync(storeId, twoFactorToken, code);
        context.SignInResult = GetSignInResult(verifyResult.Outcome);

        if (verifyResult.Outcome != MfaOutcome.Success)
        {
            return GrantValidationResult.Fail(BuildErrorResponse(context, verifyResult), verifyResult.User);
        }

        return GrantValidationResult.Succeed(verifyResult.User);
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

        return CreateTokenResponse(Errors.InvalidRequest, MfaErrorDescriber.MissingParameters(missingParameters));
    }

    protected virtual TokenResponse BuildErrorResponse(TokenRequestContext context, MfaResult verifyResult)
    {
        context.FailureReason = verifyResult.Outcome switch
        {
            MfaOutcome.InvalidTwoFactorToken => ModuleConstants.Security.FailureReason.InvalidTwoFactorToken,
            MfaOutcome.LockoutDisabled => ModuleConstants.Security.FailureReason.LockoutDisabled,
            MfaOutcome.ProviderNotAvailable => ModuleConstants.Security.FailureReason.ProviderNotAvailable,
            MfaOutcome.AccountLocked => SignInFailureReason.LockedOut,
            MfaOutcome.InvalidCode => ModuleConstants.Security.FailureReason.InvalidCode,
            _ => SignInFailureReason.Unknown,
        };

        return verifyResult.Outcome switch
        {
            MfaOutcome.InvalidTwoFactorToken => CreateTokenResponse(Errors.InvalidGrant, MfaErrorDescriber.InvalidTwoFactorToken()),
            MfaOutcome.LockoutDisabled => CreateTokenResponse(Errors.InvalidGrant, MfaErrorDescriber.LockoutDisabled()),
            MfaOutcome.ProviderNotAvailable => CreateTokenResponse(Errors.InvalidGrant, MfaErrorDescriber.ProviderNotAvailable()),
            MfaOutcome.AccountLocked when context.DetailedErrors => CreateTokenResponse(Errors.InvalidGrant, MfaErrorDescriber.GetLockoutError(verifyResult.User), MfaErrorDescriber.GetLockoutSecondsRemaining(verifyResult.User)),
            _ => CreateTokenResponse(Errors.InvalidGrant, MfaErrorDescriber.InvalidCode()),
        };
    }

    protected virtual TokenResponse CreateTokenResponse(string error, IdentityError identityError, int? lockoutSecondsRemaining = null)
    {
        return new TokenResponse
        {
            Error = error,
            Code = identityError.Code,
            ErrorDescription = identityError.Description,
            LockoutSecondsRemaining = lockoutSecondsRemaining,
        };
    }
}
