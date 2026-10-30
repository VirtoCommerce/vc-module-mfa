using System.Linq;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Data.Services;
using VirtoCommerce.MFA.Xapi.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;

namespace VirtoCommerce.MFA.Xapi;

/// <summary>
/// Turns the results of the MFA services into xAPI responses with the same error codes as the token endpoint.
/// </summary>
public class MfaResponseFactory(MfaErrorDescriber errorDescriber)
{
    public virtual MfaCodeChallenge ToCodeChallenge(MfaResult result)
    {
        var challenge = AbstractTypeFactory<MfaCodeChallenge>.TryCreateInstance();
        var error = errorDescriber.GetError(result, reportLockout: true);

        challenge.Succeeded = error == null;
        challenge.ErrorCode = error?.Code;
        challenge.ErrorDescription = error?.Description;
        challenge.LockoutSecondsRemaining = errorDescriber.GetLockoutSecondsRemaining(result, reportLockout: true);
        challenge.TwoFactorToken = result.TwoFactorToken;
        challenge.Provider = result.Provider;
        challenge.ResendSecondsRemaining = result.ResendSecondsRemaining;

        var provider = result.Providers?.FirstOrDefault(x => x.Name == result.Provider);
        challenge.Destination = provider?.Destination;

        return challenge;
    }

    public virtual MfaChangeResponse ToChangeResponse(MfaChangeResult result, ApplicationUser user)
    {
        var response = AbstractTypeFactory<MfaChangeResponse>.TryCreateInstance();
        response.Succeeded = result.Succeeded;
        response.Settings = result.Settings;

        if (result.Outcome == MfaChangeOutcome.CodeRefused)
        {
            var error = errorDescriber.GetError(result.CodeResult.Outcome, user);
            response.ErrorCode = error?.Code;
            response.ErrorDescription = error?.Description;
            response.LockoutSecondsRemaining = errorDescriber.GetLockoutSecondsRemaining(result.CodeResult, reportLockout: true);
        }
        else if (!result.Succeeded)
        {
            (response.ErrorCode, response.ErrorDescription) = result.Outcome switch
            {
                MfaChangeOutcome.LockedByAdministrator => ("mfa_locked_by_administrator", "Multi-factor authentication is managed by the administrator."),
                MfaChangeOutcome.OrganizationNotAllowed => ("mfa_organization_not_allowed", "You can't manage multi-factor authentication of this organization."),
                _ => ("mfa_self_service_not_allowed", "The store doesn't let customers manage multi-factor authentication."),
            };
        }

        return response;
    }
}
