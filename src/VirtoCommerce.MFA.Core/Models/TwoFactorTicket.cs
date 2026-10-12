using System;

namespace VirtoCommerce.MFA.Core.Models;

public class TwoFactorTicket
{
    public string UserId { get; set; }

    public string StoreId { get; set; }

    public DateTimeOffset IssuedAt { get; set; }

    public DateTime? LastLoginDate { get; set; }

    public string SecurityStamp { get; set; }

    public string Provider { get; set; }
}
