using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Events;
using VirtoCommerce.Platform.Core.Security.SignInLog;
using VirtoCommerce.Platform.Security.Exceptions;

namespace VirtoCommerce.MFA.Data.Services;

public class BackofficeMfaSignInService(
    SignInManager<ApplicationUser> signInManager,
    IMfaService mfaService,
    IEventPublisher eventPublisher)
    : IBackofficeMfaSignInService
{
    private UserManager<ApplicationUser> UserManager => signInManager.UserManager;

    public virtual async Task<MfaResult> SendCodeAsync(string provider = null)
    {
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user == null)
        {
            return MfaResult.Fail(MfaOutcome.InvalidTwoFactorToken);
        }

        if (await IsBackofficeAccessDeniedAsync(user))
        {
            return MfaResult.Fail(MfaOutcome.SignInNotAllowed, user);
        }

        return await mfaService.SendCodeAsync(user, provider);
    }

    public virtual async Task<MfaResult> SignInAsync(string code, string provider = null, bool rememberMe = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);

        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user == null)
        {
            return MfaResult.Fail(MfaOutcome.InvalidTwoFactorToken);
        }

        if (await IsBackofficeAccessDeniedAsync(user))
        {
            return await FailAsync(MfaOutcome.SignInNotAllowed, user, SignInFailureReason.NotAllowed);
        }

        var validation = await mfaService.ValidateUserAsync(user);
        if (validation.Outcome != MfaOutcome.Success)
        {
            return await FailAsync(validation.Outcome, user, GetFailureReason(validation.Outcome));
        }

        var mfaProvider = await mfaService.GetProviderAsync(user, provider);
        if (mfaProvider == null)
        {
            return await FailAsync(MfaOutcome.ProviderNotAvailable, user, ModuleConstants.Security.FailureReason.ProviderNotAvailable);
        }

        var signInResult = await signInManager.TwoFactorSignInAsync(mfaProvider.Name, code, rememberMe, rememberClient: false);

        if (signInResult.IsLockedOut)
        {
            return await FailAsync(MfaOutcome.AccountLocked, await UserManager.FindByIdAsync(user.Id), SignInFailureReason.LockedOut);
        }

        if (signInResult.IsNotAllowed)
        {
            return await FailAsync(MfaOutcome.SignInNotAllowed, user, SignInFailureReason.NotAllowed);
        }

        if (!signInResult.Succeeded)
        {
            return await FailAsync(MfaOutcome.InvalidCode, user, ModuleConstants.Security.FailureReason.InvalidCode);
        }

        user = await UserManager.FindByIdAsync(user.Id);

        try
        {
            user.LastLoginDate = DateTime.UtcNow;
            await UserManager.UpdateAsync(user);
        }
        catch (DuplicateEmailException)
        {
            await signInManager.SignOutAsync();
            return await FailAsync(MfaOutcome.DuplicateEmail, user, SignInFailureReason.DuplicateEmail);
        }

        await eventPublisher.Publish(new UserLoginEvent(user));
        await PublishSignInAttemptAsync(user, succeeded: true, failureReason: null);

        return MfaResult.Succeed(user);
    }

    protected virtual Task<bool> IsBackofficeAccessDeniedAsync(ApplicationUser user)
    {
        return UserManager.IsInRoleAsync(user, PlatformConstants.Security.SystemRoles.Customer);
    }

    protected virtual string GetFailureReason(MfaOutcome outcome)
    {
        return outcome switch
        {
            MfaOutcome.LockoutDisabled => ModuleConstants.Security.FailureReason.LockoutDisabled,
            MfaOutcome.AccountLocked => SignInFailureReason.LockedOut,
            _ => SignInFailureReason.Unknown,
        };
    }

    protected virtual async Task<MfaResult> FailAsync(MfaOutcome outcome, ApplicationUser user, string failureReason)
    {
        await PublishSignInAttemptAsync(user, succeeded: false, failureReason);

        return MfaResult.Fail(outcome, user);
    }

    protected virtual Task PublishSignInAttemptAsync(ApplicationUser user, bool succeeded, string failureReason)
    {
        var attempt = AbstractTypeFactory<UserSignInAttemptEvent>.TryCreateInstance();

        attempt.UserName = user.UserName;
        attempt.UserId = user.Id;
        attempt.Succeeded = succeeded;
        attempt.FailureReason = failureReason;
        attempt.SignInType = ModuleConstants.Security.SignInType;
        attempt.StoreId = user.StoreId;
        attempt.MemberId = user.MemberId;

        return eventPublisher.Publish(attempt);
    }
}
