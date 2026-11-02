using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Notifications;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.MFA.Data.Providers;
using VirtoCommerce.MFA.Data.Services;
using VirtoCommerce.NotificationsModule.Core.Model;
using VirtoCommerce.NotificationsModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using MfaModuleSettings = VirtoCommerce.MFA.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.MFA.Tests;

public abstract class MfaTestsBase
{
    public const string StoreId = "test-store";
    public const string MfaEnabledStoreId = "mfa-enabled-store";
    public const string MfaDisabledStoreId = "mfa-disabled-store";
    public const string AdminManagedStoreId = "admin-managed-store";
    public const string CustomerChoiceStoreId = "customer-choice-store";
    public const string OrganizationId = "organization-1";
    public const string UnknownStoreId = "unknown-store";
    public const string ContactId = "contact-1";
    public const string EmployeeId = "employee-1";
    public const string StoreLanguage = "en-US";
    public const string ContactLanguage = "de-DE";
    public const string EmployeeLanguage = "fr-FR";
    public const string EmailProvider = "Email";

    public const string ActiveUserId = "active-user";
    public const string BackofficeUserId = "backoffice-user";
    public const string DisabledStoreCustomerId = "disabled-store-customer";
    public const string DisabledStoreAdministratorId = "disabled-store-administrator";
    public const string DisabledStoreManagerId = "disabled-store-manager";
    public const string StoreSender = "store-admin@acme.com";
    public const string EnabledStoreCustomerId = "enabled-store-customer";
    public const string LockedUserId = "locked-user";
    public const string LockoutDisabledUserId = "lockout-disabled-user";
    public const string NoEmailUserId = "no-email-user";
    public const string UnconfirmedEmailUserId = "unconfirmed-email-user";
    public const string UnknownUserId = "unknown-user";

    public const string ActiveUserEmail = "john.smith@acme.com";
    public const string MaskedActiveUserEmail = "j•••h@acme.com";

    public const string SecurityStamp = "security-stamp";
    public const string ValidCode = "246810";
    public const string InvalidCode = "000000";
    public const int LockoutSeconds = 600;

    public IMfaService MfaService { get; }

    public IMfaRequirementService RequirementService { get; }

    public InMemoryMfaModeService ModeService { get; } = new();

    public MfaErrorDescriber ErrorDescriber { get; }

    public ITwoFactorTicketStore TicketStore { get; }

    public IMfaCodeAttemptLimiter CodeAttemptLimiter { get; }

    public bool IsNotificationActive { get; set; } = true;

    public ITwoFactorTokenService TwoFactorTokenService { get; }

    public Mock<UserManager<ApplicationUser>> UserManager { get; }

    public Mock<SignInManager<ApplicationUser>> SignInManager { get; }

    public IOptions<IdentityOptions> IdentityOptions { get; } = Options.Create(new IdentityOptions());

    public ApplicationUser TwoFactorAuthenticationUser { get; set; }

    public Mock<IStoreService> StoreService { get; }

    public Store[] Stores { get; }

    public Mock<ISettingsManager> SettingsManager { get; }

    public Mock<INotificationSender> NotificationSender { get; }

    public bool IsGloballyEnabled { get; set; }

    public ApplicationUser ActiveUser { get; }

    public ApplicationUser BackofficeUser { get; }

    public ApplicationUser DisabledStoreCustomer { get; }

    public ApplicationUser LockedUser { get; }

    public ApplicationUser LockoutDisabledUser { get; }

    public ApplicationUser NoEmailUser { get; }

    public ApplicationUser UnconfirmedEmailUser { get; }

    public IdentityResult CodeStampUpdateResult { get; set; } = IdentityResult.Success;

    private readonly ConcurrentDictionary<string, string> _codeStamps = new();

