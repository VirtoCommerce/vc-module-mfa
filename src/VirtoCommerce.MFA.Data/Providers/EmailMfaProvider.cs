using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.MFA.Core.Notifications;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.NotificationsModule.Core.Extensions;
using VirtoCommerce.NotificationsModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.StoreModule.Core.Model;

namespace VirtoCommerce.MFA.Data.Providers;

public class EmailMfaProvider(
    IMemberService memberService,
    INotificationSearchService notificationSearchService,
    INotificationSender notificationSender)
    : IMfaProvider
{
    public virtual string Name => TokenOptions.DefaultEmailProvider;

    public virtual string GetDestination(ApplicationUser user)
    {
        var email = user.Email;
        var atIndex = email.IndexOf('@');
        if (atIndex <= 1)
        {
            return email;
        }

        var localPart = email[..atIndex];
        var domainPart = email[atIndex..];

        return $"{localPart[0]}•••{localPart[^1]}{domainPart}";
    }

    public virtual async Task SendCodeAsync(Store store, ApplicationUser user, string code)
    {
        var tenant = store != null ? new TenantIdentity(store.Id, nameof(Store)) : null;
        var notification = await notificationSearchService.GetNotificationAsync<MfaSignInEmailNotification>(tenant);
        var member = string.IsNullOrEmpty(user.MemberId) ? null : await memberService.GetByIdAsync(user.MemberId);

        var memberLanguage = member switch
        {
            Contact contact => contact.DefaultLanguage,
            Employee employee => employee.DefaultLanguage,
            _ => null,
        };

        notification.To = user.Email;
        notification.Code = code;
        notification.LanguageCode = memberLanguage ?? store?.DefaultLanguage;

        await notificationSender.ScheduleSendNotificationAsync(notification);
    }
}
