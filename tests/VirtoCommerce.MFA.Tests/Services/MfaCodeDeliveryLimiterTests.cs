using System;
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
    public void TryAcquire_Should_AllowTheFirstDelivery_And_ReportTheWholeInterval()
    {
        // Act
        var isAcquired = _limiter.TryAcquire("user-1:Email", out var retryAfter);

        // Assert
        Assert.True(isAcquired);
        Assert.Equal(Interval, retryAfter);
    }

    [Fact]
    public void TryAcquire_Should_RefuseADeliveryWithinTheInterval_And_ReportTheTimeLeft()
    {
        // Arrange
        _limiter.TryAcquire("user-1:Email", out _);
        _timeProvider.Advance(TimeSpan.FromSeconds(24));

        // Act
        var isAcquired = _limiter.TryAcquire("user-1:Email", out var retryAfter);

        // Assert
        Assert.False(isAcquired);
        Assert.Equal(TimeSpan.FromSeconds(6), retryAfter);
    }

    [Fact]
    public void TryAcquire_Should_NotExtendTheInterval_When_ADeliveryIsRefused()
    {
        // Arrange
        _limiter.TryAcquire("user-1:Email", out _);
        _timeProvider.Advance(TimeSpan.FromSeconds(20));
        _limiter.TryAcquire("user-1:Email", out _);
        _timeProvider.Advance(TimeSpan.FromSeconds(10));

        // Act
        var isAcquired = _limiter.TryAcquire("user-1:Email", out var retryAfter);

        // Assert
        Assert.True(isAcquired);
        Assert.Equal(Interval, retryAfter);
    }

    [Fact]
    public void TryAcquire_Should_LimitEachKeySeparately()
    {
        // Arrange
        _limiter.TryAcquire("user-1:Email", out _);

        // Act
        var isAcquired = _limiter.TryAcquire("user-2:Email", out _);

        // Assert
        Assert.True(isAcquired);
    }
}