    protected MfaTestsBase()
    {
        Stores =
        [
            CreateStore(StoreId, ModuleConstants.StoreModes.UseGlobalSetting),
            CreateStore(MfaEnabledStoreId, ModuleConstants.StoreModes.Enabled),
            CreateStore(MfaDisabledStoreId, ModuleConstants.StoreModes.Disabled),
            CreateStore(AdminManagedStoreId, ModuleConstants.StoreModes.AdminManaged),
            CreateStore(CustomerChoiceStoreId, ModuleConstants.StoreModes.CustomerChoice),
        ];

        StoreService = new Mock<IStoreService>();

        StoreService
            .Setup(x => x.GetAsync(It.IsAny<IList<string>>(), null, false))
            .ReturnsAsync((IList<string> ids, string _, bool _) => Stores.Where(x => ids.Contains(x.Id)).ToList());

        SettingsManager = new Mock<ISettingsManager>();

        SettingsManager
            .Setup(x => x.GetObjectSettingAsync(MfaModuleSettings.MfaEnabled.Name, null, null))
            .ReturnsAsync(() => new ObjectSettingEntry { Name = MfaModuleSettings.MfaEnabled.Name, Value = IsGloballyEnabled });

        var members = new Member[]
        {
            new Contact { Id = ContactId, DefaultLanguage = ContactLanguage, Organizations = [OrganizationId] },
            new Employee { Id = EmployeeId, DefaultLanguage = EmployeeLanguage },
        };

        var memberService = new Mock<IMemberService>();

        memberService
            .Setup(x => x.GetByIdAsync(It.IsAny<string>(), It.IsAny<string>(), null))
            .ReturnsAsync((string id, string _, string _) => members.FirstOrDefault(x => x.Id == id));

        ActiveUser = CreateUser(ActiveUserId, ActiveUserEmail);
        BackofficeUser = CreateUser(BackofficeUserId, "manager@acme.com", userType: UserType.Manager, memberId: EmployeeId);
        DisabledStoreCustomer = CreateUser(DisabledStoreCustomerId, "customer@acme.com", storeId: MfaDisabledStoreId);
        var disabledStoreAdministrator = CreateUser(DisabledStoreAdministratorId, "admin@acme.com", storeId: MfaDisabledStoreId);
        disabledStoreAdministrator.IsAdministrator = true;
        var disabledStoreManager = CreateUser(DisabledStoreManagerId, "store-manager@acme.com", userType: UserType.Manager, storeId: MfaDisabledStoreId);
        var enabledStoreCustomer = CreateUser(EnabledStoreCustomerId, "enabled@acme.com", storeId: MfaEnabledStoreId);
        LockedUser = CreateUser(LockedUserId, "locked@acme.com", lockoutEnd: TimeProvider.GetUtcNow().AddSeconds(LockoutSeconds));
        LockoutDisabledUser = CreateUser(LockoutDisabledUserId, "lockout-disabled@acme.com", lockoutEnabled: false);
        NoEmailUser = CreateUser(NoEmailUserId, email: null);
        UnconfirmedEmailUser = CreateUser(UnconfirmedEmailUserId, "unconfirmed@acme.com", emailConfirmed: false);

        var users = new[] { ActiveUser, BackofficeUser, DisabledStoreCustomer, disabledStoreAdministrator, disabledStoreManager, enabledStoreCustomer, LockedUser, LockoutDisabledUser, NoEmailUser, UnconfirmedEmailUser };

        UserManager = new Mock<UserManager<ApplicationUser>>(Mock.Of<IUserStore<ApplicationUser>>(), null, null, null, null, null, null, null, null);

        UserManager.SetupGet(x => x.SupportsUserLockout).Returns(true);

        UserManager
            .Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => users.FirstOrDefault(x => x.Id == id));

