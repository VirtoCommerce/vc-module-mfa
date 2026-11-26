using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.MFA.Data.Services;

public class RedisMfaCodeDeliveryLimiter(
    IConnectionMultiplexer connection,
    IOptions<MfaOptions> options,
    ILogger<RedisMfaCodeDeliveryLimiter> logger)
    : IMfaCodeDeliveryLimiter
{
    private const string KeyPrefix = "vc:mfa:code-delivery:";

    protected IDatabase Database => connection.GetDatabase();

    public virtual async Task<MfaCodeDeliveryResult> TryAcquireAsync(string key)
    {
        var redisKey = GetKey(key);
        var interval = options.Value.CodeResendInterval;
        var result = AbstractTypeFactory<MfaCodeDeliveryResult>.TryCreateInstance();

        if (await Database.StringSetAsync(redisKey, true, interval, When.NotExists))
        {
            result.IsAcquired = true;
            result.RetryAfter = interval;

            return result;
        }

        result.RetryAfter = await Database.KeyTimeToLiveAsync(redisKey) ?? TimeSpan.Zero;

        return result;
    }

    public virtual async Task ReleaseAsync(string key)
    {
        var redisKey = GetKey(key);

        try
        {
            await Database.KeyDeleteAsync(redisKey);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The code resend limit {Key} was not released.", key);
        }
    }

    protected virtual RedisKey GetKey(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        return KeyPrefix + key;
    }
}
