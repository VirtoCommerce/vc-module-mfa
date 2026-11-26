using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.Platform.Security.Repositories;

namespace VirtoCommerce.MFA.Data.Services;

public class MfaUserTokenCodeAttemptLimiter(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<MfaOptions> options,
    TimeProvider timeProvider,
    ILogger<MfaUserTokenCodeAttemptLimiter> logger)
    : IMfaCodeAttemptLimiter
{
    private const int MaxSwapAttempts = 10;
    private const char Separator = '|';

    public virtual Task<bool> HasAttemptsLeftAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return UseDbContextAsync(async dbContext =>
        {
            var token = await GetTokenQuery(dbContext, userId).AsNoTracking().FirstOrDefaultAsync();
            var window = ReadWindow(token?.Value);

            return window == null || window.Value.Attempts < options.Value.MaxCodeAttempts;
        });
    }

    public virtual Task<bool> TryConsumeAttemptAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        return UseDbContextAsync(async dbContext =>
        {
            for (var i = 0; i < MaxSwapAttempts; i++)
            {
                var token = await GetTokenQuery(dbContext, userId).AsNoTracking().FirstOrDefaultAsync();
                var window = ReadWindow(token?.Value) ?? (Attempts: 0, EndsAt: timeProvider.GetUtcNow() + options.Value.TwoFactorTokenLifetime);

                if (window.Attempts >= options.Value.MaxCodeAttempts)
                {
                    return false;
                }

                var newValue = WriteWindow(window.Attempts + 1, window.EndsAt);

                if (token == null
                    ? await TryAddTokenAsync(dbContext, userId, newValue)
                    : await TryReplaceTokenValueAsync(dbContext, userId, token.Value, newValue))
                {
                    return true;
                }
            }

            return false;
        });
    }

    public virtual async Task ResetAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        try
        {
            await UseDbContextAsync(dbContext => GetTokenQuery(dbContext, userId).ExecuteDeleteAsync());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "The wrong code count of user {UserId} was not reset.", userId);
        }
    }

    protected virtual IQueryable<IdentityUserToken<string>> GetTokenQuery(SecurityDbContext dbContext, string userId)
    {
        return dbContext.UserTokens.Where(x =>
            x.UserId == userId &&
            x.LoginProvider == ModuleConstants.Security.LoginProvider &&
            x.Name == ModuleConstants.Security.CodeAttemptsName);
    }

    protected virtual async Task<bool> TryAddTokenAsync(SecurityDbContext dbContext, string userId, string value)
    {
        dbContext.UserTokens.Add(new IdentityUserToken<string>
        {
            UserId = userId,
            LoginProvider = ModuleConstants.Security.LoginProvider,
            Name = ModuleConstants.Security.CodeAttemptsName,
            Value = value,
        });

        try
        {
            await dbContext.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();

            if (!await GetTokenQuery(dbContext, userId).AnyAsync())
            {
                throw;
            }

            return false;
        }
    }

    protected virtual async Task<bool> TryReplaceTokenValueAsync(SecurityDbContext dbContext, string userId, string oldValue, string newValue)
    {
        var updatedCount = await GetTokenQuery(dbContext, userId)
            .Where(x => x.Value == oldValue)
            .ExecuteUpdateAsync(x => x.SetProperty(token => token.Value, newValue));

        return updatedCount == 1;
    }

    private async Task<T> UseDbContextAsync<T>(Func<SecurityDbContext, Task<T>> action)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();

        return await action(dbContext);
    }

    private (int Attempts, DateTimeOffset EndsAt)? ReadWindow(string value)
    {
        var parts = value?.Split(Separator);

        if (parts is [var attemptsPart, var endsAtPart] &&
            int.TryParse(attemptsPart, NumberStyles.None, CultureInfo.InvariantCulture, out var attempts) &&
            long.TryParse(endsAtPart, NumberStyles.None, CultureInfo.InvariantCulture, out var endsAtTicks) &&
            endsAtTicks <= DateTimeOffset.MaxValue.UtcTicks)
        {
            var endsAt = new DateTimeOffset(endsAtTicks, TimeSpan.Zero);
            if (endsAt > timeProvider.GetUtcNow())
            {
                return (attempts, endsAt);
            }
        }

        return null;
    }

    private static string WriteWindow(int attempts, DateTimeOffset endsAt)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{attempts}{Separator}{endsAt.UtcTicks}");
    }
}
