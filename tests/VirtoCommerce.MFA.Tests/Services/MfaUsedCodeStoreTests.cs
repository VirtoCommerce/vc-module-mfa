using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Data.Services;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public class MfaUsedCodeStoreTests
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly TestTimeProvider _timeProvider = new();
    private readonly MfaUsedCodeStore _store;

    public MfaUsedCodeStoreTests()
    {
        _store = new MfaUsedCodeStore(new MemoryCache(new MemoryCacheOptions()), Options.Create(new MfaOptions { UsedCodeLifetime = Lifetime }), _timeProvider);
    }

    [Fact]
    public async Task TryUseAsync_Should_AcceptACodeOnlyOnce()
    {
        // Arrange
        await _store.TryUseAsync("user-1", "Email", "246810");

        // Act
        var isAccepted = await _store.TryUseAsync("user-1", "Email", "246810");

        // Assert
        Assert.False(isAccepted);
    }

    [Fact]
    public async Task TryUseAsync_Should_AcceptTheCodeAgain_When_ItsLifetimeIsOver()
    {
        // Arrange
        await _store.TryUseAsync("user-1", "Email", "246810");
        _timeProvider.Advance(Lifetime);

        // Act
        var isAccepted = await _store.TryUseAsync("user-1", "Email", "246810");

        // Assert
        Assert.True(isAccepted);
    }

    [Theory]
    [InlineData("user-2", "Email", "246810")]
    [InlineData("user-1", "Authenticator", "246810")]
    [InlineData("user-1", "Email", "135791")]
    public async Task TryUseAsync_Should_KeepCodesApart_When_TheUserTheProviderOrTheCodeDiffers(string userId, string provider, string code)
    {
        // Arrange
        await _store.TryUseAsync("user-1", "Email", "246810");

        // Act
        var isAccepted = await _store.TryUseAsync(userId, provider, code);

        // Assert
        Assert.True(isAccepted);
    }

    [Fact]
    public async Task ReleaseAsync_Should_LetTheCodeBeUsedAgain()
    {
        // Arrange
        await _store.TryUseAsync("user-1", "Email", "246810");
        await _store.ReleaseAsync("user-1", "Email", "246810");

        // Act
        var isAccepted = await _store.TryUseAsync("user-1", "Email", "246810");

        // Assert
        Assert.True(isAccepted);
    }
}
