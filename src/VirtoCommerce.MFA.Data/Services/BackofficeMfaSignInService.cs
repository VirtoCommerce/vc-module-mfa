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
    IMfaCodeAttemptLimiter codeAttemptLimiter,
    IMfaUsedCodeStore usedCodeStore,
    IEventPublisher eventPublisher,
    MfaErrorDescriber errorDescriber)
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
            return await FailAsync(MfaResult.Fail(MfaOutcome.SignInNotAllowed, user, provider));
        }

        var result = await mfaService.SendCodeAsync(user, provider);
        if (result.Outcome is MfaOutcome.Success or MfaOutcome.ResendTooSoon)
        {
            return result;
        }

        return await FailAsync(result);
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
            return await FailAsync(MfaResult.Fail(MfaOutcome.SignInNotAllowed, user, provider));
        }

        var validation = await mfaService.ValidateUserAsync(user);
        if (validation.Outcome != MfaOutcome.Success)
        {
            return await FailAsync(validation);
        }

        var mfaProvider = await mfaService.GetProviderAsync(user, provider);
        if (mfaProvider == null)
        {
            return await FailAsync(MfaResult.Fail(MfaOutcome.ProviderNotAvailable, user, provider));
        }

        if (!await codeAttemptLimiter.TryConsumeAttemptAsync(user.Id))
        {
            return await FailAsync(MfaResult.Fail(MfaOutcome.CodeAttemptsExceeded, user, mfaProvider.Name));
        }

        if (!await usedCodeStore.TryUseAsync(user.Id, mfaProvider.Name, code))
        {
            return await FailAsync(MfaResult.Fail(MfaOutcome.InvalidCode, user, mfaProvider.Name));
        }

        var signInResult = await signInManager.TwoFactorSignInAsync(mfaProvider.Name, code, rememberMe, rememberClient: false);
        if (!signInResult.Succeeded)
        {
            await usedCodeStore.ReleaseAsync(user.Id, mfaProvider.Name, code);
        }

        if (signInResult.IsLockedOut)
        {
            return await FailAsync(MfaResult.Fail(MfaOutcome.AccountLocked, await UserManager.FindByIdAsync(user.Id), mfaProvider.Name));
        }

        if (signInResult.IsNotAllowed)
        {
            return await FailAsync(MfaResult.Fail(MfaOutcome.SignInNotAllowed, user, mfaProvider.Name));
        }

        if (!signInResult.Succeeded)
        {
            return await FailAsync(MfaResult.Fail(MfaOutcome.InvalidCode, user, mfaProvider.Name));
        }

        await codeAttemptLimiter.ResetAsync(user.Id);

        user = await UserManager.FindByIdAsync(user.Id);

        try
        {
            user.LastLoginDate = DateTime.UtcNow;
            await UserManager.UpdateAsync(user);
        }
        catch (DuplicateEmailException)
        {
            await signInManager.SignOutAsync();
            return await FailAsync(MfaResult.Fail(MfaOutcome.DuplicateEmail, user, mfaProvider.Name));
        }

        await eventPublisher.Publish(new UserLoginEvent(user));

        var result = MfaResult.Succeed(user, mfaProvider.Name);
        await PublishSignInAttemptAsync(result);

        return result;
    }

    protected virtual Task<bool> IsBackofficeAccessDeniedAsync(ApplicationUser user)
    {
        return UserManager.IsInRoleAsync(user, PlatformConstants.Security.SystemRoles.Customer);
    }

    protected virtual async Task<MfaResult> FailAsync(MfaResult result)
    {
        await PublishSignInAttemptAsync(result);

        return result;
    }

    protected virtual Task PublishSignInAttemptAsync(MfaResult result)
    {
        var isSucceeded = result.Outcome == MfaOutcome.Success;
        var attempt = AbstractTypeFactory<UserSignInAttemptEvent>.TryCreateInstance();

        attempt.UserName = result.User.UserName;
        attempt.UserId = result.User.Id;
        attempt.Succeeded = isSucceeded;
        attempt.FailureReason = isSucceeded ? null : errorDescriber.GetFailureReason(result.Outcome);
        attempt.SignInType = ModuleConstants.Security.SignInType;
        attempt.Provider = result.Provider;
        attempt.StoreId = result.User.StoreId;
        attempt.MemberId = result.User.MemberId;

        return eventPublisher.Publish(attempt);
    }
}
