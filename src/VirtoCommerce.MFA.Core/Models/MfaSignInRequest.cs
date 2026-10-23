using System.ComponentModel.DataAnnotations;

namespace VirtoCommerce.MFA.Core.Models;

public class MfaSignInRequest
{
    [Required]
    public string Code { get; set; }

    public string Provider { get; set; }

    public bool RememberMe { get; set; }
}
