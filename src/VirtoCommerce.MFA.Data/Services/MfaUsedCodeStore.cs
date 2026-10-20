using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;

namespace VirtoCommerce.MFA.Data.Services;

public class MfaUsedCodeStore(
    IMemoryCache memoryCache,
    IOptions<MfaOptions> options,
    TimeProvider timeProvider)
    : IMfaUsedCodeStore
{
    private const string CacheKeyPrefix = "VirtoCommerce.MFA.UsedCode:";

    private readonly Lock _lock = new();

    public virtual Task<bool> TryUseAsync(string userId, string provider, string code)
    {
        var cacheKey = GetCacheKey(userId, provider, code);
        var lifetime = options.Value.UsedCodeLifetime;

        lock (_lock)
        {
            var now = timeProvider.GetUtcNow();

            if (memoryCache.TryGetValue(cacheKey, out DateTimeOffset usedUntil) && usedUntil > now)
            {
                return Task.FromResult(false);
            }

            memoryCache.Set(cacheKey, now + lifetime, lifetime);
        }

        return Task.FromResult(true);
    }

    public virtual Task ReleaseAsync(string userId, string provider, string code)
    {
        var cacheKey = GetCacheKey(userId, provider, code);

        lock (_lock)
        {
            memoryCache.Remove(cacheKey);
        }

        return Task.CompletedTask;
    }

    protected virtual string GetCacheKey(string userId, string provider, string code)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(provider);
        ArgumentException.ThrowIfNullOrEmpty(code);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{userId}\n{provider}\n{code}"));

        return CacheKeyPrefix + Convert.ToHexString(hash);
    }
}
