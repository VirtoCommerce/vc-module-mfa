using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Data.Services;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public class RedisMfaCodeAttemptLimiterTests
{
    private const int MaxCodeAttempts = 3;
    private const string UserId = "user-1";

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly TestTimeProvider _timeProvider = new();
    private readonly FakeRedis _redis;

    public RedisMfaCodeAttemptLimiterTests()
    {
        _redis = new FakeRedis(_timeProvider);
    }

    [Fact]
    public async Task TryConsumeAttemptAsync_Should_ShareAttempts_AcrossPlatformInstances()
    {
        // Arrange
        var firstInstance = CreateLimiter();
        var secondInstance = CreateLimiter();

        // Act
        bool[] results =
        [
            await firstInstance.TryConsumeAttemptAsync(UserId),
            await secondInstance.TryConsumeAttemptAsync(UserId),
            await firstInstance.TryConsumeAttemptAsync(UserId),
            await secondInstance.TryConsumeAttemptAsync(UserId),
        ];

        // Assert
        Assert.Equal([true, true, true, false], results);
        Assert.False(await firstInstance.HasAttemptsLeftAsync(UserId));
        Assert.False(await secondInstance.HasAttemptsLeftAsync(UserId));
    }

    [Fact]
    public async Task TryConsumeAttemptAsync_Should_CountAttemptsPerUser()
    {
        // Arrange
        var limiter = CreateLimiter();
        for (var i = 0; i < MaxCodeAttempts; i++)
        {
            await limiter.TryConsumeAttemptAsync(UserId);
        }

        // Act
        var isOtherUserAllowed = await limiter.TryConsumeAttemptAsync("user-2");

        // Assert
        Assert.True(isOtherUserAllowed);
        Assert.False(await limiter.HasAttemptsLeftAsync(UserId));
    }

    [Fact]
    public async Task TryConsumeAttemptAsync_Should_NotExtendWindow_When_NextAttemptIsConsumed()
    {
        // Arrange
        var limiter = CreateLimiter();
        await limiter.TryConsumeAttemptAsync(UserId);
        _timeProvider.Advance(Window - TimeSpan.FromMinutes(1));
        await limiter.TryConsumeAttemptAsync(UserId);
        await limiter.TryConsumeAttemptAsync(UserId);

        // Act
        _timeProvider.Advance(TimeSpan.FromMinutes(1));

        // Assert
        Assert.True(await limiter.HasAttemptsLeftAsync(UserId));
        for (var i = 0; i < MaxCodeAttempts; i++)
        {
            Assert.True(await limiter.TryConsumeAttemptAsync(UserId));
        }
    }

    [Fact]
    public async Task ResetAsync_Should_AllowAttempts_OnAnotherPlatformInstance()
    {
        // Arrange
        var firstInstance = CreateLimiter();
        var secondInstance = CreateLimiter();
        for (var i = 0; i < MaxCodeAttempts; i++)
        {
            await firstInstance.TryConsumeAttemptAsync(UserId);
        }

        // Act
        await firstInstance.ResetAsync(UserId);

        // Assert
        Assert.True(await secondInstance.HasAttemptsLeftAsync(UserId));
        Assert.True(await secondInstance.TryConsumeAttemptAsync(UserId));
    }

    [Fact]
    public async Task ResetAsync_Should_NotThrow_When_RedisIsNotAvailable()
    {
        // Arrange
        var limiter = CreateLimiter();
        _redis.IsAvailable = false;

        // Act
        var exception = await Record.ExceptionAsync(() => limiter.ResetAsync(UserId));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public async Task TryConsumeAttemptAsync_Should_Throw_When_RedisIsNotAvailable()
    {
        // Arrange
        var limiter = CreateLimiter();
        _redis.IsAvailable = false;

        // Act
        var consume = () => limiter.TryConsumeAttemptAsync(UserId);

        // Assert
        await Assert.ThrowsAsync<RedisConnectionException>(consume);
    }

    private RedisMfaCodeAttemptLimiter CreateLimiter()
    {
        var options = new MfaOptions { MaxCodeAttempts = MaxCodeAttempts, TwoFactorTokenLifetime = Window };

        return new RedisMfaCodeAttemptLimiter(_redis.Connection, Options.Create(options), NullLogger<RedisMfaCodeAttemptLimiter>.Instance);
    }
}
