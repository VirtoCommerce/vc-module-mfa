using VirtoCommerce.NotificationsModule.Core.Model;

namespace VirtoCommerce.MFA.Core.Notifications;

public class MfaSignInEmailNotification : EmailNotification
{
    public MfaSignInEmailNotification()
        : base(nameof(MfaSignInEmailNotification))
    {
    }

    public string Code { get; set; }
}
