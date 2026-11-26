using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;

namespace VirtoCommerce.MFA.Data.Services;

public class RedisTwoFactorTicketStore(IConnectionMultiplexer connection, IOptions<MfaOptions> options) : ITwoFactorTicketStore
{
    private const string KeyPrefix = "vc:mfa:redeemed-ticket:";

    protected IDatabase Database => connection.GetDatabase();

    public virtual Task<bool> IsRedeemedAsync(TwoFactorTicket ticket)
    {
        return Database.KeyExistsAsync(GetKey(ticket));
    }

    public virtual Task<bool> TryRedeemAsync(TwoFactorTicket ticket)
    {
        return Database.StringSetAsync(GetKey(ticket), true, options.Value.TwoFactorTokenLifetime, When.NotExists);
    }

    public virtual Task RevokeAsync(TwoFactorTicket ticket)
    {
        return Database.StringSetAsync(GetKey(ticket), true, options.Value.TwoFactorTokenLifetime, When.Always);
    }

    protected virtual RedisKey GetKey(TwoFactorTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentException.ThrowIfNullOrEmpty(ticket.Id);

        return KeyPrefix + ticket.Id;
    }
}
