using System.ComponentModel.DataAnnotations;

namespace VirtoCommerce.MFA.Core.Models;

public class MfaResendCodeRequest
{
    [Required]
    public string TwoFactorToken { get; set; }

    public string Provider { get; set; }
}
