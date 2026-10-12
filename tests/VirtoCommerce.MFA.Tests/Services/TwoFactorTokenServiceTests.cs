using System;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Data.Services;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public class TwoFactorTokenServiceTests
{
    private static readonly TwoFactorTicket _ticket = new()
    {
        UserId = "user-1",
        StoreId = "store-1",
        IssuedAt = DateTimeOffset.UtcNow,
        SecurityStamp = "stamp",
    };

    [Fact]
    public void ReadToken_Should_ReturnTheTicket_When_TokenIsValid()
    {
        // Arrange
        var service = CreateService(TimeSpan.FromMinutes(10));

        // Act
        var ticket = service.ReadToken(service.CreateToken(_ticket));

        // Assert
        Assert.Equal(_ticket.UserId, ticket.UserId);
        Assert.Equal(_ticket.StoreId, ticket.StoreId);
        Assert.Equal(_ticket.SecurityStamp, ticket.SecurityStamp);
    }

    [Fact]
    public void ReadToken_Should_ReturnNull_When_TokenExpired()
    {
        // Arrange
        var service = CreateService(TimeSpan.FromSeconds(-1));

        // Act
        var ticket = service.ReadToken(service.CreateToken(_ticket));

        // Assert
        Assert.Null(ticket);
    }

    [Fact]
    public void ReadToken_Should_ReturnNull_When_TheTicketWasIssuedLongerAgoThanTheLifetime()
    {
        // Arrange
        var service = CreateService(TimeSpan.FromMinutes(10));
        var reissuedTicket = new TwoFactorTicket { UserId = "user-1", IssuedAt = DateTimeOffset.UtcNow.AddMinutes(-11) };

        // Act
        var ticket = service.ReadToken(service.CreateToken(reissuedTicket));

        // Assert
        Assert.Null(ticket);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    public void ReadToken_Should_ReturnNull_When_TokenIsMalformed(string token)
    {
        // Arrange
        var service = CreateService(TimeSpan.FromMinutes(10));

        // Act
        var ticket = service.ReadToken(token);

        // Assert
        Assert.Null(ticket);
    }

    [Fact]
    public void ReadToken_Should_ReturnNull_When_TokenIsTamperedWith()
    {
        // Arrange
        var service = CreateService(TimeSpan.FromMinutes(10));
        var token = service.CreateToken(_ticket);
        var tamperedToken = token[..^2] + (token[^2] == 'A' ? 'B' : 'A') + token[^1];

        // Act
        var ticket = service.ReadToken(tamperedToken);

        // Assert
        Assert.Null(ticket);
    }

    private static TwoFactorTokenService CreateService(TimeSpan lifetime)
    {
        return new TwoFactorTokenService(new EphemeralDataProtectionProvider(), Options.Create(new MfaOptions { TwoFactorTokenLifetime = lifetime }));
    }
}
