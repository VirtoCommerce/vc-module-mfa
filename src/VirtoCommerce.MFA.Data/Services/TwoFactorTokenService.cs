using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;

namespace VirtoCommerce.MFA.Data.Services;

public class TwoFactorTokenService(IDataProtectionProvider dataProtectionProvider) : ITwoFactorTokenService
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(ModuleConstants.Security.TwoFactorTokenPurpose);

    public virtual string CreateToken(TwoFactorTicket ticket)
    {
        return _protector.Protect(JsonSerializer.Serialize(ticket));
    }

    public virtual TwoFactorTicket ReadToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TwoFactorTicket>(_protector.Unprotect(token));
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
