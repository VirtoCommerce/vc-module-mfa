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

    /// <summary>
    /// The user's own store requires the code, not this store: tokens are not bound to a store.
    /// </summary>
    public bool IsRequiredByUserStore { get; set; }

    /// <summary>
    /// This store is Disabled, but the user isn't one of its own customers, so the global setting applies.
    /// </summary>
    public bool IsDisabledModeSkipped { get; set; }
}
