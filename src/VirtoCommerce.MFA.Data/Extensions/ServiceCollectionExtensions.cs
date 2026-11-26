using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.MFA.Data.Services;

namespace VirtoCommerce.MFA.Data.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMfaSignInLimits(this IServiceCollection services)
    {
        services.AddSharedSingleton<IMfaCodeAttemptLimiter, MfaCodeAttemptLimiter, RedisMfaCodeAttemptLimiter>();
        services.AddSharedSingleton<IMfaCodeDeliveryLimiter, MfaCodeDeliveryLimiter, RedisMfaCodeDeliveryLimiter>();
        services.AddSharedSingleton<ITwoFactorTicketStore, TwoFactorTicketStore, RedisTwoFactorTicketStore>();

        return services;
    }

    private static void AddSharedSingleton<TService, TInMemory, TRedis>(this IServiceCollection services)
        where TService : class
        where TInMemory : TService
        where TRedis : TService
    {
        services.AddSingleton<TService>(serviceProvider => serviceProvider.GetService<IConnectionMultiplexer>() is null
            ? ActivatorUtilities.CreateInstance<TInMemory>(serviceProvider)
            : ActivatorUtilities.CreateInstance<TRedis>(serviceProvider));
    }
}
