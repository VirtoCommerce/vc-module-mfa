using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.MFA.Data.Providers;
using VirtoCommerce.MFA.Web;
using VirtoCommerce.Platform.Core.Security;
using Xunit;

namespace VirtoCommerce.MFA.Tests;

[Trait("Category", "Unit")]
public class ModuleTests
{
    [Theory]
    [InlineData(typeof(IMfaCodeAttemptLimiter))]
    [InlineData(typeof(IMfaCodeDeliveryLimiter))]
    [InlineData(typeof(ITwoFactorTicketStore))]
    public void Initialize_Should_ShareTheLimitsAcrossRequests(Type serviceType)
    {
        // Act
        var services = Initialize();

        // Assert
        Assert.Equal(ServiceLifetime.Singleton, services.Single(x => x.ServiceType == serviceType).Lifetime);
    }

    [Fact]
    public void Initialize_Should_KeepTheTwoFactorCookieAsLongAsTheTwoFactorToken()
    {
        // Arrange
        using var serviceProvider = Initialize(new() { ["MultiFactorAuthentication:TwoFactorTokenLifetime"] = "00:05:00" }).BuildServiceProvider();

        // Act
        var cookieOptions = serviceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.TwoFactorUserIdScheme);

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(5), cookieOptions.ExpireTimeSpan);
        Assert.False(cookieOptions.SlidingExpiration);
    }

    [Fact]
    public void Initialize_Should_ReplaceTheIdentityEmailProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddIdentityCore<ApplicationUser>().AddDefaultTokenProviders();

        // Act
        Initialize(services: services);
        using var serviceProvider = services.BuildServiceProvider();

        // Assert
        var providerType = serviceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value.Tokens.ProviderMap[TokenOptions.DefaultEmailProvider].ProviderType;
        Assert.Equal(typeof(MfaEmailTokenProvider), providerType);
        Assert.IsType<MfaEmailTokenProvider>(serviceProvider.GetRequiredService(providerType));
    }

    [Theory]
    [InlineData("MultiFactorAuthentication:TwoFactorTokenLifetime", "00:00:00")]
    [InlineData("MultiFactorAuthentication:CodeResendInterval", "00:00:00")]
    [InlineData("MultiFactorAuthentication:CodeResendInterval", "00:10:00")]
    [InlineData("MultiFactorAuthentication:MaxCodeAttempts", "0")]
    public void Initialize_Should_RejectInvalidOptions(string key, string value)
    {
        // Arrange
        using var serviceProvider = Initialize(new() { [key] = value }).BuildServiceProvider();

        // Act
        var readOptions = () => serviceProvider.GetRequiredService<IOptions<MfaOptions>>().Value;

        // Assert
        Assert.Throws<OptionsValidationException>(readOptions);
    }

    private static ServiceCollection Initialize(Dictionary<string, string> configuration = null, ServiceCollection services = null)
    {
        services ??= new ServiceCollection();
        var module = new Module
        {
            Configuration = new ConfigurationBuilder().AddInMemoryCollection(configuration ?? []).Build(),
        };

        module.Initialize(services);

        return services;
    }
}
