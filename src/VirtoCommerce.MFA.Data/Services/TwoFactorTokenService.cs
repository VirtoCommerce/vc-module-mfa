using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.MFA.Data.Services;

public class TwoFactorTokenService(IDataProtectionProvider dataProtectionProvider) : ITwoFactorTokenService
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(ModuleConstants.Security.TwoFactorTokenPurpose);

    public virtual string CreateToken(TwoFactorTicket ticket)
    {
        return _protector.Protect(JsonSerializer.Serialize(ticket, ticket.GetType()));
    }

    public virtual TwoFactorTicket ReadToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            var ticketType = AbstractTypeFactory<TwoFactorTicket>.TryCreateInstance().GetType();

            return (TwoFactorTicket)JsonSerializer.Deserialize(_protector.Unprotect(token), ticketType);
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
