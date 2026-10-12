using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;

namespace VirtoCommerce.MFA.Data.Services;

public class TwoFactorTokenService(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<MfaOptions> options)
    : ITwoFactorTokenService
{
    private readonly ITimeLimitedDataProtector _protector = dataProtectionProvider
        .CreateProtector(ModuleConstants.Security.TwoFactorTokenPurpose)
        .ToTimeLimitedDataProtector();

    public virtual string CreateToken(TwoFactorTicket ticket)
    {
        var payload = JsonSerializer.Serialize(ticket);

        return _protector.Protect(payload, ticket.IssuedAt + options.Value.TwoFactorTokenLifetime);
    }

    public virtual TwoFactorTicket ReadToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            var payload = _protector.Unprotect(token);

            return JsonSerializer.Deserialize<TwoFactorTicket>(payload);
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
