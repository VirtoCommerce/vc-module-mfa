using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;

namespace VirtoCommerce.MFA.Data.Services;

public class MfaCodeDeliveryLimiter(
    IMemoryCache memoryCache,
    IOptions<MfaOptions> options,
    TimeProvider timeProvider)
    : IMfaCodeDeliveryLimiter
{
    private const string CacheKeyPrefix = "VirtoCommerce.MFA.CodeDelivery:";

    private readonly object _lock = new();

    public virtual bool TryAcquire(string key, out TimeSpan retryAfter)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var cacheKey = CacheKeyPrefix + key;
        var interval = options.Value.CodeResendInterval;

        lock (_lock)
        {
            var now = timeProvider.GetUtcNow();

            if (memoryCache.TryGetValue(cacheKey, out DateTimeOffset allowedAt) && allowedAt > now)
            {
                retryAfter = allowedAt - now;
                return false;
            }

            memoryCache.Set(cacheKey, now + interval, interval);
            retryAfter = interval;

            return true;
        }
    }
}
