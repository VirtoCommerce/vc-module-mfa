using VirtoCommerce.MFA.Core.Models;

namespace VirtoCommerce.MFA.Core.Services;

public interface ITwoFactorTokenService
{
    string CreateToken(TwoFactorTicket ticket);

    TwoFactorTicket ReadToken(string token);
}
