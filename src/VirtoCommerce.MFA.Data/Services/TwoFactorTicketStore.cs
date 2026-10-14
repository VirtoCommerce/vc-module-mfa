using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;

namespace VirtoCommerce.MFA.Data.Services;

public class TwoFactorTicketStore(IMemoryCache memoryCache, IOptions<MfaOptions> options) : ITwoFactorTicketStore
{
    private const string CacheKeyPrefix = "VirtoCommerce.MFA.TwoFactorTicket:";

    private readonly object _lock = new();

    public virtual bool HasAttemptsLeft(TwoFactorTicket ticket)
    {
        lock (_lock)
        {
            return GetState(ticket).Attempts < options.Value.MaxCodeAttempts;
        }
    }

    public virtual bool IsRedeemed(TwoFactorTicket ticket)
    {
        lock (_lock)
        {
            return GetState(ticket).IsRedeemed;
        }
    }

    public virtual bool TryConsumeAttempt(TwoFactorTicket ticket)
    {
        lock (_lock)
        {
            var state = GetState(ticket);
            if (state.Attempts >= options.Value.MaxCodeAttempts)
            {
                return false;
            }

            state.Attempts++;

            return true;
        }
    }

    public virtual bool TryRedeem(TwoFactorTicket ticket)
    {
        lock (_lock)
        {
            var state = GetState(ticket);
            if (state.IsRedeemed)
            {
                return false;
            }

            state.IsRedeemed = true;

            return true;
        }
    }

    public virtual void Revoke(TwoFactorTicket ticket)
    {
        lock (_lock)
        {
            GetState(ticket).Attempts = options.Value.MaxCodeAttempts;
        }
    }

    private TicketState GetState(TwoFactorTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentException.ThrowIfNullOrEmpty(ticket.Id);

        var cacheKey = CacheKeyPrefix + ticket.Id;

        if (!memoryCache.TryGetValue(cacheKey, out TicketState state))
        {
            state = new TicketState();
            memoryCache.Set(cacheKey, state, options.Value.TwoFactorTokenLifetime);
        }

        return state;
    }

    private sealed class TicketState
    {
        public int Attempts { get; set; }

        public bool IsRedeemed { get; set; }
    }
}
