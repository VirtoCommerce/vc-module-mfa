namespace VirtoCommerce.MFA.Core.Models;

public enum MfaOutcome
{
    Undefined,
    Success,
    InvalidTwoFactorToken,
    LockoutDisabled,
    AccountLocked,
    ProviderNotAvailable,
    InvalidCode,
    SignInNotAllowed,
    DuplicateEmail,
}
