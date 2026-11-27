using System;
using System.IO;
using System.Runtime.CompilerServices;
using GraphQL.MicrosoftDI;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Notifications;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.MFA.Data.OpenIddict;
using VirtoCommerce.MFA.Data.Providers;
using VirtoCommerce.MFA.Data.Services;
using VirtoCommerce.MFA.Xapi;
using VirtoCommerce.MFA.Xapi.Authorization;
using VirtoCommerce.NotificationsModule.Core.Services;
using VirtoCommerce.NotificationsModule.TemplateLoader.FileSystem;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Platform.Security.OpenIddict;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.MFA.Web;

public class Module : IModule, IHasConfiguration, IHasModuleService
{
    // Optional: without it the module protects sign-ins, but the storefront can't manage MFA
    private const string XapiModuleId = "VirtoCommerce.Xapi";

    public ManifestModuleInfo ModuleInfo { get; set; }
    public IConfiguration Configuration { get; set; }
    public IModuleService ModuleService { get; set; }

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

        serviceCollection.AddTransient<MfaEmailTokenProvider>();
        serviceCollection.Configure<IdentityOptions>(options =>
            options.Tokens.ProviderMap[TokenOptions.DefaultEmailProvider] = new TokenProviderDescriptor(typeof(MfaEmailTokenProvider)));

        serviceCollection.TryAddSingleton(TimeProvider.System);
        serviceCollection.AddSingleton<IMfaCodeAttemptLimiter, MfaCodeAttemptLimiter>();
        serviceCollection.AddSingleton<IMfaCodeDeliveryLimiter, MfaCodeDeliveryLimiter>();
        serviceCollection.AddSingleton<ITwoFactorTicketStore, TwoFactorTicketStore>();
        serviceCollection.AddSingleton<MfaErrorDescriber>();

        serviceCollection.AddTransient<IMfaProvider, EmailMfaProvider>();
        serviceCollection.AddTransient<ITwoFactorTokenService, TwoFactorTokenService>();
        serviceCollection.AddTransient<IMfaRequirementService, MfaRequirementService>();
        serviceCollection.AddTransient<IMfaModeService, MfaModeService>();
        serviceCollection.AddTransient<IMfaSelfService, MfaSelfService>();
        serviceCollection.AddTransient<IMfaStatusService, MfaStatusService>();
        serviceCollection.AddTransient<IMfaService, MfaService>();
        serviceCollection.AddTransient<IBackofficeMfaSignInService, BackofficeMfaSignInService>();

        serviceCollection.AddTransient<ITwoFactorPolicy, MfaTwoFactorPolicy>();
        serviceCollection.AddTransient<ITwoFactorSignInHandler, MfaTwoFactorSignInHandler>();
        serviceCollection.AddGrantTypeHandler<MfaGrantTypeHandler>(ModuleConstants.Security.GrantType);
        serviceCollection.AddTransient<ITokenClaimProvider, MfaAuthenticationMethodClaimProvider>();

        if (ModuleService.IsInstalled(XapiModuleId))
        {
            AddXapi(serviceCollection);
        }
    }

    public void PostInitialize(IApplicationBuilder appBuilder)
    {
        var serviceProvider = appBuilder.ApplicationServices;

        var settingsRegistrar = serviceProvider.GetRequiredService<ISettingsRegistrar>();
        settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);
        settingsRegistrar.RegisterSettingsForType(ModuleConstants.Settings.StoreSettings, nameof(Store));

        var permissionsRegistrar = serviceProvider.GetRequiredService<IPermissionsRegistrar>();
        permissionsRegistrar.RegisterPermissions(ModuleInfo.Id, "MFA", ModuleConstants.Security.Permissions.AllPermissions);
        permissionsRegistrar.RegisterPermissions(ModuleInfo.Id, "XAPI", ModuleConstants.Security.FrontendPermissions.AllPermissions);

        if (ModuleService.IsInstalled(XapiModuleId))
        {
            UseXapi(appBuilder);
        }

        var notificationRegistrar = serviceProvider.GetRequiredService<INotificationRegistrar>();
        notificationRegistrar.RegisterNotification<MfaSignInEmailNotification>()
            .WithTemplatesFromPath(Path.Combine(ModuleInfo.FullPhysicalPath, "NotificationTemplates"));

        if (serviceProvider.GetRequiredService<IOptions<MfaOptions>>().Value.DisableEnforcement)
        {
            serviceProvider.GetRequiredService<ILogger<Module>>().LogWarning(
                "Multi-factor authentication is not enforced: MultiFactorAuthentication:DisableEnforcement is on. Turn it off once access is restored.");
        }
    }

    // Kept out of Initialize and PostInitialize, so the GraphQL types load only when the xAPI module is installed
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AddXapi(IServiceCollection serviceCollection)
    {
        // Register GraphQL schema
        _ = new GraphQLBuilder(serviceCollection, builder =>
        {
            builder.AddSchema(serviceCollection, typeof(XapiAssemblyMarker));
        });

        serviceCollection.AddSingleton<ScopedSchemaFactory<XapiAssemblyMarker>>();
        serviceCollection.AddTransient<MfaUserResolver>();
        serviceCollection.AddTransient<MfaResponseFactory>();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void UseXapi(IApplicationBuilder appBuilder)
    {
        // Register partial GraphQL schema
        appBuilder.UseScopedSchema<XapiAssemblyMarker>("mfa");
    }

    public void Uninstall()
    {
        // Nothing to do here
    }
}
