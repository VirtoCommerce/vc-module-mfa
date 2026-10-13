using System.Collections.Generic;
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

    public static MfaResult Succeed(ApplicationUser user, string twoFactorToken = null, string provider = null, IList<MfaProviderInfo> providers = null)
    {
        return new MfaResult
        {
            Outcome = MfaOutcome.Success,
            User = user,
            TwoFactorToken = twoFactorToken,
            Provider = provider,
            Providers = providers,
        };
    }

    public static MfaResult Fail(MfaOutcome outcome, ApplicationUser user = null)
    {
        return new MfaResult
        {
            Outcome = outcome,
            User = user,
        };
    }
}
