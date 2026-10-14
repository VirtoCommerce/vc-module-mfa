using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.MFA.Core.Models;

public class MfaResult
{
    public MfaOutcome Outcome { get; set; }

    public ApplicationUser User { get; set; }

    public string TwoFactorToken { get; set; }

    public string Provider { get; set; }

    public IList<MfaProviderInfo> Providers { get; set; }

    public int? ResendSecondsRemaining { get; set; }

    public static MfaResult Succeed(ApplicationUser user, string provider = null, IList<MfaProviderInfo> providers = null)
    {
        var result = AbstractTypeFactory<MfaResult>.TryCreateInstance();
        result.Outcome = MfaOutcome.Success;
        result.User = user;
        result.Provider = provider;
        result.Providers = providers;

        return result;
    }

    public static MfaResult Fail(MfaOutcome outcome, ApplicationUser user = null, string provider = null)
    {
        var result = AbstractTypeFactory<MfaResult>.TryCreateInstance();
        result.Outcome = outcome;
        result.User = user;
        result.Provider = provider;

        return result;
    }
}
