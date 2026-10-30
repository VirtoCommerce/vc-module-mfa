using System;

namespace VirtoCommerce.MFA.Core.Models;

public class TwoFactorTicket
{
    public string Id { get; set; }

    public string UserId { get; set; }

    public string StoreId { get; set; }

    public DateTimeOffset IssuedAt { get; set; }

    public DateTime? LastLoginDate { get; set; }

    public string SecurityStamp { get; set; }

    public string Provider { get; set; }

    /// <summary>
    /// Null for a sign-in, <see cref="ModuleConstants.Security.ChangeTicketPurpose"/> for confirming a change of the MFA mode.
    /// A ticket can be redeemed only for its own purpose.
    /// </summary>
    public string Purpose { get; set; }
}
