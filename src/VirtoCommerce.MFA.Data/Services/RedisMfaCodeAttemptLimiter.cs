using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;

namespace VirtoCommerce.MFA.Data.Services;

public class RedisMfaCodeAttemptLimiter(
    IConnectionMultiplexer connection,
    IOptions<MfaOptions> options,
    ILogger<RedisMfaCodeAttemptLimiter> logger)
    : IMfaCodeAttemptLimiter
{
    private const string KeyPrefix = "vc:mfa:code-attempts:";

    private const string ConsumeAttemptScript = """
        local attempts = redis.call('INCR', KEYS[1])
        if attempts == 1 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        return attempts
        """;

    protected IDatabase Database => connection.GetDatabase();

    public virtual async Task<bool> HasAttemptsLeftAsync(string userId)
    {
        var attempts = await Database.StringGetAsync(GetKey(userId));

        return attempts.IsNull || (long)attempts < options.Value.MaxCodeAttempts;
    }

    public virtual async Task<bool> TryConsumeAttemptAsync(string userId)
    {
        var windowMilliseconds = (long)options.Value.TwoFactorTokenLifetime.TotalMilliseconds;
        var attempts = await Database.ScriptEvaluateAsync(ConsumeAttemptScript, [GetKey(userId)], [windowMilliseconds]);

        return (long)attempts <= options.Value.MaxCodeAttempts;
    }

    public virtual async Task ResetAsync(string userId)
    {
        var key = GetKey(userId);

        try
        {
            await Database.KeyDeleteAsync(key);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The wrong code count of user {UserId} was not reset.", userId);
        }
    }

    protected virtual RedisKey GetKey(string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return KeyPrefix + userId;
    }
}
