using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;

namespace VirtoCommerce.MFA.Data.Services;

public class MfaCodeAttemptLimiter(
    IMemoryCache memoryCache,
    IOptions<MfaOptions> options,
    TimeProvider timeProvider)
    : IMfaCodeAttemptLimiter
{
    private const string CacheKeyPrefix = "VirtoCommerce.MFA.CodeAttempts:";

    private readonly Lock _lock = new();

    public virtual Task<bool> HasAttemptsLeftAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        lock (_lock)
        {
            var window = GetWindow(userId);

            return Task.FromResult(window == null || window.Attempts < options.Value.MaxCodeAttempts);
        }
    }

    public virtual Task<bool> TryConsumeAttemptAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        lock (_lock)
        {
            var window = GetWindow(userId);
            if (window == null)
            {
                var lifetime = options.Value.TwoFactorTokenLifetime;
                window = new AttemptWindow { EndsAt = timeProvider.GetUtcNow() + lifetime };
                memoryCache.Set(CacheKeyPrefix + userId, window, lifetime);
            }

            if (window.Attempts >= options.Value.MaxCodeAttempts)
            {
                return Task.FromResult(false);
            }

            window.Attempts++;

            return Task.FromResult(true);
        }
    }

    public virtual Task ResetAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        lock (_lock)
        {
            memoryCache.Remove(CacheKeyPrefix + userId);
        }

        return Task.CompletedTask;
    }

    private AttemptWindow GetWindow(string userId)
    {
        return memoryCache.TryGetValue(CacheKeyPrefix + userId, out AttemptWindow window) && window.EndsAt > timeProvider.GetUtcNow()
            ? window
            : null;
    }

    private sealed class AttemptWindow
    {
        public DateTimeOffset EndsAt { get; init; }

        public int Attempts { get; set; }
    }
}
