using System;
using System.IO;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Notifications;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.MFA.Data.OpenIddict;
using VirtoCommerce.MFA.Data.Providers;
using VirtoCommerce.MFA.Data.Services;
using VirtoCommerce.NotificationsModule.Core.Services;
using VirtoCommerce.NotificationsModule.TemplateLoader.FileSystem;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Platform.Security.OpenIddict;
using VirtoCommerce.StoreModule.Core.Model;

namespace VirtoCommerce.MFA.Web;

public class Module : IModule, IHasConfiguration
{
    public ManifestModuleInfo ModuleInfo { get; set; }
    public IConfiguration Configuration { get; set; }

    public void Initialize(IServiceCollection serviceCollection)
    {
        serviceCollection.AddOptions<MfaOptions>()
            .Bind(Configuration.GetSection("MultiFactorAuthentication"))
            .Validate(x => x.TwoFactorTokenLifetime > TimeSpan.Zero, "MultiFactorAuthentication:TwoFactorTokenLifetime must be positive.")
            .Validate(x => x.CodeResendInterval > TimeSpan.Zero && x.CodeResendInterval < x.TwoFactorTokenLifetime, "MultiFactorAuthentication:CodeResendInterval must be positive and shorter than TwoFactorTokenLifetime.")
            .Validate(x => x.MaxCodeAttempts > 0, "MultiFactorAuthentication:MaxCodeAttempts must be positive.")
            .ValidateOnStart();

        serviceCollection.AddOptions<CookieAuthenticationOptions>(IdentityConstants.TwoFactorUserIdScheme)
            .Configure<IOptions<MfaOptions>>((cookieOptions, mfaOptions) =>
            {
                cookieOptions.ExpireTimeSpan = mfaOptions.Value.TwoFactorTokenLifetime;
                cookieOptions.SlidingExpiration = false;
            });

        serviceCollection.TryAddSingleton(TimeProvider.System);
        serviceCollection.AddSingleton<IMfaCodeDeliveryLimiter, MfaCodeDeliveryLimiter>();
        serviceCollection.AddSingleton<ITwoFactorTicketStore, TwoFactorTicketStore>();
        serviceCollection.AddSingleton<MfaErrorDescriber>();

        serviceCollection.AddTransient<IMfaProvider, EmailMfaProvider>();
        serviceCollection.AddTransient<ITwoFactorTokenService, TwoFactorTokenService>();
        serviceCollection.AddTransient<IMfaRequirementService, MfaRequirementService>();
        serviceCollection.AddTransient<IMfaService, MfaService>();
        serviceCollection.AddTransient<IBackofficeMfaSignInService, BackofficeMfaSignInService>();

        serviceCollection.AddTransient<ITwoFactorPolicy, MfaTwoFactorPolicy>();
        serviceCollection.AddTransient<ITwoFactorSignInHandler, MfaTwoFactorSignInHandler>();
        serviceCollection.AddGrantTypeHandler<MfaGrantTypeHandler>(ModuleConstants.Security.GrantType);
        serviceCollection.AddTransient<ITokenClaimProvider, MfaAuthenticationMethodClaimProvider>();
    }

    public void PostInitialize(IApplicationBuilder appBuilder)
    {
        var serviceProvider = appBuilder.ApplicationServices;

        var settingsRegistrar = serviceProvider.GetRequiredService<ISettingsRegistrar>();
        settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);
        settingsRegistrar.RegisterSettingsForType(ModuleConstants.Settings.StoreSettings, nameof(Store));

        var notificationRegistrar = serviceProvider.GetRequiredService<INotificationRegistrar>();
        notificationRegistrar.RegisterNotification<MfaSignInEmailNotification>()
            .WithTemplatesFromPath(Path.Combine(ModuleInfo.FullPhysicalPath, "NotificationTemplates"));
    }

    public void Uninstall()
    {
        // Nothing to do here
    }
}
