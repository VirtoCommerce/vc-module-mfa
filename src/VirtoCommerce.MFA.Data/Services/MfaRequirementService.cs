using System;
using System.Threading.Tasks;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using MfaModuleSettings = VirtoCommerce.MFA.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.MFA.Data.Services;

public class MfaRequirementService(IStoreService storeService, ISettingsManager settingsManager) : IMfaRequirementService
{
    public virtual async Task<bool> IsRequiredAsync(ApplicationUser user, string storeId)
    {
        ArgumentNullException.ThrowIfNull(user);

        var isGloballyEnabled = await settingsManager.GetValueAsync<bool>(MfaModuleSettings.MfaEnabled);

        var store = await storeService.GetNoCloneAsync(storeId);

        if (store == null || !store.Id.EqualsIgnoreCase(user.StoreId))
        {
            var userStore = await storeService.GetNoCloneAsync(user.StoreId);
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

    protected virtual bool IsStoreCustomer(ApplicationUser user, Store store)
    {
        return user.UserType.EqualsIgnoreCase(nameof(UserType.Customer)) &&
            user.StoreId.EqualsIgnoreCase(store.Id) &&
            !user.IsAdministrator;
    }

    protected virtual string GetStoreMode(Store store)
    {
        return store?.Settings.GetValue<string>(MfaModuleSettings.StoreMode) ?? ModuleConstants.StoreModes.UseGlobalSetting;
    }
}
