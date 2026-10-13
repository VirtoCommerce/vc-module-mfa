using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace VirtoCommerce.MFA.Core.Models;

public class MfaSendCodeResult
{
    public bool Succeeded => Error is null;

    public IdentityError Error { get; set; }

    public string TwoFactorToken { get; set; }

    public string Provider { get; set; }

    public IList<MfaProviderInfo> Providers { get; set; }

    public int? LockoutSecondsRemaining { get; set; }

    public int? ResendSecondsRemaining { get; set; }
}
