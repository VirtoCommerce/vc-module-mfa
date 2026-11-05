using VirtoCommerce.MFA.Xapi.Models;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.MFA.Xapi.Commands;

public abstract class MfaCommand<TResult> : ICommand<TResult>
{
    public string StoreId { get; set; }

    public ApplicationUser User { get; set; }
}

/// <summary>
/// Sends a code that confirms a change of the MFA mode.
/// </summary>
public class RequestMfaChangeCodeCommand : MfaCommand<MfaCodeChallenge>;

public abstract class ChangeMfaModeCommand : MfaCommand<MfaChangeResponse>
{
    public bool Enabled { get; set; }

    public string TwoFactorToken { get; set; }

    public string Code { get; set; }
}

public class UpdateMfaUserModeCommand : ChangeMfaModeCommand;

public class UpdateMfaOrganizationModeCommand : ChangeMfaModeCommand
{
    public string OrganizationId { get; set; }

    public bool CanManageOrganization { get; set; }
}
