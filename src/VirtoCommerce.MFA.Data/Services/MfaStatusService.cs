using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model.Search;
using VirtoCommerce.StoreModule.Core.Services;
using MfaModuleSettings = VirtoCommerce.MFA.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.MFA.Data.Services;

public class MfaStatusService(
    UserManager<ApplicationUser> userManager,
    IStoreSearchService storeSearchService,
    ISettingsManager settingsManager,
    IMfaRequirementService requirementService,
    IMfaModeService modeService)
    : IMfaStatusService
{
    public virtual async Task<MfaStatus> GetUserStatusAsync(string userId, string mode = null)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return null;
        }

        var organizationModes = await modeService.GetOrganizationModesAsync(await requirementService.GetOrganizationIdsAsync(user));
        var userMode = mode ?? await modeService.GetUserModeAsync(user.Id);

        var status = await CreateStatusAsync(organizationModes.Values.ToList(), userMode);
        if (user.UserType != nameof(UserType.Customer))
        {
            status.IsRequiredForBackoffice = await requirementService.IsRequiredForBackofficeAsync(user);
        }

        return status;
    }

    public virtual async Task<MfaStatus> GetOrganizationStatusAsync(string organizationId, string mode = null)
    {
        var organizationMode = mode ?? await modeService.GetOrganizationModeAsync(organizationId);

        // Each member chooses for themselves, so the status shows only what the organization decides
        return await CreateStatusAsync([organizationMode], userMode: null);
    }

    protected virtual async Task<MfaStatus> CreateStatusAsync(IList<string> organizationModes, string userMode)
    {
        var status = AbstractTypeFactory<MfaStatus>.TryCreateInstance();
        status.IsGloballyEnabled = await settingsManager.GetValueAsync<bool>(MfaModuleSettings.MfaEnabled);

        var stores = await storeSearchService.SearchAllNoCloneAsync(new StoreSearchCriteria { Sort = "Name" });

        foreach (var store in stores)
        {
            var storeMode = await requirementService.GetStoreModeAsync(store.Id);
            var requirement = await requirementService.GetRequirementAsync(storeMode, organizationModes, userMode);

            var storeStatus = AbstractTypeFactory<MfaStoreStatus>.TryCreateInstance();
            storeStatus.StoreId = store.Id;
            storeStatus.StoreName = store.Name;
            storeStatus.StoreMode = storeMode;
            storeStatus.IsRequired = requirement.IsRequired;
            storeStatus.RequiredBy = requirement.Source;
            status.Stores.Add(storeStatus);
        }

        return status;
    }
}
