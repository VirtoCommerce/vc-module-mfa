using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
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
    INotificationSender notificationSender,
    ILogger<EmailMfaProvider> logger)
    : IMfaProvider
{
    private const int MinLengthToShowFirstCharacter = 2;
    private const int MinLengthToShowLastCharacter = 3;

    public virtual string Name => TokenOptions.DefaultEmailProvider;

    public virtual bool SendsCode => true;

    public virtual string GetDestination(ApplicationUser user)
    {
        var email = user.Email;
        var atIndex = email?.IndexOf('@') ?? -1;
        if (atIndex < 1)
        {
            return email;
        }

        var localPart = email[..atIndex];
        var domainPart = email[atIndex..];
        var firstCharacter = localPart.Length >= MinLengthToShowFirstCharacter ? localPart[..1] : string.Empty;
        var lastCharacter = localPart.Length >= MinLengthToShowLastCharacter ? localPart[^1..] : string.Empty;

        return $"{firstCharacter}•••{lastCharacter}{domainPart}";
    }

    public virtual async Task<bool> SendCodeAsync(Store store, ApplicationUser user, string code)
    {
        var tenant = store != null ? new TenantIdentity(store.Id, nameof(Store)) : null;
        var notification = await notificationSearchService.GetNotificationAsync<MfaSignInEmailNotification>(tenant);

        if (notification?.IsActive != true)
        {
            logger.LogWarning("The sign-in code was not sent because the {Notification} notification is not active (store: {StoreId}).",
                nameof(MfaSignInEmailNotification), store?.Id);
            return false;
        }

        notification.To = user.Email;
        notification.Code = code;
        notification.LanguageCode = await GetLanguageAsync(store, user);
        notification.From = GetSender(store) ?? notification.From;

        await notificationSender.ScheduleSendNotificationAsync(notification);

        return true;
    }

    protected virtual async Task<string> GetLanguageAsync(Store store, ApplicationUser user)
    {
        var member = string.IsNullOrEmpty(user.MemberId)
            ? null
            : await memberService.GetByIdAsync(user.MemberId, nameof(MemberResponseGroup.Default));

        var memberLanguage = member switch
        {
            Contact contact => contact.DefaultLanguage,
            Employee employee => employee.DefaultLanguage,
            _ => null,
        };

        return memberLanguage ?? store?.DefaultLanguage;
    }

    protected virtual string GetSender(Store store)
    {
        return (store?.AdminEmailWithName ?? store?.EmailWithName).EmptyToNull();
    }
}
