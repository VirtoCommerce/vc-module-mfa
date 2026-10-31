using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using MfaModuleSettings = VirtoCommerce.MFA.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.MFA.Data.Services;

/// <summary>
/// Keeps the modes in the platform settings of the user and the organization, so the module needs no tables of its own.
/// </summary>
public class MfaModeService(ISettingsManager settingsManager) : IMfaModeService
{
    public virtual Task<string> GetUserModeAsync(string userId)
    {
        return GetModeAsync(MfaModuleSettings.UserMode, nameof(ApplicationUser), userId);
    }

    public virtual Task SetUserModeAsync(string userId, string mode)
    {
        return SetModeAsync(MfaModuleSettings.UserMode, nameof(ApplicationUser), userId, mode);
    }

    public virtual Task<string> GetOrganizationModeAsync(string organizationId)
    {
        return GetModeAsync(MfaModuleSettings.OrganizationMode, nameof(Organization), organizationId);
    }

    public virtual async Task<IDictionary<string, string>> GetOrganizationModesAsync(IList<string> organizationIds)
    {
        var result = new Dictionary<string, string>();

        foreach (var organizationId in organizationIds.Where(x => !string.IsNullOrEmpty(x)).Distinct())
        {
            result[organizationId] = await GetOrganizationModeAsync(organizationId);
        }

        return result;
    }

    public virtual Task SetOrganizationModeAsync(string organizationId, string mode)
    {
        return SetModeAsync(MfaModuleSettings.OrganizationMode, nameof(Organization), organizationId, mode);
    }

    protected virtual async Task<string> GetModeAsync(SettingDescriptor descriptor, string objectType, string objectId)
    {
        if (string.IsNullOrEmpty(objectId))
        {
            return ModuleConstants.Modes.Off;
        }

        var setting = (await settingsManager.GetObjectSettingsAsync([descriptor.Name], objectType, objectId)).FirstOrDefault();

        return NormalizeMode(setting?.Value as string);
    }

    protected virtual Task SetModeAsync(SettingDescriptor descriptor, string objectType, string objectId, string mode)
    {
        ArgumentException.ThrowIfNullOrEmpty(objectId);

        var setting = new ObjectSettingEntry(descriptor)
        {
            ObjectType = objectType,
            ObjectId = objectId,
            Value = NormalizeMode(mode),
        };

        return settingsManager.SaveObjectSettingsAsync([setting]);
    }

    protected virtual string NormalizeMode(string mode)
    {
        return ModuleConstants.Modes.AllModes.FirstOrDefault(x => x.Equals(mode, StringComparison.OrdinalIgnoreCase))
            ?? ModuleConstants.Modes.Off;
    }
}
