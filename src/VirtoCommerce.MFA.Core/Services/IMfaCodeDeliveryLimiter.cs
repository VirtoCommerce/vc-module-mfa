using System;

namespace VirtoCommerce.MFA.Core.Services;

public interface IMfaCodeDeliveryLimiter
{
    bool TryAcquire(string key, out TimeSpan retryAfter);

    void Release(string key);
}
