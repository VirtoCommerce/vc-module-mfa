using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;

namespace VirtoCommerce.MFA.Data.Services;

public class TwoFactorTicketStore(IMemoryCache memoryCache, IOptions<MfaOptions> options) : ITwoFactorTicketStore
{
    private const string CacheKeyPrefix = "VirtoCommerce.MFA.RedeemedTwoFactorTicket:";

    private readonly Lock _lock = new();

    public virtual Task<bool> IsRedeemedAsync(TwoFactorTicket ticket)
    {
        return Task.FromResult(memoryCache.TryGetValue(GetCacheKey(ticket), out _));
    }

    public virtual Task<bool> TryRedeemAsync(TwoFactorTicket ticket)
    {
        var cacheKey = GetCacheKey(ticket);

        lock (_lock)
        {
            if (memoryCache.TryGetValue(cacheKey, out _))
            {
                return Task.FromResult(false);
            }

            memoryCache.Set(cacheKey, true, options.Value.TwoFactorTokenLifetime);

            return Task.FromResult(true);
        }
    }

    public virtual Task RevokeAsync(TwoFactorTicket ticket)
    {
        memoryCache.Set(GetCacheKey(ticket), true, options.Value.TwoFactorTokenLifetime);

        return Task.CompletedTask;
    }

    private static string GetCacheKey(TwoFactorTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentException.ThrowIfNullOrEmpty(ticket.Id);

        return CacheKeyPrefix + ticket.Id;
    }
}
