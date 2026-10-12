using System;

namespace VirtoCommerce.MFA.Core.Models;

public class MfaOptions
{
    public TimeSpan TwoFactorTokenLifetime { get; set; } = TimeSpan.FromMinutes(10);
}
