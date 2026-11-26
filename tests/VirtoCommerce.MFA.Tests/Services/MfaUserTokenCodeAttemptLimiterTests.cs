using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.MFA.Web;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Security.Repositories;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public sealed class MfaUserTokenCodeAttemptLimiterTests : IDisposable
{
    private const int MaxCodeAttempts = 3;
    private const string UserId = "user-1";

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"mfa-attempts-{Guid.NewGuid():N}.db");
    private readonly TestTimeProvider _timeProvider = new();
    private readonly List<ServiceProvider> _instances = [];

    public MfaUserTokenCodeAttemptLimiterTests()
    {
        using var dbContext = CreateDbContext();
        dbContext.Database.EnsureCreated();
        dbContext.Users.Add(new ApplicationUser { Id = UserId, UserName = UserId });
        dbContext.Users.Add(new ApplicationUser { Id = "user-2", UserName = "user-2" });
        dbContext.SaveChanges();
    }

    public void Dispose()
    {
        foreach (var instance in _instances)
        {
            instance.Dispose();
        }

        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }

    [Fact]
    public async Task TryConsumeAttemptAsync_Should_ShareCount_When_AttemptsComeToDifferentPlatformInstances()
    {
        // Arrange
        var firstInstance = CreatePlatformInstanceLimiter();
        var secondInstance = CreatePlatformInstanceLimiter();

        // Act
        var results = new List<bool>();
        for (var i = 0; i <= MaxCodeAttempts; i++)
        {
            var limiter = i % 2 == 0 ? firstInstance : secondInstance;
            results.Add(await limiter.TryConsumeAttemptAsync(UserId));
        }

        // Assert
        Assert.All(results.Take(MaxCodeAttempts), Assert.True);
        Assert.False(results[MaxCodeAttempts]);
        Assert.False(await firstInstance.HasAttemptsLeftAsync(UserId));
        Assert.False(await secondInstance.HasAttemptsLeftAsync(UserId));
    }

    [Fact]
    public async Task TryConsumeAttemptAsync_Should_NotExceedLimit_When_AttemptsRunInParallelOnDifferentPlatformInstances()
    {
        // Arrange
        var limiters = new[] { CreatePlatformInstanceLimiter(), CreatePlatformInstanceLimiter() };

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(i => Task.Run(() => limiters[i % limiters.Length].TryConsumeAttemptAsync(UserId))));

        // Assert
        Assert.Equal(MaxCodeAttempts, results.Count(x => x));
    }

    [Fact]
    public async Task ResetAsync_Should_RestoreAttempts_When_ResetOnAnotherPlatformInstance()
    {
        // Arrange
        var firstInstance = CreatePlatformInstanceLimiter();
        var secondInstance = CreatePlatformInstanceLimiter();
        for (var i = 0; i < MaxCodeAttempts; i++)
        {
            await firstInstance.TryConsumeAttemptAsync(UserId);
        }

        // Act
        await secondInstance.ResetAsync(UserId);

        // Assert
        Assert.True(await firstInstance.HasAttemptsLeftAsync(UserId));
        Assert.True(await firstInstance.TryConsumeAttemptAsync(UserId));
    }

    [Fact]
    public async Task TryConsumeAttemptAsync_Should_CountEachUserSeparately()
    {
        // Arrange
        var limiter = CreatePlatformInstanceLimiter();
        for (var i = 0; i < MaxCodeAttempts; i++)
        {
            await limiter.TryConsumeAttemptAsync(UserId);
        }

        // Act
        var isOtherUserAllowed = await limiter.TryConsumeAttemptAsync("user-2");

        // Assert
        Assert.False(await limiter.HasAttemptsLeftAsync(UserId));
        Assert.True(isOtherUserAllowed);
    }

    [Fact]
    public async Task TryConsumeAttemptAsync_Should_StartNewWindow_When_WindowEnds()
    {
        // Arrange
        var limiter = CreatePlatformInstanceLimiter();
        for (var i = 0; i < MaxCodeAttempts; i++)
        {
            await limiter.TryConsumeAttemptAsync(UserId);
        }

        _timeProvider.Advance(Window);

        // Act
        var hasAttemptsLeft = await limiter.HasAttemptsLeftAsync(UserId);
        var isAllowed = await limiter.TryConsumeAttemptAsync(UserId);

        // Assert
        Assert.True(hasAttemptsLeft);
        Assert.True(isAllowed);
    }

    [Fact]
    public async Task TryConsumeAttemptAsync_Should_NotExtendWindow_When_AttemptsContinue()
    {
        // Arrange
        var limiter = CreatePlatformInstanceLimiter();
        await limiter.TryConsumeAttemptAsync(UserId);
        _timeProvider.Advance(Window - TimeSpan.FromSeconds(1));
        await limiter.TryConsumeAttemptAsync(UserId);
        await limiter.TryConsumeAttemptAsync(UserId);
        _timeProvider.Advance(TimeSpan.FromSeconds(1));

        // Act
        var isAllowed = await limiter.TryConsumeAttemptAsync(UserId);

        // Assert
        Assert.True(isAllowed);
    }

    [Fact]
    public async Task TryConsumeAttemptAsync_Should_NotLoseCount_When_AnotherPlatformInstanceCountsBetweenReadAndWrite()
    {
        // Arrange
        var otherInstance = CreatePlatformInstanceLimiter();
        for (var i = 0; i < MaxCodeAttempts - 1; i++)
        {
            await otherInstance.TryConsumeAttemptAsync(UserId);
        }

        var isOtherAttemptAllowed = false;
        var interceptor = new BeforeTokenUpdateInterceptor(async () => isOtherAttemptAllowed = await otherInstance.TryConsumeAttemptAsync(UserId));
        var limiter = CreatePlatformInstanceLimiter(interceptor);

        // Act
        var isAllowed = await limiter.TryConsumeAttemptAsync(UserId);

        // Assert
        Assert.True(isOtherAttemptAllowed);
        Assert.False(isAllowed);
        Assert.False(await limiter.HasAttemptsLeftAsync(UserId));
    }

    [Fact]
    public async Task TryConsumeAttemptAsync_Should_Throw_When_SavingCountFailsWithoutConcurrentInsert()
    {
        // Arrange
        var limiter = CreatePlatformInstanceLimiter();

        // Act
        // No such user: the foreign key refuses the token, which no other request has added
        var action = () => limiter.TryConsumeAttemptAsync("unknown-user");

        // Assert
        await Assert.ThrowsAsync<DbUpdateException>(action);
    }

    private IMfaCodeAttemptLimiter CreatePlatformInstanceLimiter(IInterceptor interceptor = null)
    {
        // Each platform instance has its own service provider, memory and database connections, but the same database
        var services = new ServiceCollection();
        services.AddMemoryCache();
        services.AddSingleton<TimeProvider>(_timeProvider);
        services.AddDbContext<SecurityDbContext>(builder =>
        {
            ConfigureDbContext(builder);
            if (interceptor != null)
            {
                builder.AddInterceptors(interceptor);
            }
        });

        var moduleService = new Mock<IModuleService>();
        var module = new Module
        {
            Configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["MultiFactorAuthentication:MaxCodeAttempts"] = MaxCodeAttempts.ToString(),
                ["MultiFactorAuthentication:TwoFactorTokenLifetime"] = Window.ToString(),
            }).Build(),
            ModuleService = moduleService.Object,
        };
        module.Initialize(services);

        var serviceProvider = services.BuildServiceProvider();
        _instances.Add(serviceProvider);

        return serviceProvider.GetRequiredService<IMfaCodeAttemptLimiter>();
    }

    private SecurityDbContext CreateDbContext()
    {
        var builder = new DbContextOptionsBuilder<SecurityDbContext>();
        ConfigureDbContext(builder);

        return new SecurityDbContext(builder.Options);
    }

    private void ConfigureDbContext(DbContextOptionsBuilder builder)
    {
        builder.UseSqlite($"Data Source={_databasePath};Default Timeout=60");
    }

    private sealed class BeforeTokenUpdateInterceptor(Func<Task> action) : DbCommandInterceptor
    {
        private bool _isDone;

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_isDone && command.CommandText.Contains("AspNetUserTokens", StringComparison.Ordinal))
            {
                _isDone = true;
                await action();
            }

            return result;
        }
    }
}
