using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.MFA.Data.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.MFA.Web.Controllers.Api;

[ApiController]
[Route("api/mfa")]
[AllowAnonymous]
public class MfaController(
    IMfaService mfaService,
    IBackofficeMfaSignInService backofficeSignInService,
    IOptions<PasswordLoginOptions> passwordLoginOptions,
    MfaErrorDescriber errorDescriber)
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

        var signInResult = AbstractTypeFactory<MfaSignInResult>.TryCreateInstance();
        FillSignInResult(signInResult, result, reportLockout: true);

        return Ok(signInResult);
    }

    protected virtual MfaSendCodeResult ToSendCodeResult(MfaResult result, bool reportLockout)
    {
        var sendCodeResult = AbstractTypeFactory<MfaSendCodeResult>.TryCreateInstance();
        FillSignInResult(sendCodeResult, result, reportLockout);
        sendCodeResult.TwoFactorToken = result.TwoFactorToken;
        sendCodeResult.Provider = result.Provider;
        sendCodeResult.Providers = result.Providers;
        sendCodeResult.ResendSecondsRemaining = result.ResendSecondsRemaining;

        return sendCodeResult;
    }

    protected virtual void FillSignInResult(MfaSignInResult signInResult, MfaResult result, bool reportLockout)
    {
        var isLockoutReported = reportLockout && result.Outcome == MfaOutcome.AccountLocked;

        signInResult.Error = GetError(result, reportLockout);
        signInResult.LockoutSecondsRemaining = isLockoutReported ? errorDescriber.GetLockoutSecondsRemaining(result.User) : null;
    }

    protected virtual IdentityError GetError(MfaResult result, bool reportLockout)
    {
        return result.Outcome == MfaOutcome.AccountLocked && !reportLockout
            ? errorDescriber.LoginFailed()
            : errorDescriber.GetError(result.Outcome, result.User);
    }
}
