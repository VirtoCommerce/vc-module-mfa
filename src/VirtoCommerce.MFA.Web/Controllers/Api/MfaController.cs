using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.MFA.Data.Services;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.MFA.Web.Controllers.Api;

[ApiController]
[Route("api/mfa")]
[AllowAnonymous]
public class MfaController(
    IMfaService mfaService,
    IBackofficeMfaSignInService backofficeSignInService,
    IOptions<PasswordLoginOptions> passwordLoginOptions)
    : Controller
{
    private readonly PasswordLoginOptions _passwordLoginOptions = passwordLoginOptions.Value;

    [HttpPost]
    [Route("resend")]
    public async Task<ActionResult<MfaSendCodeResult>> ResendCode([FromBody] MfaResendCodeRequest request)
    {
        var result = await mfaService.ResendCodeAsync(request.TwoFactorToken, request.Provider);

        return Ok(ToSendCodeResult(result, reportLockout: _passwordLoginOptions.DetailedErrors));
    }

    [HttpPost]
    [Route("signin/send")]
    public async Task<ActionResult<MfaSendCodeResult>> SendSignInCode([FromBody] MfaSendCodeRequest request)
    {
        var result = await backofficeSignInService.SendCodeAsync(request.Provider);

        return Ok(ToSendCodeResult(result, reportLockout: true));
    }

    [HttpPost]
    [Route("signin/verify")]
    public async Task<ActionResult<MfaSignInResult>> VerifySignInCode([FromBody] MfaSignInRequest request)
    {
        var result = await backofficeSignInService.SignInAsync(request.Code, request.Provider, request.RememberMe);
        var isLockedOut = result.Outcome == MfaOutcome.AccountLocked;

        return Ok(new MfaSignInResult
        {
            Error = GetError(result, reportLockout: true),
            LockoutSecondsRemaining = isLockedOut ? MfaErrorDescriber.GetLockoutSecondsRemaining(result.User) : null,
        });
    }

    private static MfaSendCodeResult ToSendCodeResult(MfaResult result, bool reportLockout)
    {
        var isLockoutReported = reportLockout && result.Outcome == MfaOutcome.AccountLocked;

        return new MfaSendCodeResult
        {
            Error = GetError(result, reportLockout),
            TwoFactorToken = result.TwoFactorToken,
            Provider = result.Provider,
            Providers = result.Providers,
            LockoutSecondsRemaining = isLockoutReported ? MfaErrorDescriber.GetLockoutSecondsRemaining(result.User) : null,
            ResendSecondsRemaining = result.ResendSecondsRemaining,
        };
    }

    private static IdentityError GetError(MfaResult result, bool reportLockout)
    {
        return result.Outcome switch
        {
            MfaOutcome.Success => null,
            MfaOutcome.InvalidTwoFactorToken => MfaErrorDescriber.InvalidTwoFactorToken(),
            MfaOutcome.InvalidCode => MfaErrorDescriber.InvalidCode(),
            MfaOutcome.SignInNotAllowed => MfaErrorDescriber.SignInNotAllowed(),
            MfaOutcome.AccountLocked when reportLockout => MfaErrorDescriber.GetLockoutError(result.User),
            MfaOutcome.LockoutDisabled => MfaErrorDescriber.LockoutDisabled(),
            MfaOutcome.ProviderNotAvailable => MfaErrorDescriber.ProviderNotAvailable(),
            MfaOutcome.ResendTooSoon => MfaErrorDescriber.ResendTooSoon(),
            _ => MfaErrorDescriber.LoginFailed(),
        };
    }
}
