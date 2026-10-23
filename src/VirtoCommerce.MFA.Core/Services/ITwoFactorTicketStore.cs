using System.Threading.Tasks;
using VirtoCommerce.MFA.Core.Models;

namespace VirtoCommerce.MFA.Core.Services;

public interface ITwoFactorTicketStore
{
    Task<bool> IsRedeemedAsync(TwoFactorTicket ticket);

    Task<bool> TryRedeemAsync(TwoFactorTicket ticket);

    Task RevokeAsync(TwoFactorTicket ticket);
}
