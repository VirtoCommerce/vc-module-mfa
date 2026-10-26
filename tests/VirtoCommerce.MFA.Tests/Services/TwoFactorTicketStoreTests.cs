using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Data.Services;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public class TwoFactorTicketStoreTests
{
    private readonly TwoFactorTicketStore _store = new(new MemoryCache(new MemoryCacheOptions()), Options.Create(new MfaOptions()));

    [Fact]
    public async Task TryRedeemAsync_Should_RedeemTheTicketOnce()
    {
        // Arrange
        var ticket = CreateTicket();
        var otherTicket = CreateTicket();

        // Act
        var isFirstRedeemed = await _store.TryRedeemAsync(ticket);
        var isSecondRedeemed = await _store.TryRedeemAsync(ticket);
        var isTicketRedeemed = await _store.IsRedeemedAsync(ticket);
        var isOtherTicketRedeemed = await _store.IsRedeemedAsync(otherTicket);

        // Assert
        Assert.True(isFirstRedeemed);
        Assert.False(isSecondRedeemed);
        Assert.True(isTicketRedeemed);
        Assert.False(isOtherTicketRedeemed);
    }

    [Fact]
    public async Task RevokeAsync_Should_RefuseTheTicket()
    {
        // Arrange
        var ticket = CreateTicket();

        // Act
        await _store.RevokeAsync(ticket);

        // Assert
        Assert.True(await _store.IsRedeemedAsync(ticket));
        Assert.False(await _store.TryRedeemAsync(ticket));
    }

    private static TwoFactorTicket CreateTicket()
    {
        return new TwoFactorTicket
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = "user-1",
        };
    }
}
