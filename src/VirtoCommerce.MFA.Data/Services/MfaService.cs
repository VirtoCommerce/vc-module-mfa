using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Security.Exceptions;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.MFA.Data.Services;

public class MfaService(
    UserManager<ApplicationUser> userManager,
    IStoreService storeService,
    ITwoFactorTokenService twoFactorTokenService,
    ITwoFactorTicketStore ticketStore,
    IEnumerable<IMfaProvider> providers,
    IMfaCodeAttemptLimiter codeAttemptLimiter,
    IMfaCodeDeliveryLimiter codeDeliveryLimiter,
    IOptions<MfaOptions> options,
    TimeProvider timeProvider)
    : IMfaService
{
    private readonly IList<IMfaProvider> _providers = providers.ToList();

    public virtual async Task<MfaResult> SendCodeAsync(ApplicationUser user, string provider = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        var result = await ValidateUserAsync(user);
        if (result.Outcome != MfaOutcome.Success)
        {
            return result;
        }

        return await DeliverCodeAsync(store: null, user, provider);
    }

    public virtual async Task<MfaResult> ChallengeAsync(ApplicationUser user, string storeId, string provider = null, string purpose = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        var result = await ValidateUserAsync(user);
        if (result.Outcome != MfaOutcome.Success)
        {
            return result;
        }

        var ticket = AbstractTypeFactory<TwoFactorTicket>.TryCreateInstance();
        ticket.Id = Guid.NewGuid().ToString("N");
        ticket.UserId = user.Id;
        ticket.StoreId = storeId.EmptyToNull();
        ticket.IssuedAt = timeProvider.GetUtcNow();
        ticket.LastLoginDate = user.LastLoginDate;
        ticket.SecurityStamp = await userManager.GetSecurityStampAsync(user);
        ticket.Purpose = purpose.EmptyToNull();

        var store = await GetStoreAsync(ticket.StoreId);
        var deliveryResult = await DeliverCodeAsync(store, user, provider);

        if (deliveryResult.Outcome == MfaOutcome.ResendTooSoon)
        {
            deliveryResult.Outcome = MfaOutcome.Success;
        }

        return IssueTwoFactorToken(ticket, deliveryResult);
    }

    public virtual async Task<MfaResult> ResendCodeAsync(string twoFactorToken, string provider = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(twoFactorToken);

        var ticket = twoFactorTokenService.ReadToken(twoFactorToken);

        var result = await ValidateTicketAsync(ticket);
        if (result.Outcome != MfaOutcome.Success)
        {
            return result;
        }

        var store = await GetStoreAsync(ticket.StoreId);
        var deliveryResult = await DeliverCodeAsync(store, result.User, provider);

        return IssueTwoFactorToken(ticket, deliveryResult);
    }

    public virtual async Task<MfaResult> VerifyCodeAsync(string twoFactorToken, string code, string storeId, string purpose = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(twoFactorToken);
        ArgumentException.ThrowIfNullOrEmpty(code);

        var ticket = twoFactorTokenService.ReadToken(twoFactorToken);

        var result = await ValidateTicketAsync(ticket);
        if (result.Outcome != MfaOutcome.Success)
        {
            return result;
        }

        var user = result.User;

        if (!ticket.StoreId.EqualsIgnoreCase(storeId.EmptyToNull()) ||
            !ticket.Purpose.EqualsIgnoreCase(purpose.EmptyToNull()))
        {
            return MfaResult.Fail(MfaOutcome.InvalidTwoFactorToken, user, ticket.Provider);
        }

        var mfaProvider = await GetProviderAsync(user, ticket.Provider);
        if (mfaProvider == null)
        {
            return MfaResult.Fail(MfaOutcome.ProviderNotAvailable, user, ticket.Provider);
        }

        if (!await codeAttemptLimiter.TryConsumeAttemptAsync(user.Id))
        {
            return MfaResult.Fail(MfaOutcome.CodeAttemptsExceeded, user, mfaProvider.Name);
        }

        if (!await userManager.VerifyTwoFactorTokenAsync(user, mfaProvider.Name, code))
        {
            return await FailCodeAsync(ticket, user, mfaProvider);
        }

        if (!await AcceptCodeAsync(user, mfaProvider))
        {
            return MfaResult.Fail(MfaOutcome.AcceptedCodeNotRecorded, user, mfaProvider.Name);
        }

        if (!await ticketStore.TryRedeemAsync(ticket))
        {
            return MfaResult.Fail(MfaOutcome.InvalidTwoFactorToken, user, mfaProvider.Name);
        }

        await codeAttemptLimiter.ResetAsync(user.Id);

        return MfaResult.Succeed(user, mfaProvider.Name);
    }

    public virtual async Task<bool> AcceptCodeAsync(ApplicationUser user, IMfaProvider mfaProvider)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(mfaProvider);

        if (!await UpdateCodeStampAsync(user))
        {
            return false;
        }

        await codeDeliveryLimiter.ReleaseAsync(GetCodeDeliveryKey(user, mfaProvider));

        return true;
    }

    public virtual async Task<MfaResult> ValidateUserAsync(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!await userManager.GetLockoutEnabledAsync(user))
        {
            return MfaResult.Fail(MfaOutcome.LockoutDisabled, user);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return MfaResult.Fail(MfaOutcome.AccountLocked, user);
        }

        if (!await codeAttemptLimiter.HasAttemptsLeftAsync(user.Id))
        {
            return MfaResult.Fail(MfaOutcome.CodeAttemptsExceeded, user);
        }

        return MfaResult.Succeed(user);
    }

    public virtual async Task<IMfaProvider> GetProviderAsync(ApplicationUser user, string provider = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        return SelectProvider(await GetAvailableProvidersAsync(user), provider);
    }

    protected virtual async Task<MfaResult> FailCodeAsync(TwoFactorTicket ticket, ApplicationUser user, IMfaProvider mfaProvider)
    {
        var accessFailedResult = await userManager.AccessFailedAsync(user);
        if (!accessFailedResult.Succeeded)
        {
            await ticketStore.RevokeAsync(ticket);
            return MfaResult.Fail(MfaOutcome.FailedAttemptNotRecorded, user, mfaProvider.Name);
        }

        return await userManager.IsLockedOutAsync(user)
            ? MfaResult.Fail(MfaOutcome.AccountLocked, user, mfaProvider.Name)
            : MfaResult.Fail(MfaOutcome.InvalidCode, user, mfaProvider.Name);
    }

    protected virtual async Task<MfaResult> ValidateTicketAsync(TwoFactorTicket ticket)
    {
        if (ticket == null)
        {
            return MfaResult.Fail(MfaOutcome.InvalidTwoFactorToken);
        }

        var user = await userManager.FindByIdAsync(ticket.UserId);
        if (user == null)
        {
            return MfaResult.Fail(MfaOutcome.InvalidTwoFactorToken);
        }

        if (IsTicketExpired(ticket) ||
            ticket.SecurityStamp != await userManager.GetSecurityStampAsync(user) ||
            await IsTicketUsedAsync(ticket, user))
        {
            return MfaResult.Fail(MfaOutcome.InvalidTwoFactorToken, user, ticket.Provider);
        }

        var result = await ValidateUserAsync(user);

        return result.Outcome == MfaOutcome.Success
            ? result
            : MfaResult.Fail(result.Outcome, user, ticket.Provider);
    }

    protected virtual bool IsTicketExpired(TwoFactorTicket ticket)
    {
        return timeProvider.GetUtcNow() >= ticket.IssuedAt + options.Value.TwoFactorTokenLifetime;
    }

    protected virtual async Task<bool> IsTicketUsedAsync(TwoFactorTicket ticket, ApplicationUser user)
    {
        return user.LastLoginDate != ticket.LastLoginDate || await ticketStore.IsRedeemedAsync(ticket);
    }

    protected virtual MfaResult IssueTwoFactorToken(TwoFactorTicket ticket, MfaResult result)
    {
        if (result.Outcome != MfaOutcome.Success)
        {
            return result;
        }

        ticket.Provider = result.Provider;
        result.TwoFactorToken = twoFactorTokenService.CreateToken(ticket);

        return result;
    }

    protected virtual async Task<MfaResult> DeliverCodeAsync(Store store, ApplicationUser user, string provider)
    {
        var availableProviders = await GetAvailableProvidersAsync(user);
        var mfaProvider = SelectProvider(availableProviders, provider);
        if (mfaProvider == null)
        {
            return MfaResult.Fail(MfaOutcome.ProviderNotAvailable, user, provider);
        }

        var result = MfaResult.Succeed(user, mfaProvider.Name, ToProviderInfos(user, availableProviders));

        if (!mfaProvider.SendsCode)
        {
            return result;
        }

        var deliveryKey = GetCodeDeliveryKey(user, mfaProvider);
        var delivery = await codeDeliveryLimiter.TryAcquireAsync(deliveryKey);
        result.ResendSecondsRemaining = ToSeconds(delivery.RetryAfter);

        if (!delivery.IsAcquired)
        {
            result.Outcome = MfaOutcome.ResendTooSoon;
            return result;
        }

        bool isSent;

        try
        {
            isSent = await GenerateAndSendCodeAsync(store, user, mfaProvider);
        }
        catch
        {
            await codeDeliveryLimiter.ReleaseAsync(deliveryKey);
            throw;
        }

        if (!isSent)
        {
            await codeDeliveryLimiter.ReleaseAsync(deliveryKey);
            return MfaResult.Fail(MfaOutcome.ProviderNotAvailable, user, mfaProvider.Name);
        }

        return result;
    }

    protected virtual string GetCodeDeliveryKey(ApplicationUser user, IMfaProvider mfaProvider)
    {
        return $"{user.Id}:{mfaProvider.Name}";
    }

    protected virtual async Task<bool> UpdateCodeStampAsync(ApplicationUser user)
    {
        try
        {
            var result = await userManager.SetAuthenticationTokenAsync(user,
                ModuleConstants.Security.CodeStampLoginProvider, ModuleConstants.Security.CodeStampName, Guid.NewGuid().ToString("N"));

            return result.Succeeded;
        }
        catch (DuplicateEmailException)
        {
            return false;
        }
    }

    protected virtual async Task<bool> GenerateAndSendCodeAsync(Store store, ApplicationUser user, IMfaProvider mfaProvider)
    {
        if (!await UpdateCodeStampAsync(user))
        {
            return false;
        }

        var code = await userManager.GenerateTwoFactorTokenAsync(user, mfaProvider.Name);

        return await mfaProvider.SendCodeAsync(store, user, code);
    }

    protected virtual IMfaProvider SelectProvider(IList<IMfaProvider> availableProviders, string provider)
    {
        return string.IsNullOrEmpty(provider)
            ? availableProviders.FirstOrDefault()
            : availableProviders.FirstOrDefault(x => x.Name.EqualsIgnoreCase(provider));
    }

    protected virtual IList<MfaProviderInfo> ToProviderInfos(ApplicationUser user, IList<IMfaProvider> availableProviders)
    {
        return availableProviders
            .Select(x =>
            {
                var providerInfo = AbstractTypeFactory<MfaProviderInfo>.TryCreateInstance();
                providerInfo.Name = x.Name;
                providerInfo.Destination = x.GetDestination(user);

                return providerInfo;
            })
            .ToList();
    }

    protected virtual async Task<IList<IMfaProvider>> GetAvailableProvidersAsync(ApplicationUser user)
    {
        var validProviders = await userManager.GetValidTwoFactorProvidersAsync(user);

        return _providers
            .Where(x => validProviders.Contains(x.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    protected virtual Task<Store> GetStoreAsync(string storeId)
    {
        return storeService.GetNoCloneAsync(storeId);
    }

    private static int ToSeconds(TimeSpan value)
    {
        return (int)Math.Ceiling(value.TotalSeconds);
    }
}
