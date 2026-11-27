namespace VirtoCommerce.MFA.Core.Models;

public class MfaRequirement
{
    public bool IsRequired { get; set; }

    /// <summary>
    /// What requires the code: one of <see cref="ModuleConstants.RequirementSources"/>.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// The user's own store requires the code, not the store of the sign-in: tokens are not bound to a store.
    /// </summary>
    public bool IsRequiredByUserStore { get; set; }

    /// <summary>
    /// The store of the sign-in is Disabled, but that lets in without the code only its own customers,
    /// so the global setting applies to this user there.
    /// </summary>
    public bool IsDisabledModeSkipped { get; set; }

    public static MfaRequirement Create(bool isRequired, string source)
    {
        return new MfaRequirement
        {
            IsRequired = isRequired,
            Source = isRequired ? source : ModuleConstants.RequirementSources.None,
        };
    }
}
