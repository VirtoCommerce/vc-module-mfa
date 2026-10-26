using Microsoft.AspNetCore.Identity;

namespace VirtoCommerce.MFA.Core.Models;

public class MfaSignInResult
{
    public bool Succeeded => Error is null;

    public IdentityError Error { get; set; }

    public int? LockoutSecondsRemaining { get; set; }
}
