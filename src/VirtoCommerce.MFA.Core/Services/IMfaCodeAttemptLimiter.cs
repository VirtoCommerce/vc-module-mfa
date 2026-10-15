using System.Threading.Tasks;

namespace VirtoCommerce.MFA.Core.Services;

public interface IMfaCodeAttemptLimiter
{
    Task<bool> HasAttemptsLeftAsync(string userId);

    Task<bool> TryConsumeAttemptAsync(string userId);

    Task ResetAsync(string userId);
}
