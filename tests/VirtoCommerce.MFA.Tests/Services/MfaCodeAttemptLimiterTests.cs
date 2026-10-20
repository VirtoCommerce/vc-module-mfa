using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Data.Services;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public class MfaCodeAttemptLimiterTests
{
    private const int MaxCodeAttempts = 3;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly TestTimeProvider _timeProvider = new();
    private readonly MfaCodeAttemptLimiter _limiter;

    public MfaCodeAttemptLimiterTests()
    {
        var options = new MfaOptions { MaxCodeAttempts = MaxCodeAttempts, TwoFactorTokenLifetime = Window };
        _limiter = new MfaCodeAttemptLimiter(new MemoryCache(new MemoryCacheOptions()), Options.Create(options), _timeProvider);
    }

    [Fact]
    public async Task TryConsumeAttemptAsync_Should_AllowTheConfiguredNumberOfAttemptsPerUser()
    {
        // Act
        var results = new List<bool>();
        for (var i = 0; i <= MaxCodeAttempts; i++)
        {
            results.Add(await _limiter.TryConsumeAttemptAsync("user-1"));
        }

        var hasAttemptsLeft = await _limiter.HasAttemptsLeftAsync("user-1");
        var isOtherUserAllowed = await _limiter.TryConsumeAttemptAsync("user-2");

        // Assert
        Assert.All(results.Take(MaxCodeAttempts), Assert.True);
        Assert.False(results[MaxCodeAttempts]);
        Assert.False(hasAttemptsLeft);
        Assert.True(isOtherUserAllowed);
    }

    [Fact]
    public async Task TryConsumeAttemptAsync_Should_NotExceedTheLimit_When_AttemptsRunInParallel()
    {
        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(() => _limiter.TryConsumeAttemptAsync("user-1"))));

        // Assert
        Assert.Equal(MaxCodeAttempts, results.Count(x => x));
    }

    [Fact]
    public async Task TryConsumeAttemptAsync_Should_StartANewWindow_When_TheWindowEnds()
    {
        // Arrange
        for (var i = 0; i < MaxCodeAttempts; i++)
        {
            await _limiter.TryConsumeAttemptAsync("user-1");
        }

        _timeProvider.Advance(Window);

        // Act
        var hasAttemptsLeft = await _limiter.HasAttemptsLeftAsync("user-1");
        var isAllowed = await _limiter.TryConsumeAttemptAsync("user-1");

        // Assert
        Assert.True(hasAttemptsLeft);
        Assert.True(isAllowed);
    }

    [Fact]
    public async Task TryConsumeAttemptAsync_Should_NotExtendTheWindow_When_AttemptsContinue()
    {
        // Arrange
        await _limiter.TryConsumeAttemptAsync("user-1");
        _timeProvider.Advance(Window - TimeSpan.FromSeconds(1));
        await _limiter.TryConsumeAttemptAsync("user-1");
        await _limiter.TryConsumeAttemptAsync("user-1");
        _timeProvider.Advance(TimeSpan.FromSeconds(1));

        // Act
        var isAllowed = await _limiter.TryConsumeAttemptAsync("user-1");

        // Assert
        Assert.True(isAllowed);
    }

    [Fact]
    public async Task ResetAsync_Should_RestoreTheAttempts()
    {
        // Arrange
        for (var i = 0; i < MaxCodeAttempts; i++)
        {
            await _limiter.TryConsumeAttemptAsync("user-1");
        }

        // Act
        await _limiter.ResetAsync("user-1");

        // Assert
        Assert.True(await _limiter.HasAttemptsLeftAsync("user-1"));
    }
}
