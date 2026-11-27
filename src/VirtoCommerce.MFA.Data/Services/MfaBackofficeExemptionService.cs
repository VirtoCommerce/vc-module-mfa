using System;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.MFA.Data.Services;

public class MfaBackofficeExemptionService(ISettingsManager settingsManager) : IMfaBackofficeExemptionService
{
    public virtual async Task<bool> IsExemptAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var settings = await settingsManager.GetObjectSettingsAsync(
            [ModuleConstants.Settings.General.BackofficeExempt.Name], nameof(ApplicationUser), userId);

        return settings.FirstOrDefault()?.Value is true;
    }

    public virtual Task SetExemptionAsync(string userId, bool isExempt)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return settingsManager.SaveObjectSettingsAsync(
        [
            new ObjectSettingEntry(ModuleConstants.Settings.General.BackofficeExempt)
            {
                ObjectType = nameof(ApplicationUser),
                ObjectId = userId,
                Value = isExempt,
            },
        ]);
    }
}
