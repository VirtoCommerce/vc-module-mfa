using System;
using System.Threading.Tasks;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using Modes = VirtoCommerce.MFA.Core.ModuleConstants.Modes;

namespace VirtoCommerce.MFA.Data.Services;

public class MfaSelfService(
    IMfaService mfaService,
    ITwoFactorTokenService twoFactorTokenService,
    IMfaRequirementService requirementService,
    IMfaModeService modeService)
    : IMfaSelfService
{
    public virtual async Task<MfaSettings> GetSettingsAsync(ApplicationUser user, string storeId, string organizationId, bool canManageOrganization)
    {
        ArgumentNullException.ThrowIfNull(user);

        var storeMode = await requirementService.GetStoreModeAsync(storeId);
        var requirement = await requirementService.GetRequirementAsync(user, storeId);
        var isCustomerChoice = storeMode == ModuleConstants.StoreModes.CustomerChoice;

        var settings = AbstractTypeFactory<MfaSettings>.TryCreateInstance();
        settings.StoreId = storeId;
        settings.StoreMode = storeMode;
        settings.IsRequired = requirement.IsRequired;
        settings.RequiredBy = requirement.Source;

        settings.UserMode = await modeService.GetUserModeAsync(user.Id);
        settings.CanChangeUserMode = isCustomerChoice && settings.UserMode != Modes.Required;

        if (!string.IsNullOrEmpty(organizationId))
        {
            settings.OrganizationId = organizationId;
            settings.OrganizationMode = await modeService.GetOrganizationModeAsync(organizationId);
            settings.CanChangeOrganizationMode = isCustomerChoice && canManageOrganization && settings.OrganizationMode != Modes.Required;
        }

        var provider = await mfaService.GetProviderAsync(user);
        settings.IsProviderAvailable = provider != null;
        settings.Provider = provider?.Name;
        settings.Destination = provider?.GetDestination(user);

        return settings;
    }

    public virtual Task<MfaResult> SendChangeCodeAsync(ApplicationUser user, string storeId)
    {
        ArgumentNullException.ThrowIfNull(user);

        return mfaService.ChallengeAsync(user, storeId, purpose: ModuleConstants.Security.ChangeTicketPurpose);
    }

    public virtual async Task<MfaChangeResult> ChangeUserModeAsync(ApplicationUser user, string storeId, bool enabled, string twoFactorToken, string code)
    {
        ArgumentNullException.ThrowIfNull(user);

        var settings = await GetSettingsAsync(user, storeId, organizationId: null, canManageOrganization: false);
        if (!settings.CanChangeUserMode)
        {
            return Fail(settings.UserMode == Modes.Required ? MfaChangeOutcome.LockedByAdministrator : MfaChangeOutcome.SelfServiceNotAllowed);
        }

        var codeResult = await VerifyCodeAsync(user, storeId, twoFactorToken, code);
        if (codeResult.Outcome != MfaOutcome.Success)
        {
            return Fail(MfaChangeOutcome.CodeRefused, codeResult);
        }

        await modeService.SetUserModeAsync(user.Id, enabled ? Modes.Enabled : Modes.Off);

        return await SucceedAsync(user, storeId, organizationId: null, canManageOrganization: false);
    }

    public virtual async Task<MfaChangeResult> ChangeOrganizationModeAsync(ApplicationUser user, string storeId, string organizationId, bool canManageOrganization,
        bool enabled, string twoFactorToken, string code)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrEmpty(organizationId) || !canManageOrganization)
        {
            return Fail(MfaChangeOutcome.OrganizationNotAllowed);
        }

        var settings = await GetSettingsAsync(user, storeId, organizationId, canManageOrganization);
        if (!settings.CanChangeOrganizationMode)
        {
            return Fail(settings.OrganizationMode == Modes.Required ? MfaChangeOutcome.LockedByAdministrator : MfaChangeOutcome.SelfServiceNotAllowed);
        }

        var codeResult = await VerifyCodeAsync(user, storeId, twoFactorToken, code);
        if (codeResult.Outcome != MfaOutcome.Success)
        {
            return Fail(MfaChangeOutcome.CodeRefused, codeResult);
        }

        await modeService.SetOrganizationModeAsync(organizationId, enabled ? Modes.Enabled : Modes.Off);

        return await SucceedAsync(user, storeId, organizationId, canManageOrganization);
    }

    /// <summary>
    /// The code must come from a change ticket of the same user and store.
    /// </summary>
    protected virtual async Task<MfaResult> VerifyCodeAsync(ApplicationUser user, string storeId, string twoFactorToken, string code)
    {
        if (string.IsNullOrEmpty(twoFactorToken) || string.IsNullOrEmpty(code))
        {
            return MfaResult.Fail(MfaOutcome.InvalidCode, user);
        }

        // Checked before the code, so the token of another user can't spend that user's code
        if (twoFactorTokenService.ReadToken(twoFactorToken)?.UserId != user.Id)
        {
            return MfaResult.Fail(MfaOutcome.InvalidTwoFactorToken, user);
        }

        return await mfaService.VerifyCodeAsync(twoFactorToken, code, storeId, ModuleConstants.Security.ChangeTicketPurpose);
    }

    protected virtual async Task<MfaChangeResult> SucceedAsync(ApplicationUser user, string storeId, string organizationId, bool canManageOrganization)
    {
        var result = AbstractTypeFactory<MfaChangeResult>.TryCreateInstance();
        result.Outcome = MfaChangeOutcome.Success;
        result.Settings = await GetSettingsAsync(user, storeId, organizationId, canManageOrganization);

        return result;
    }

    protected virtual MfaChangeResult Fail(MfaChangeOutcome outcome, MfaResult codeResult = null)
    {
        var result = AbstractTypeFactory<MfaChangeResult>.TryCreateInstance();
        result.Outcome = outcome;
        result.CodeResult = codeResult;

        return result;
    }
}
