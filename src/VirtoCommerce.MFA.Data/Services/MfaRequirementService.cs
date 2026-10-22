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

        var store = await storeService.GetNoCloneAsync(storeId);

        return GetStoreMode(store) switch
        {
            ModuleConstants.StoreModes.Enabled => true,
            ModuleConstants.StoreModes.Disabled => false,
            _ => await settingsManager.GetValueAsync<bool>(MfaModuleSettings.MfaEnabled),
        };
    }

    public virtual Task<bool> IsRequiredForBackofficeAsync(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return settingsManager.GetValueAsync<bool>(MfaModuleSettings.MfaEnabled);
    }

    protected virtual string GetStoreMode(Store store)
    {
        return store?.Settings.GetValue<string>(MfaModuleSettings.StoreMode) ?? ModuleConstants.StoreModes.UseGlobalSetting;
    }
}
