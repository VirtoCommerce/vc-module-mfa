using VirtoCommerce.MFA.Core.Models;

namespace VirtoCommerce.MFA.Core.Services;

public interface ITwoFactorTicketStore
{
    bool HasAttemptsLeft(TwoFactorTicket ticket);

    bool IsRedeemed(TwoFactorTicket ticket);

    bool TryConsumeAttempt(TwoFactorTicket ticket);

    bool TryRedeem(TwoFactorTicket ticket);

    void Revoke(TwoFactorTicket ticket);
}
