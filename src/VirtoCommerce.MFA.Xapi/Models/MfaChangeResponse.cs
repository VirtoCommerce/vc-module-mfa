using VirtoCommerce.MFA.Core.Models;

namespace VirtoCommerce.MFA.Xapi.Models;

public class MfaChangeResponse
{
    public bool Succeeded { get; set; }

    public string ErrorCode { get; set; }

    public string ErrorDescription { get; set; }

    public int? LockoutSecondsRemaining { get; set; }

    public MfaSettings Settings { get; set; }
}
