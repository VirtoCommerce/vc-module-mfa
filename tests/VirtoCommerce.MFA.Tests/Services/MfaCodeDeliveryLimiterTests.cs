using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Data.Services;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public class MfaCodeDeliveryLimiterTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private readonly TestTimeProvider _timeProvider = new();
    private readonly MfaCodeDeliveryLimiter _limiter;

    public MfaCodeDeliveryLimiterTests()
    {
        _limiter = new MfaCodeDeliveryLimiter(new MemoryCache(new MemoryCacheOptions()), Options.Create(new MfaOptions { CodeResendInterval = Interval }), _timeProvider);
    }

    [Fact]
    public async Task TryAcquireAsync_Should_AllowTheFirstDelivery_And_ReportTheWholeInterval()
    {
        // Act
        var result = await _limiter.TryAcquireAsync("user-1:Email");

        // Assert
        Assert.True(result.IsAcquired);
        Assert.Equal(Interval, result.RetryAfter);
    }

    [Fact]
    public async Task TryAcquireAsync_Should_RefuseADeliveryWithinTheInterval_And_ReportTheTimeLeft()
    {
        // Arrange
        await _limiter.TryAcquireAsync("user-1:Email");
        _timeProvider.Advance(TimeSpan.FromSeconds(24));

        // Act
        var result = await _limiter.TryAcquireAsync("user-1:Email");

        // Assert
        Assert.False(result.IsAcquired);
        Assert.Equal(TimeSpan.FromSeconds(6), result.RetryAfter);
    }

    [Fact]
    public async Task TryAcquireAsync_Should_NotExtendTheInterval_When_ADeliveryIsRefused()
    {
        // Arrange
        await _limiter.TryAcquireAsync("user-1:Email");
        _timeProvider.Advance(TimeSpan.FromSeconds(20));
        await _limiter.TryAcquireAsync("user-1:Email");
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        // Act
        var result = await _limiter.TryAcquireAsync("user-1:Email");

        // Assert
        Assert.True(result.IsAcquired);
        Assert.Equal(Interval, result.RetryAfter);
    }

    [Fact]
    public async Task TryAcquireAsync_Should_LimitEachKeySeparately()
    {
        // Arrange
        await _limiter.TryAcquireAsync("user-1:Email");

        // Act
        var result = await _limiter.TryAcquireAsync("user-2:Email");

        // Assert
        Assert.True(result.IsAcquired);
    }

    [Fact]
    public async Task ReleaseAsync_Should_AllowTheNextDeliveryRightAway()
    {
        // Arrange
        await _limiter.TryAcquireAsync("user-1:Email");

        // Act
        await _limiter.ReleaseAsync("user-1:Email");
        var result = await _limiter.TryAcquireAsync("user-1:Email");

        // Assert
        Assert.True(result.IsAcquired);
    }
}
