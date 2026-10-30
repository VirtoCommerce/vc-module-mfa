using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.MFA.Data.Services;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.SignInLog;
using VirtoCommerce.Platform.Security;
using VirtoCommerce.Platform.Security.Extensions;
using VirtoCommerce.Platform.Security.OpenIddict;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static VirtoCommerce.Platform.Core.Security.SignInLog.SignInType;

namespace VirtoCommerce.MFA.Data.OpenIddict;

public class MfaGrantTypeHandler(
    SignInManager<ApplicationUser> signInManager,
    IOptions<IdentityOptions> identityOptions,
    IEnumerable<ITokenRequestValidator> requestValidators,
    IEnumerable<ITokenClaimProvider> claimProviders,
    IEnumerable<ITokenRequestHandler> requestHandlers,
    IEventPublisher eventPublisher,
    ITwoFactorSignInService twoFactorSignInService,
    IMfaService mfaService,
    MfaErrorDescriber errorDescriber)
    : GrantTypeHandlerBase(signInManager, identityOptions, requestValidators, claimProviders, requestHandlers, eventPublisher, twoFactorSignInService)
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

    protected override Task<bool> IsTwoFactorRequiredAsync(TokenRequestContext context)
    {
        return Task.FromResult(false);
    }

    protected override async Task<AuthenticationTicket> CreateTicketAsync(TokenRequestContext context)
    {
        var ticket = await base.CreateTicketAsync(context);

        // Two-factor sign-in only follows a password sign-in: every other sign-in bypasses it
        ticket.Principal.SetAuthenticationMethod(Password, [Destinations.AccessToken]);

        return ticket;
    }

    protected override UserSignInAttemptEvent BuildSignInAttemptEvent(TokenRequestContext context, bool succeeded)
    {
        var attempt = base.BuildSignInAttemptEvent(context, succeeded);
        attempt.UserName = context.User?.UserName;
        attempt.Provider = _provider;

        return attempt;
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

    protected virtual TokenResponse BuildErrorResponse(TokenRequestContext context, MfaResult verifyResult)
    {
        context.FailureReason = errorDescriber.GetFailureReason(verifyResult.Outcome);

        return errorDescriber.ToTokenResponse(verifyResult, context.DetailedErrors, errorDescriber.InvalidCode());
    }
}
