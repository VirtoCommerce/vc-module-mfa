namespace VirtoCommerce.MFA.Core.Models;

public class MfaRequirement
{
    public bool IsRequired { get; set; }

    /// <summary>
    /// What requires the code: one of <see cref="ModuleConstants.RequirementSources"/>.
    /// </summary>
    public string Source { get; set; }

    public static MfaRequirement Create(bool isRequired, string source)
    {
        return new MfaRequirement
        {
            IsRequired = isRequired,
            Source = isRequired ? source : ModuleConstants.RequirementSources.None,
        };
    }
}
