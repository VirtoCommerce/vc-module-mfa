namespace VirtoCommerce.MFA.Core.Models;

public class MfaStoreStatus
{
    public string StoreId { get; set; }

    public string StoreName { get; set; }

    /// <summary>
    /// One of <see cref="ModuleConstants.StoreModes"/>.
    /// </summary>
    public string StoreMode { get; set; }

    public bool IsRequired { get; set; }

    /// <summary>
    /// What requires the code: one of <see cref="ModuleConstants.RequirementSources"/>.
    /// </summary>
    public string RequiredBy { get; set; }
}
