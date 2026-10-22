using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.MFA.Core;

public static class ModuleConstants
{
    public static class Security
    {
        public const string SignInType = "MFA";
        public const string GrantType = "mfa";
        public const string AuthenticationMethod = "mfa";
        public const string TwoFactorTokenPurpose = "VirtoCommerce.MFA.TwoFactorToken";
        public const string IdentityTwoFactorPurpose = "TwoFactor";
        public const string CodeStampLoginProvider = "VirtoCommerce.MFA";
        public const string CodeStampName = "CodeStamp";

        public static class Parameters
        {
            public const string StoreId = "storeId";
            public const string TwoFactorToken = "two_factor_token";
            public const string Provider = "provider";
            public const string Code = "code";
        }

        public static class FailureReason
        {
            public const string MissingParameter = "MissingParameter";
            public const string InvalidTwoFactorToken = "InvalidTwoFactorToken";
            public const string LockoutDisabled = "LockoutDisabled";
            public const string ProviderNotAvailable = "ProviderNotAvailable";
            public const string InvalidCode = "InvalidCode";
            public const string CodeAttemptsExceeded = "CodeAttemptsExceeded";
            public const string FailedAttemptNotRecorded = "FailedAttemptNotRecorded";
            public const string AcceptedCodeNotRecorded = "AcceptedCodeNotRecorded";
        }
    }

    public static class StoreModes
    {
        public const string UseGlobalSetting = "Use global setting";

        public const string Enabled = "Enabled";

        public const string Disabled = "Disabled";
    }

    public static class Settings
    {
        public static class General
        {
            public static readonly SettingDescriptor MfaEnabled = new()
            {
                Name = "MultiFactorAuthentication.Enabled",
                GroupName = "Platform|Security",
                ValueType = SettingValueType.Boolean,
                DefaultValue = false,
            };

            public static readonly SettingDescriptor StoreMode = new()
            {
                Name = "MultiFactorAuthentication.StoreMode",
                GroupName = "Store|Security",
                ValueType = SettingValueType.ShortText,
                DefaultValue = StoreModes.UseGlobalSetting,
                AllowedValues = [StoreModes.UseGlobalSetting, StoreModes.Enabled, StoreModes.Disabled],
            };
        }

        public static IEnumerable<SettingDescriptor> StoreSettings
        {
            get
            {
                yield return General.StoreMode;
            }
        }

        public static IEnumerable<SettingDescriptor> AllSettings
        {
            get
            {
                yield return General.MfaEnabled;
                yield return General.StoreMode;
            }
        }
    }
}
