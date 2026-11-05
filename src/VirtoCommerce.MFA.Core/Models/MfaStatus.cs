using System.Collections.Generic;

namespace VirtoCommerce.MFA.Core.Models;

/// <summary>
/// Where the sign-in of a user, or of the members of an organization, needs the code.
/// </summary>
public class MfaStatus
{
    /// <summary>
    /// The global setting: it decides for the Backoffice sign-in and for the stores that use the global setting.
    /// </summary>
    public bool IsGloballyEnabled { get; set; }

    /// <summary>
    /// Whether the Backoffice sign-in needs the code; null for a customer or an organization.
    /// </summary>
    public bool? IsRequiredForBackoffice { get; set; }

    /// <summary>
    /// The user's own store: it applies to the sign-ins to other stores too. Null for an organization.
    /// </summary>
    public string UserStoreId { get; set; }

    public IList<MfaStoreStatus> Stores { get; set; } = [];
}
