using System.Collections.Generic;

namespace VirtoCommerce.MFA.Core.Models;

public class MfaSendCodeResult : MfaSignInResult
{
    public string TwoFactorToken { get; set; }

    public string Provider { get; set; }

    public IList<MfaProviderInfo> Providers { get; set; }

    public int? ResendSecondsRemaining { get; set; }
}
