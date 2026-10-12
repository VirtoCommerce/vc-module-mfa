using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using MfaModuleSettings = VirtoCommerce.MFA.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.MFA.Data.Services;

public class MfaService(
    UserManager<ApplicationUser> userManager,
    IStoreService storeService,
    ISettingsManager settingsManager,
    ITwoFactorTokenService twoFactorTokenService,
    IEnumerable<IMfaProvider> providers)
    : IMfaService
{
    private readonly IList<IMfaProvider> _providers = providers.ToList();

    public virtual async Task<bool> IsRequiredAsync(ApplicationUser user, string storeId)
    {
        ArgumentNullException.ThrowIfNull(user);

        var isGloballyEnabled = await settingsManager.GetValueAsync<bool>(MfaModuleSettings.MfaEnabled);

        var store = await GetStoreAsync(storeId);

        if (store == null || !store.Id.EqualsIgnoreCase(user.StoreId))
        {
            var userStore = await GetStoreAsync(user.StoreId);
            if (GetStoreMode(userStore) == ModuleConstants.StoreModes.Enabled)
            {
                return true;
            }
        }

        return GetStoreMode(store) switch
        {
            ModuleConstants.StoreModes.Enabled => true,
            ModuleConstants.StoreModes.Disabled when IsStoreCustomer(user, store) => false,
            _ => isGloballyEnabled,
        };
    }

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

    public virtual async Task<MfaResult> ChallengeAsync(string storeId, ApplicationUser user, string provider = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        var result = await ValidateUserAsync(user);
        if (result.Outcome != MfaOutcome.Success)
        {
            return result;
        }

        var ticket = new TwoFactorTicket
        {
            UserId = user.Id,
            StoreId = storeId.EmptyToNull(),
            IssuedAt = DateTimeOffset.UtcNow,
            LastLoginDate = user.LastLoginDate,
            SecurityStamp = await userManager.GetSecurityStampAsync(user),
        };

        return await SendCodeForTicketAsync(ticket, user, provider);
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

        return await SendCodeForTicketAsync(ticket, result.User, provider);
    }

    public virtual async Task<MfaResult> VerifyCodeAsync(string storeId, string twoFactorToken, string code)
    {
        ArgumentException.ThrowIfNullOrEmpty(twoFactorToken);
        ArgumentException.ThrowIfNullOrEmpty(code);

        var ticket = twoFactorTokenService.ReadToken(twoFactorToken);

        if (ticket != null && !ticket.StoreId.EqualsIgnoreCase(storeId.EmptyToNull()))
        {
            return MfaResult.Fail(MfaOutcome.InvalidTwoFactorToken);
        }

        var result = await ValidateTicketAsync(ticket);
        if (result.Outcome != MfaOutcome.Success)
        {
            return result;
        }

        var user = result.User;

        var mfaProvider = await GetProviderAsync(user, ticket.Provider);
        if (mfaProvider == null)
        {
            return MfaResult.Fail(MfaOutcome.ProviderNotAvailable, user);
        }

        if (!await userManager.VerifyTwoFactorTokenAsync(user, mfaProvider.Name, code))
        {
            await userManager.AccessFailedAsync(user);

            return await userManager.IsLockedOutAsync(user)
                ? MfaResult.Fail(MfaOutcome.AccountLocked, user)
                : MfaResult.Fail(MfaOutcome.InvalidCode, user);
        }

        return MfaResult.Succeed(user);
    }

    protected virtual async Task<MfaResult> ValidateTicketAsync(TwoFactorTicket ticket)
    {
        if (ticket == null)
        {
            return MfaResult.Fail(MfaOutcome.InvalidTwoFactorToken);
        }

        var user = await userManager.FindByIdAsync(ticket.UserId);

        if (user == null || ticket.SecurityStamp != await userManager.GetSecurityStampAsync(user) || IsTicketUsed(ticket, user))
        {
            return MfaResult.Fail(MfaOutcome.InvalidTwoFactorToken);
        }

        return await ValidateUserAsync(user);
    }

    protected virtual bool IsTicketUsed(TwoFactorTicket ticket, ApplicationUser user)
    {
        return user.LastLoginDate != ticket.LastLoginDate;
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

    protected virtual async Task<MfaResult> SendCodeForTicketAsync(TwoFactorTicket ticket, ApplicationUser user, string provider)
    {
        var store = await GetStoreAsync(ticket.StoreId);

        var result = await DeliverCodeAsync(store, user, provider);
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
            return MfaResult.Fail(MfaOutcome.ProviderNotAvailable, user);
        }

        await GenerateAndSendCodeAsync(store, user, mfaProvider);

        return MfaResult.Succeed(user, provider: mfaProvider.Name, providers: ToProviderInfos(user, availableProviders));
    }

    protected virtual async Task GenerateAndSendCodeAsync(Store store, ApplicationUser user, IMfaProvider mfaProvider)
    {
        var code = await userManager.GenerateTwoFactorTokenAsync(user, mfaProvider.Name);
        await mfaProvider.SendCodeAsync(store, user, code);
    }

    public virtual async Task<IMfaProvider> GetProviderAsync(ApplicationUser user, string provider = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        return SelectProvider(await GetAvailableProvidersAsync(user), provider);
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
            .Select(x => new MfaProviderInfo { Name = x.Name, Destination = x.GetDestination(user) })
            .ToList();
    }

    protected virtual async Task<IList<IMfaProvider>> GetAvailableProvidersAsync(ApplicationUser user)
    {
        var validProviders = await userManager.GetValidTwoFactorProvidersAsync(user);

        return _providers
            .Where(x => validProviders.Contains(x.Name, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    protected virtual bool IsStoreCustomer(ApplicationUser user, Store store)
    {
        return user.UserType.EqualsIgnoreCase(nameof(UserType.Customer)) &&
            user.StoreId.EqualsIgnoreCase(store.Id) &&
            !user.IsAdministrator;
    }

    protected virtual async Task<Store> GetStoreAsync(string storeId)
    {
        return string.IsNullOrEmpty(storeId) ? null : await storeService.GetNoCloneAsync(storeId);
    }

    protected virtual string GetStoreMode(Store store)
    {
        return store?.Settings.GetValue<string>(MfaModuleSettings.StoreMode) ?? ModuleConstants.StoreModes.UseGlobalSetting;
    }
}
