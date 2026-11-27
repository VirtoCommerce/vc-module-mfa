using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Data.Services;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public class RedisMfaCodeDeliveryLimiterTests
{
    private const string Key = "user-1:Email";

    private static readonly TimeSpan ResendInterval = TimeSpan.FromSeconds(30);

    private readonly TestTimeProvider _timeProvider = new();
    private readonly FakeRedis _redis;

    public RedisMfaCodeDeliveryLimiterTests()
    {
        _redis = new FakeRedis(_timeProvider);
    }

    [Fact]
    public async Task TryAcquireAsync_Should_RefuseResend_OnAnotherPlatformInstance_UntilIntervalEnds()
    {
        // Arrange
        var firstInstance = CreateLimiter();
        var secondInstance = CreateLimiter();

        // Act
        var firstDelivery = await firstInstance.TryAcquireAsync(Key);
        _timeProvider.Advance(TimeSpan.FromSeconds(10));
        var tooSoonDelivery = await secondInstance.TryAcquireAsync(Key);
        _timeProvider.Advance(TimeSpan.FromSeconds(20));
        var nextDelivery = await secondInstance.TryAcquireAsync(Key);

        // Assert
        Assert.True(firstDelivery.IsAcquired);
        Assert.Equal(ResendInterval, firstDelivery.RetryAfter);
        Assert.False(tooSoonDelivery.IsAcquired);
        Assert.Equal(TimeSpan.FromSeconds(20), tooSoonDelivery.RetryAfter);
        Assert.True(nextDelivery.IsAcquired);
    }

    [Fact]
    public async Task TryAcquireAsync_Should_LimitEachKeySeparately()
    {
        // Arrange
        var limiter = CreateLimiter();
        await limiter.TryAcquireAsync(Key);

        // Act
        var otherDelivery = await limiter.TryAcquireAsync("user-2:Email");

        // Assert
        Assert.True(otherDelivery.IsAcquired);
    }

    [Fact]
    public async Task ReleaseAsync_Should_AllowResend_OnAnotherPlatformInstance()
    {
        // Arrange
        var firstInstance = CreateLimiter();
        var secondInstance = CreateLimiter();
        await firstInstance.TryAcquireAsync(Key);

        // Act
        await firstInstance.ReleaseAsync(Key);

        // Assert
        Assert.True((await secondInstance.TryAcquireAsync(Key)).IsAcquired);
    }

    [Fact]
    public async Task ReleaseAsync_Should_NotThrow_When_RedisIsNotAvailable()
    {
        // Arrange
        var limiter = CreateLimiter();
        _redis.IsAvailable = false;

        // Act
        var exception = await Record.ExceptionAsync(() => limiter.ReleaseAsync(Key));

        // Assert
        Assert.Null(exception);
    }

    private RedisMfaCodeDeliveryLimiter CreateLimiter()
    {
        var options = new MfaOptions { CodeResendInterval = ResendInterval };

        return new RedisMfaCodeDeliveryLimiter(_redis.Connection, Options.Create(options), NullLogger<RedisMfaCodeDeliveryLimiter>.Instance);
    }
}
