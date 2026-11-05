namespace VirtoCommerce.MFA.Core.Models;

public class MfaChangeResult
{
    public MfaChangeOutcome Outcome { get; set; }

    /// <summary>
    /// The result of the code check when the code was refused.
    /// </summary>
    public MfaResult CodeResult { get; set; }

    public MfaSettings Settings { get; set; }

    public bool Succeeded => Outcome == MfaChangeOutcome.Success;
}
