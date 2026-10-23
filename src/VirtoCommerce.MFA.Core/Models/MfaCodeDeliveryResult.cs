using System;

namespace VirtoCommerce.MFA.Core.Models;

public class MfaCodeDeliveryResult
{
    public bool IsAcquired { get; set; }

    public TimeSpan RetryAfter { get; set; }
}
