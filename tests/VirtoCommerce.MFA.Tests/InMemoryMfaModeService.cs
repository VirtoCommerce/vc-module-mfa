using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Services;

namespace VirtoCommerce.MFA.Tests;

public class InMemoryMfaModeService : IMfaModeService
{
    private readonly ConcurrentDictionary<string, string> _userModes = new();
    private readonly ConcurrentDictionary<string, string> _organizationModes = new();

    public Task<string> GetUserModeAsync(string userId)
    {
        return Task.FromResult(_userModes.GetValueOrDefault(userId ?? string.Empty, ModuleConstants.Modes.Off));
    }

    public Task SetUserModeAsync(string userId, string mode)
    {
        _userModes[userId] = mode;
        return Task.CompletedTask;
    }

    public Task<string> GetOrganizationModeAsync(string organizationId)
    {
        return Task.FromResult(_organizationModes.GetValueOrDefault(organizationId ?? string.Empty, ModuleConstants.Modes.Off));
    }

    public async Task<IDictionary<string, string>> GetOrganizationModesAsync(IList<string> organizationIds)
    {
        var result = new Dictionary<string, string>();

        foreach (var organizationId in organizationIds.Distinct())
        {
            result[organizationId] = await GetOrganizationModeAsync(organizationId);
        }

        return result;
    }

    public Task SetOrganizationModeAsync(string organizationId, string mode)
    {
        _organizationModes[organizationId] = mode;
        return Task.CompletedTask;
    }
}
