using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.Platform.Security.Repositories;

namespace VirtoCommerce.MFA.Data.Services;

/// <summary>
/// Keeps the wrong code count of each user in the Identity user tokens (<c>AspNetUserTokens</c>), so every platform
/// instance shares it. The count is read from the database on each call, never from a cache, and is changed only if it
/// still has the value that was read (compare-and-swap), so parallel attempts on any instances can't exceed the limit.
/// </summary>
public class MfaUserTokenCodeAttemptLimiter(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<MfaOptions> options,
    TimeProvider timeProvider)
    : IMfaCodeAttemptLimiter
{
    // Each failed swap means another request changed the count, which happens at most MaxCodeAttempts times per period
    private const int MaxSwapAttempts = 10;
    private const char Separator = '|';

    public virtual async Task<bool> HasAttemptsLeftAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();

        var token = await GetTokenQuery(dbContext, userId).AsNoTracking().FirstOrDefaultAsync();
        var window = ReadWindow(token?.Value);

        return window == null || window.Value.Attempts < options.Value.MaxCodeAttempts;
    }

    public virtual async Task<bool> TryConsumeAttemptAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();

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

        // Fail closed: the attempt is not checked when it can't be counted
        return false;
    }

    public virtual async Task ResetAsync(string userId)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();

        await GetTokenQuery(dbContext, userId).ExecuteDeleteAsync();
    }

    protected virtual IQueryable<IdentityUserToken<string>> GetTokenQuery(SecurityDbContext dbContext, string userId)
    {
        return dbContext.UserTokens.Where(x =>
            x.UserId == userId &&
            x.LoginProvider == ModuleConstants.Security.CodeStampLoginProvider &&
            x.Name == ModuleConstants.Security.CodeAttemptsName);
    }

    protected virtual async Task<bool> TryAddTokenAsync(SecurityDbContext dbContext, string userId, string value)
    {
        dbContext.UserTokens.Add(new IdentityUserToken<string>
        {
            UserId = userId,
            LoginProvider = ModuleConstants.Security.CodeStampLoginProvider,
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
            // Another request added the token first
            dbContext.ChangeTracker.Clear();
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

    private (int Attempts, DateTimeOffset EndsAt)? ReadWindow(string value)
    {
        var parts = value?.Split(Separator);

        if (parts?.Length == 2 &&
            int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var attempts) &&
            long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var endsAtTicks) &&
            endsAtTicks <= DateTimeOffset.MaxValue.UtcTicks)
        {
            var endsAt = new DateTimeOffset(endsAtTicks, TimeSpan.Zero);
            if (endsAt > timeProvider.GetUtcNow())
            {
                return (attempts, endsAt);
            }
        }

        // No count, an expired period or an unreadable value: a new period starts
        return null;
    }

    private static string WriteWindow(int attempts, DateTimeOffset endsAt)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{attempts}{Separator}{endsAt.UtcTicks}");
    }
}
