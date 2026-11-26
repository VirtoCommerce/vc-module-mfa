using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace VirtoCommerce.MFA.Tests;

public sealed class FakeRedis
{
    private readonly Dictionary<string, Entry> _entries = [];
    private readonly Lock _lock = new();
    private readonly TimeProvider _timeProvider;

    public FakeRedis(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;

        var database = DispatchProxy.Create<IDatabase, Proxy>();
        ((Proxy)(object)database).Handler = HandleDatabase;
        Connection = DispatchProxy.Create<IConnectionMultiplexer, Proxy>();
        ((Proxy)(object)Connection).Handler = (method, _) => method.Name == nameof(IConnectionMultiplexer.GetDatabase)
            ? database
            : throw new NotSupportedException(method.Name);
    }

    public IConnectionMultiplexer Connection { get; }

    public bool IsAvailable { get; set; } = true;

    private object HandleDatabase(MethodInfo method, object[] args)
    {
        if (!IsAvailable)
        {
            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, CommandFlags.None, "Redis is not available.", null, CommandStatus.Unknown);
        }

        lock (_lock)
        {
            return method.Name switch
            {
                nameof(IDatabase.StringGetAsync) => Task.FromResult(TryGet((RedisKey)args[0], out var entry) ? entry.Value : RedisValue.Null),
                nameof(IDatabase.StringSetAsync) => Task.FromResult(Set((RedisKey)args[0], (RedisValue)args[1], GetExpiry(args), args.OfType<When>().Single())),
                nameof(IDatabase.KeyExistsAsync) => Task.FromResult(TryGet((RedisKey)args[0], out _)),
                nameof(IDatabase.KeyDeleteAsync) => Task.FromResult(_entries.Remove((RedisKey)args[0])),
                nameof(IDatabase.KeyTimeToLiveAsync) => Task.FromResult(TryGet((RedisKey)args[0], out var entry) ? entry.ExpiresAt - _timeProvider.GetUtcNow() : null),
                nameof(IDatabase.ScriptEvaluateAsync) => Task.FromResult(IncrementWithExpiry(((RedisKey[])args[1])[0], (long)((RedisValue[])args[2])[0])),
                _ => throw new NotSupportedException(method.Name),
            };
        }
    }

    private bool Set(string key, RedisValue value, TimeSpan? expiry, When when)
    {
        if (when == When.NotExists && TryGet(key, out _))
        {
            return false;
        }

        _entries[key] = new Entry(value, _timeProvider.GetUtcNow() + expiry);

        return true;
    }

    private RedisResult IncrementWithExpiry(string key, long expiryMilliseconds)
    {
        var exists = TryGet(key, out var entry);
        var attempts = exists ? (long)entry.Value + 1 : 1;
        var expiresAt = exists ? entry.ExpiresAt : _timeProvider.GetUtcNow().AddMilliseconds(expiryMilliseconds);
        _entries[key] = new Entry(attempts, expiresAt);

        return RedisResult.Create(attempts);
    }

    private bool TryGet(string key, out Entry entry)
    {
        if (_entries.TryGetValue(key, out entry) && entry.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            _entries.Remove(key);
        }

        return _entries.TryGetValue(key, out entry);
    }

    private static TimeSpan? GetExpiry(object[] args)
    {
        return args.OfType<TimeSpan>().Cast<TimeSpan?>().SingleOrDefault();
    }

    private sealed record Entry(RedisValue Value, DateTimeOffset? ExpiresAt);

    public class Proxy : DispatchProxy
    {
        public Func<MethodInfo, object[], object> Handler { get; set; }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            return Handler(targetMethod, args);
        }
    }
}
