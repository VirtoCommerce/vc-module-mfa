using System;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Data.Services;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public class TwoFactorTicketStoreTests
{
    private readonly MfaOptions _options = new() { MaxCodeAttempts = 2 };
    private readonly TwoFactorTicketStore _store;

    public TwoFactorTicketStoreTests()
    {
        _store = new TwoFactorTicketStore(new MemoryCache(new MemoryCacheOptions()), Options.Create(_options));
    }

    [Fact]
    public void TryConsumeAttempt_Should_AllowTheConfiguredNumberOfAttemptsPerTicket()
    {
        // Arrange
        var ticket = CreateTicket();
        var otherTicket = CreateTicket();

        // Act & Assert
        Assert.True(_store.TryConsumeAttempt(ticket));
        Assert.True(_store.TryConsumeAttempt(ticket));
        Assert.False(_store.TryConsumeAttempt(ticket));
        Assert.True(_store.TryConsumeAttempt(otherTicket));
    }

    [Fact]
    public void TryRedeem_Should_RedeemTheTicketOnce()
    {
        // Arrange
        var ticket = CreateTicket();

        // Act & Assert
        Assert.True(_store.TryRedeem(ticket));
        Assert.False(_store.TryRedeem(ticket));
    }

    [Fact]
    public void Revoke_Should_RefuseFurtherAttempts()
    {
        // Arrange
        var ticket = CreateTicket();

        // Act
        _store.Revoke(ticket);

        // Assert
        Assert.False(_store.TryConsumeAttempt(ticket));
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