        UserManager
            .Setup(x => x.GetSecurityStampAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync((ApplicationUser user) => user.SecurityStamp);

        UserManager
            .Setup(x => x.GetLockoutEnabledAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync((ApplicationUser user) => user.LockoutEnabled);

        UserManager
            .Setup(x => x.IsLockedOutAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync((ApplicationUser user) => user.LockoutEnd > TimeProvider.GetUtcNow());

        // Like the Identity "Email" token provider: only a confirmed email can receive a code
        UserManager
            .Setup(x => x.GetValidTwoFactorProvidersAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync((ApplicationUser user) => !string.IsNullOrEmpty(user.Email) && user.EmailConfirmed ? [EmailProvider] : []);

        UserManager
            .Setup(x => x.GenerateTwoFactorTokenAsync(It.IsAny<ApplicationUser>(), EmailProvider))
            .ReturnsAsync((ApplicationUser user, string _) => CurrentCode(user));

        UserManager
            .Setup(x => x.VerifyTwoFactorTokenAsync(It.IsAny<ApplicationUser>(), EmailProvider, It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser user, string _, string code) => code == CurrentCode(user));

        UserManager
            .Setup(x => x.SetAuthenticationTokenAsync(It.IsAny<ApplicationUser>(),
                ModuleConstants.Security.CodeStampLoginProvider, ModuleConstants.Security.CodeStampName, It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser user, string _, string _, string codeStamp) => UpdateCodeStamp(user, codeStamp));

        UserManager
            .Setup(x => x.AccessFailedAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync((ApplicationUser user) => AccessFailed(user));

        UserManager
            .Setup(x => x.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        SignInManager = new Mock<SignInManager<ApplicationUser>>(
            UserManager.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            IdentityOptions,
            Mock.Of<ILogger<SignInManager<ApplicationUser>>>(),
            Mock.Of<IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<ApplicationUser>>());

        SignInManager.Object.UserManager = UserManager.Object;

        SignInManager
            .Setup(x => x.CanSignInAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(true);

        SignInManager
            .Setup(x => x.CreateUserPrincipalAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(new ClaimsPrincipal(new ClaimsIdentity()));

        SignInManager
            .Setup(x => x.GetTwoFactorAuthenticationUserAsync())
            .ReturnsAsync(() => TwoFactorAuthenticationUser);

        SignInManager
            .Setup(x => x.TwoFactorSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .Returns((string provider, string code, bool _, bool _) => TwoFactorSignInAsync(provider, code));

        var notificationSearchService = new Mock<INotificationSearchService>();

        notificationSearchService
            .Setup(x => x.SearchNotificationsAsync(It.IsAny<NotificationSearchCriteria>()))
            .ReturnsAsync((NotificationSearchCriteria criteria) => new NotificationSearchResult
            {
                Results =
                [
                    new MfaSignInEmailNotification
                    {
                        IsActive = IsNotificationActive,
                        TenantIdentity = new TenantIdentity(criteria.TenantId, criteria.TenantType),
                    },
                ],
            });

        NotificationSender = new Mock<INotificationSender>();

        TwoFactorTokenService = new TwoFactorTokenService(new EphemeralDataProtectionProvider());

        MemberService = memberService;
        var emailProvider = new EmailMfaProvider(memberService.Object, notificationSearchService.Object, NotificationSender.Object, NullLogger<EmailMfaProvider>.Instance);

        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        CodeDeliveryLimiter = new MfaCodeDeliveryLimiter(memoryCache, Options.Create(MfaOptions), TimeProvider);
        TicketStore = new TwoFactorTicketStore(memoryCache, Options.Create(MfaOptions));
        CodeAttemptLimiter = new MfaCodeAttemptLimiter(memoryCache, Options.Create(MfaOptions), TimeProvider);
        ErrorDescriber = new MfaErrorDescriber(TimeProvider);
        RequirementService = new MfaRequirementService(StoreService.Object, SettingsManager.Object, ModeService, memberService.Object);

        MfaService = new MfaService(UserManager.Object, StoreService.Object, TwoFactorTokenService, TicketStore, [emailProvider],
            CodeAttemptLimiter, CodeDeliveryLimiter, Options.Create(MfaOptions), TimeProvider);
    }

    public MfaOptions MfaOptions { get; } = new();

    public TestTimeProvider TimeProvider { get; } = new();

    public IMfaCodeDeliveryLimiter CodeDeliveryLimiter { get; }

    public Mock<IMemberService> MemberService { get; }

    public int ResendIntervalSeconds => (int)MfaOptions.CodeResendInterval.TotalSeconds;

    // Like the Identity "Email" provider with the module's modifier: the code changes with the user's code stamp
    public string CurrentCode(ApplicationUser user)
    {
        if (!_codeStamps.TryGetValue(user.Id, out var codeStamp))
        {
            return ValidCode;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(codeStamp));

        return (BitConverter.ToUInt32(hash) % 1_000_000).ToString("D6");
    }

    public string CreateTwoFactorToken(string userId = ActiveUserId, string storeId = StoreId, string securityStamp = SecurityStamp, string provider = EmailProvider)
    {
        return TwoFactorTokenService.CreateToken(new TwoFactorTicket
        {
            Id = Guid.NewGuid().ToString("N"),
            UserId = userId,
            StoreId = storeId,
            IssuedAt = TimeProvider.GetUtcNow(),
            SecurityStamp = securityStamp,
            Provider = provider,
        });
    }

    private static Store CreateStore(string id, string mfaMode)
    {
        return new Store
        {
            Id = id,
            Name = id,
            DefaultLanguage = StoreLanguage,
            AdminEmail = StoreSender,
            Settings = [new() { Name = MfaModuleSettings.StoreMode.Name, Value = mfaMode }],
        };
    }

    private static ApplicationUser CreateUser(
        string id,
        string email,
        DateTimeOffset? lockoutEnd = null,
        bool lockoutEnabled = true,
        bool emailConfirmed = true,
        UserType userType = UserType.Customer,
        string memberId = ContactId,
        string storeId = StoreId)
    {
        return new ApplicationUser
        {
            Id = id,
            UserName = id,
            Email = email,
            EmailConfirmed = emailConfirmed,
            MemberId = memberId,
            StoreId = storeId,
            UserType = userType.ToString(),
            SecurityStamp = SecurityStamp,
            LockoutEnd = lockoutEnd,
            LockoutEnabled = lockoutEnabled,
        };
    }

    private IdentityResult UpdateCodeStamp(ApplicationUser user, string codeStamp)
    {
        if (CodeStampUpdateResult.Succeeded)
        {
            _codeStamps[user.Id] = codeStamp;
        }

        return CodeStampUpdateResult;
    }

    // Like Identity: a wrong code counts toward the lockout, which starts at MaxFailedAccessAttempts
    private IdentityResult AccessFailed(ApplicationUser user)
    {
        user.AccessFailedCount++;

        if (user.AccessFailedCount >= IdentityOptions.Value.Lockout.MaxFailedAccessAttempts)
        {
            user.LockoutEnd = TimeProvider.GetUtcNow() + IdentityOptions.Value.Lockout.DefaultLockoutTimeSpan;
            user.AccessFailedCount = 0;
        }

        return IdentityResult.Success;
    }

    // Like Identity: the code of the user waiting for the second factor, refused while the account is locked
    private async Task<SignInResult> TwoFactorSignInAsync(string provider, string code)
    {
        var user = TwoFactorAuthenticationUser;
        if (user == null)
        {
            return SignInResult.Failed;
        }

        if (await UserManager.Object.IsLockedOutAsync(user))
        {
            return SignInResult.LockedOut;
        }

        if (await UserManager.Object.VerifyTwoFactorTokenAsync(user, provider, code))
        {
            user.AccessFailedCount = 0;
            return SignInResult.Success;
        }

        await UserManager.Object.AccessFailedAsync(user);

        return await UserManager.Object.IsLockedOutAsync(user) ? SignInResult.LockedOut : SignInResult.Failed;
    }
}
