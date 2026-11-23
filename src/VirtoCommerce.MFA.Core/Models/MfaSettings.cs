namespace VirtoCommerce.MFA.Core.Models;

/// <summary>
/// The MFA state of a user in a store, as the storefront shows it.
/// </summary>
public class MfaSettings
{
    public string StoreId { get; set; }

    /// <summary>
    /// One of <see cref="ModuleConstants.StoreModes"/>.
    /// </summary>
    public string StoreMode { get; set; }

    /// <summary>
    /// Whether the password sign-in of the user to the store requires the code.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// One of <see cref="ModuleConstants.RequirementSources"/>.
    /// </summary>
    public string RequiredBy { get; set; }

    /// <summary>
    /// One of <see cref="ModuleConstants.Modes"/>.
    /// </summary>
    public string UserMode { get; set; }

    public bool CanChangeUserMode { get; set; }

    public string OrganizationId { get; set; }

    /// <summary>
    /// One of <see cref="ModuleConstants.Modes"/>; null without an organization.
    /// </summary>
    public string OrganizationMode { get; set; }

    public bool CanChangeOrganizationMode { get; set; }

    /// <summary>
    /// Whether a code can be delivered to the user, for example the user has an email.
    /// </summary>
    public bool IsProviderAvailable { get; set; }

    /// <summary>
    /// Whether the account has lockout enabled: without it the user can't pass the second factor, so neither mode can be changed.
    /// </summary>
    public bool IsLockoutEnabled { get; set; }

    public string Provider { get; set; }

    /// <summary>
    /// The masked destination of the code, for example j•••h@acme.com.
    /// </summary>
    public string Destination { get; set; }
}
