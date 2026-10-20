using System.Threading.Tasks;

namespace VirtoCommerce.MFA.Core.Services;

public interface IMfaUsedCodeStore
{
    Task<bool> TryUseAsync(string userId, string provider, string code);

    Task ReleaseAsync(string userId, string provider, string code);
}
