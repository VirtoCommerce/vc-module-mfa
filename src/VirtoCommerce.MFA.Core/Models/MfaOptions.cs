using System;

namespace VirtoCommerce.MFA.Core.Models;

public class MfaOptions
{
    public TimeSpan TwoFactorTokenLifetime { get; set; } = TimeSpan.FromMinutes(10);

    public TimeSpan CodeResendInterval { get; set; } = TimeSpan.FromSeconds(30);

    public int MaxCodeAttempts { get; set; } = 3;

    public TimeSpan UsedCodeLifetime { get; set; } = TimeSpan.FromMinutes(10);

    public bool DisableEnforcement { get; set; }
}
