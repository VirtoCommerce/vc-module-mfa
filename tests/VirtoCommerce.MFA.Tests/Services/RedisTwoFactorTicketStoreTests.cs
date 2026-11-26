using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Data.Services;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public class RedisTwoFactorTicketStoreTests
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(10);

    private readonly TestTimeProvider _timeProvider = new();
    private readonly FakeRedis _redis;
    private readonly TwoFactorTicket _ticket = new() { Id = "ticket-1", UserId = "user-1" };

    public RedisTwoFactorTicketStoreTests()
    {
        _redis = new FakeRedis(_timeProvider);
    }

    [Fact]
    public async Task TryRedeemAsync_Should_RedeemTicketOnce_AcrossPlatformInstances()
    {
        // Arrange
        var firstInstance = CreateStore();
        var secondInstance = CreateStore();

        // Act
        var isRedeemedFirst = await firstInstance.TryRedeemAsync(_ticket);
        var isRedeemedSecond = await secondInstance.TryRedeemAsync(_ticket);

        // Assert
        Assert.True(isRedeemedFirst);
        Assert.False(isRedeemedSecond);
        Assert.True(await secondInstance.IsRedeemedAsync(_ticket));
    }

    [Fact]
    public async Task RevokeAsync_Should_RefuseTicket_OnAnotherPlatformInstance()
    {
        // Arrange
        var firstInstance = CreateStore();
        var secondInstance = CreateStore();

        // Act
        await firstInstance.RevokeAsync(_ticket);

        // Assert
        Assert.True(await secondInstance.IsRedeemedAsync(_ticket));
        Assert.False(await secondInstance.TryRedeemAsync(_ticket));
    }

    [Fact]
    public async Task TryRedeemAsync_Should_KeepTicketRedeemed_ForTokenLifetime()
    {
        // Arrange
        var store = CreateStore();
        await store.TryRedeemAsync(_ticket);

        // Act
        _timeProvider.Advance(TokenLifetime - TimeSpan.FromSeconds(1));
        var isRedeemedBeforeExpiry = await store.IsRedeemedAsync(_ticket);
        _timeProvider.Advance(TimeSpan.FromSeconds(1));
        var isRedeemedAfterExpiry = await store.IsRedeemedAsync(_ticket);

        // Assert
        Assert.True(isRedeemedBeforeExpiry);
        Assert.False(isRedeemedAfterExpiry);
    }

    [Fact]
    public async Task IsRedeemedAsync_Should_ReturnFalse_When_TicketIsNotRedeemed()
    {
        // Act
        var isRedeemed = await CreateStore().IsRedeemedAsync(_ticket);

        // Assert
        Assert.False(isRedeemed);
    }

    private RedisTwoFactorTicketStore CreateStore()
    {
        return new RedisTwoFactorTicketStore(_redis.Connection, Options.Create(new MfaOptions { TwoFactorTokenLifetime = TokenLifetime }));
    }
}
