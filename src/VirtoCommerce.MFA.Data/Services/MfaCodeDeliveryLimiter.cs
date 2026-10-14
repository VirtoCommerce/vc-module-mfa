using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.MFA.Data.Services;

public class MfaCodeDeliveryLimiter(
    IMemoryCache memoryCache,
    IOptions<MfaOptions> options,
    TimeProvider timeProvider)
    : IMfaCodeDeliveryLimiter
{
    private const string CacheKeyPrefix = "VirtoCommerce.MFA.CodeDelivery:";

    private readonly Lock _lock = new();

    public virtual Task<MfaCodeDeliveryResult> TryAcquireAsync(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var cacheKey = CacheKeyPrefix + key;
        var interval = options.Value.CodeResendInterval;
        var result = AbstractTypeFactory<MfaCodeDeliveryResult>.TryCreateInstance();

        lock (_lock)
        {
            var now = timeProvider.GetUtcNow();

            if (memoryCache.TryGetValue(cacheKey, out DateTimeOffset allowedAt) && allowedAt > now)
            {
                result.RetryAfter = allowedAt - now;
                return Task.FromResult(result);
            }

            memoryCache.Set(cacheKey, now + interval, interval);
        }

        result.IsAcquired = true;
        result.RetryAfter = interval;

        return Task.FromResult(result);
    }

    public virtual Task ReleaseAsync(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        lock (_lock)
        {
            memoryCache.Remove(CacheKeyPrefix + key);
        }

        return Task.CompletedTask;
    }
}
