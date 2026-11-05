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
    IMemberService memberService,
    IAdminUIAccessPolicy adminUIAccessPolicy)
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

    public virtual async Task<MfaRequirement> GetRequirementAsync(ApplicationUser user, string storeId)
    {
        ArgumentNullException.ThrowIfNull(user);

        return await GetRequirementAsync(user, storeId, await modeService.GetUserModeAsync(user.Id));
    }

    public virtual async Task<MfaRequirement> GetRequirementAsync(ApplicationUser user, string storeId, string userMode)
    {
        ArgumentNullException.ThrowIfNull(user);

        var requirement = await GetStoreRequirementAsync(user, storeId, userMode);

        // Tokens are not bound to the store they were issued for, so the user's own store applies too:
        // naming another store, or none, must not skip the code that the user's store requires
        if (!requirement.IsRequired && !string.IsNullOrEmpty(user.StoreId) && !user.StoreId.EqualsIgnoreCase(storeId))
        {
            var userStoreRequirement = await GetStoreRequirementAsync(user, user.StoreId, userMode);
            if (userStoreRequirement.IsRequired)
            {
                userStoreRequirement.IsRequiredByUserStore = true;

                return userStoreRequirement;
            }
        }

        return requirement;
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

    protected virtual async Task<MfaRequirement> GetStoreRequirementAsync(ApplicationUser user, string storeId, string userMode)
    {
        var storeMode = await GetStoreModeAsync(storeId);

        // A Disabled store lets in without the code only its own customers: tokens are not bound to the store,
        // so the Backoffice accounts and the users of other stores follow the global setting there
        var isDisabledModeSkipped = storeMode == ModuleConstants.StoreModes.Disabled && !await CanSkipCodeInDisabledStoreAsync(user, storeId);
        if (isDisabledModeSkipped)
        {
            storeMode = ModuleConstants.StoreModes.UseGlobalSetting;
        }

        MfaRequirement requirement;
        if (IsChosenMode(storeMode))
        {
            var organizationModes = await modeService.GetOrganizationModesAsync(await GetOrganizationIdsAsync(user));
            requirement = await GetRequirementAsync(storeMode, organizationModes.Values, userMode);
        }
        else
        {
            requirement = await GetRequirementAsync(storeMode, [], userMode: null);
        }

        requirement.IsDisabledModeSkipped = isDisabledModeSkipped;

        return requirement;
    }

    protected virtual async Task<bool> CanSkipCodeInDisabledStoreAsync(ApplicationUser user, string storeId)
    {
        return !string.IsNullOrEmpty(user.StoreId) && user.StoreId.EqualsIgnoreCase(storeId) && !await IsBackofficeAccountAsync(user);
    }

    /// <summary>
    /// Administrators, managers and the users the platform lets into the Backoffice (by default, any user with a permission).
    /// </summary>
    protected virtual async Task<bool> IsBackofficeAccountAsync(ApplicationUser user)
    {
        if (user.IsAdministrator ||
            user.UserType.EqualsIgnoreCase(nameof(UserType.Administrator)) ||
            user.UserType.EqualsIgnoreCase(nameof(UserType.Manager)))
        {
            return true;
        }

        var permissions = user.Roles?
            .SelectMany(x => x.Permissions ?? [])
            .Select(x => x.Name)
            .Distinct()
            .ToList() ?? [];

        var access = await adminUIAccessPolicy.EvaluateAsync(new AdminUIAccessContext { User = user, Permissions = permissions });

        return access.IsAllowed;
    }
}
