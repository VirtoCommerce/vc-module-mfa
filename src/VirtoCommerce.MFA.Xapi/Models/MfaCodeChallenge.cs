namespace VirtoCommerce.MFA.Xapi.Models;

/// <summary>
/// The answer to a request for a code that confirms a change of the MFA mode.
/// </summary>
public class MfaCodeChallenge
{
    public bool Succeeded { get; set; }

    public string TwoFactorToken { get; set; }

    public string Provider { get; set; }

    public string Destination { get; set; }

    public int? ResendSecondsRemaining { get; set; }

    public int? LockoutSecondsRemaining { get; set; }

    public string ErrorCode { get; set; }

    public string ErrorDescription { get; set; }
}
