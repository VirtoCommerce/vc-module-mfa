using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;

namespace VirtoCommerce.MFA.Data.Services;

public class MfaService(
    UserManager<ApplicationUser> userManager,
    IStoreService storeService,
    ITwoFactorTokenService twoFactorTokenService,
    ITwoFactorTicketStore ticketStore,
    IEnumerable<IMfaProvider> providers,
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

    public virtual async Task<MfaResult> ChallengeAsync(ApplicationUser user, string storeId, string provider = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        var result = await ValidateUserAsync(user);
        if (result.Outcome != MfaOutcome.Success)
        {
            return result;
        }

        var ticket = new TwoFactorTicket
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = user.Id,
            StoreId = storeId.EmptyToNull(),
            IssuedAt = timeProvider.GetUtcNow(),
            LastLoginDate = user.LastLoginDate,
            SecurityStamp = await userManager.GetSecurityStampAsync(user),
        };

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

    public virtual async Task<MfaResult> VerifyCodeAsync(string twoFactorToken, string code, string storeId)
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

        if (!ticket.StoreId.EqualsIgnoreCase(storeId.EmptyToNull()))
        {
            return MfaResult.Fail(MfaOutcome.InvalidTwoFactorToken, user, ticket.Provider);
        }

        var mfaProvider = await GetProviderAsync(user, ticket.Provider);
        if (mfaProvider == null)
        {
            return MfaResult.Fail(MfaOutcome.ProviderNotAvailable, user, ticket.Provider);
        }

        if (!ticketStore.TryConsumeAttempt(ticket))
        {
            return MfaResult.Fail(MfaOutcome.CodeAttemptsExceeded, user, mfaProvider.Name);
        }

        if (!await userManager.VerifyTwoFactorTokenAsync(user, mfaProvider.Name, code))
        {
            return await FailCodeAsync(ticket, user, mfaProvider);
        }

        if (!ticketStore.TryRedeem(ticket))
        {
            return MfaResult.Fail(MfaOutcome.InvalidTwoFactorToken, user, mfaProvider.Name);
        }

        return MfaResult.Succeed(user, mfaProvider.Name);
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
            ticketStore.Revoke(ticket);
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
            IsTicketUsed(ticket, user))
        {
            return MfaResult.Fail(MfaOutcome.InvalidTwoFactorToken, user, ticket.Provider);
        }

        if (!ticketStore.HasAttemptsLeft(ticket))
        {
            return MfaResult.Fail(MfaOutcome.CodeAttemptsExceeded, user, ticket.Provider);
        }

        return await ValidateUserAsync(user);
    }

    protected virtual bool IsTicketExpired(TwoFactorTicket ticket)
    {
        return timeProvider.GetUtcNow() >= ticket.IssuedAt + options.Value.TwoFactorTokenLifetime;
    }

    protected virtual bool IsTicketUsed(TwoFactorTicket ticket, ApplicationUser user)
    {
        return user.LastLoginDate != ticket.LastLoginDate || ticketStore.IsRedeemed(ticket);
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
        var isAcquired = codeDeliveryLimiter.TryAcquire(deliveryKey, out var retryAfter);
        result.ResendSecondsRemaining = ToSeconds(retryAfter);

        if (!isAcquired)
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
            codeDeliveryLimiter.Release(deliveryKey);
            throw;
        }

        if (!isSent)
        {
            codeDeliveryLimiter.Release(deliveryKey);
            return MfaResult.Fail(MfaOutcome.ProviderNotAvailable, user, mfaProvider.Name);
        }

        return result;
    }

    protected virtual string GetCodeDeliveryKey(ApplicationUser user, IMfaProvider mfaProvider)
    {
        return $"{user.Id}:{mfaProvider.Name}";
    }

    protected virtual async Task<bool> GenerateAndSendCodeAsync(Store store, ApplicationUser user, IMfaProvider mfaProvider)
    {
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
