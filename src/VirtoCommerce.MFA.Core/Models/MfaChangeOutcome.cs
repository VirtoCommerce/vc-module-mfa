namespace VirtoCommerce.MFA.Core.Models;

public enum MfaChangeOutcome
{
    Success,

    /// <summary>
    /// The store doesn't let customers choose: its mode isn't <see cref="ModuleConstants.StoreModes.CustomerChoice"/>.
    /// </summary>
    SelfServiceNotAllowed,

    /// <summary>
    /// An administrator set the mode to <see cref="ModuleConstants.Modes.Required"/>.
    /// </summary>
    LockedByAdministrator,

    /// <summary>
    /// The user may not change the MFA mode of the organization.
    /// </summary>
    OrganizationNotAllowed,

    /// <summary>
    /// The code or the two-factor token was refused; see <see cref="MfaChangeResult.CodeResult"/>.
    /// </summary>
    CodeRefused,
}
