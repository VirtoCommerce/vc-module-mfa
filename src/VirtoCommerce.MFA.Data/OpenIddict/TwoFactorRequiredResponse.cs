using System.Collections.Generic;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.Platform.Security.OpenIddict;

namespace VirtoCommerce.MFA.Data.OpenIddict;

public class TwoFactorRequiredResponse : TokenResponse
{
    public string TwoFactorToken { get; set; }

    public string Provider { get; set; }

    public IList<MfaProviderInfo> Providers { get; set; }

    public int? ResendSecondsRemaining { get; set; }
}
