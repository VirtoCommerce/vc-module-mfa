using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using MfaModuleSettings = VirtoCommerce.MFA.Core.ModuleConstants.Settings.General;
using Modes = VirtoCommerce.MFA.Core.ModuleConstants.Modes;
using Sources = VirtoCommerce.MFA.Core.ModuleConstants.RequirementSources;

namespace VirtoCommerce.MFA.Data.Services;

public class MfaRequirementService(
    IStoreService storeService,
    ISettingsManager settingsManager,
    IMfaModeService modeService,
    IMemberService memberService)
    : IMfaRequirementService
{
    public virtual async Task<bool> IsRequiredAsync(ApplicationUser user, string storeId)
    {
        var requirement = await GetRequirementAsync(user, storeId);

        return requirement.IsRequired;
    }

    public virtual Task<bool> IsRequiredForBackofficeAsync(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return settingsManager.GetValueAsync<bool>(MfaModuleSettings.MfaEnabled);
    }

    public virtual Task<MfaRequirement> GetRequirementAsync(ApplicationUser user, string storeId)
    {
        ArgumentNullException.ThrowIfNull(user);

        return GetUserRequirementAsync(user, storeId);
    }

    public virtual async Task<MfaRequirement> GetRequirementAsync(string storeMode, IEnumerable<string> organizationModes, string userMode)
    {
        return storeMode switch
        {
            ModuleConstants.StoreModes.Enabled => MfaRequirement.Create(true, Sources.Store),
            ModuleConstants.StoreModes.Disabled => MfaRequirement.Create(false, Sources.None),
            ModuleConstants.StoreModes.AdminManaged => GetChosenRequirement(organizationModes, userMode, mode => mode == Modes.Required),
            ModuleConstants.StoreModes.CustomerChoice => GetChosenRequirement(organizationModes, userMode, mode => mode is Modes.Enabled or Modes.Required),
            _ => MfaRequirement.Create(await settingsManager.GetValueAsync<bool>(MfaModuleSettings.MfaEnabled), Sources.Global),
        };
    }

    public virtual async Task<string> GetStoreModeAsync(string storeId)
    {
        var store = await storeService.GetNoCloneAsync(storeId);

        return GetStoreMode(store);
    }

    protected virtual string GetStoreMode(Store store)
    {
        return store?.Settings.GetValue<string>(MfaModuleSettings.StoreMode) ?? ModuleConstants.StoreModes.UseGlobalSetting;
    }

    /// <summary>
    /// In these stores the modes of users and organizations decide.
    /// </summary>
    protected virtual bool IsChosenMode(string storeMode)
    {
        return storeMode is ModuleConstants.StoreModes.AdminManaged or ModuleConstants.StoreModes.CustomerChoice;
    }

    /// <summary>
    /// The organization requirement comes first: the user can't turn it off.
    /// </summary>
    protected virtual MfaRequirement GetChosenRequirement(IEnumerable<string> organizationModes, string userMode, Func<string, bool> requires)
    {
        if (organizationModes.Any(requires))
        {
            return MfaRequirement.Create(true, Sources.Organization);
        }

        return MfaRequirement.Create(userMode != null && requires(userMode), Sources.User);
    }

    public virtual async Task<IList<string>> GetOrganizationIdsAsync(ApplicationUser user)
    {
        if (string.IsNullOrEmpty(user.MemberId))
        {
            return [];
        }

        var member = await memberService.GetByIdAsync(user.MemberId, nameof(MemberResponseGroup.Default));

        return (member as Contact)?.Organizations ?? [];
    }

    private async Task<MfaRequirement> GetUserRequirementAsync(ApplicationUser user, string storeId)
    {
        var storeMode = await GetStoreModeAsync(storeId);
        if (!IsChosenMode(storeMode))
        {
            return await GetRequirementAsync(storeMode, [], userMode: null);
        }

        var organizationModes = await modeService.GetOrganizationModesAsync(await GetOrganizationIdsAsync(user));
        var userMode = await modeService.GetUserModeAsync(user.Id);

        return await GetRequirementAsync(storeMode, organizationModes.Values, userMode);
    }
}
