# Multi-Factor Authentication Module

## Overview

The module adds multi-factor authentication (MFA) to password sign-ins: after the password, the user enters a one-time code sent by email. It covers the Backoffice and any other password sign-in without a store, and the storefront password sign-in of each store.

A store can require the code from everyone, from no one, or leave it to the users and organizations: an administrator sets their mode in the Backoffice, and in a store with `CustomerChoice` customers and organization maintainers turn it on and off themselves in the storefront.

The module is built on the ASP.NET Core Identity two-factor mechanism. It tells Identity which sign-ins require a second factor through the platform `ITwoFactorPolicy`, which the platform `SignInManager.IsTwoFactorEnabledAsync` consults for every password sign-in. The policy takes the `storeId` of the token request, if there is one, and requires the second factor according to the platform and store settings. Identity then answers `RequiresTwoFactor` to password sign-ins and keeps the failed access count until the second factor is passed.

## Configuration

| Setting | Level and place in the Backoffice | Description | Default |
| --- | --- | --- | --- |
| `MultiFactorAuthentication.Enabled` | Global: Settings > Platform > Security | Requires the code for every Backoffice sign-in, and for every password sign-in to the token endpoint that names no store, an unknown store or a store that follows the global setting. | `false` |
| `MultiFactorAuthentication.StoreMode` | Store: Stores > store > Settings > Store > Security | Applies to the password sign-ins to the token endpoint that name this store. `Use global setting`: follow `MultiFactorAuthentication.Enabled`. `Enabled`: the code is required from everyone. `Disabled`: the code is not required. `AdminManaged`: required from the users and organizations an administrator set to `Required`. `CustomerChoice`: required from the users and organizations with the mode `Enabled` or `Required`; customers change their own mode, and organization maintainers the mode of their organization, in the storefront. | `Use global setting` |

The store setting is taken from the `storeId` of the token request. A token request without a store, such as from vc-shell apps and API clients, follows the global setting, whatever the user's own store says. Tokens are not limited to the store the request names: a user of a store that requires MFA who signs in without a store, or names a store that doesn't require it, gets tokens without the code.

The Backoffice sign-in has no token request and follows only the global setting, for every user. Basic authentication follows the same rule.

| appsettings.json | Description | Default |
| --- | --- | --- |
| `MultiFactorAuthentication:TwoFactorTokenLifetime` | How long the second step stays open after the password: the two-factor token of the token endpoint and the two-factor cookie of the Backoffice. | `00:10:00` |
| `MultiFactorAuthentication:CodeResendInterval` | How often a code can be sent to a user by the same provider. Must be shorter than `TwoFactorTokenLifetime`. | `00:00:30` |
| `MultiFactorAuthentication:MaxCodeAttempts` | How many wrong codes a user can enter within `TwoFactorTokenLifetime`, at the token endpoint and on the Backoffice code screen together. | `3` |
| `MultiFactorAuthentication:DisableEnforcement` | Stops requiring the code whatever the settings say. A recovery switch for an administrator who can't pass the second factor; see below. | `false` |

Codes are sent with the ASP.NET Core Identity `Email` two-factor provider, which requires a confirmed email. **Before enabling MFA, confirm the email of the users it applies to and enable lockout for them** (the account blade in the Backoffice has "Email confirmed" and "Lockout enabled" switches); a user without a confirmed email or with lockout disabled cannot complete the sign-in.

If an administrator can no longer sign in because of MFA, set `MultiFactorAuthentication:DisableEnforcement` to `true` in the configuration (for example, the `MultiFactorAuthentication__DisableEnforcement` environment variable), restart the platform, fix the account or the settings, and remove the switch. While the switch is on, the platform logs a warning at startup.

The storefront calls `POST /api/mfa/resend` on its own host. **When the storefront host forwards only some paths to the platform** (for example, an ingress with a list of routes), add `/api/mfa` next to `/api/otp`, `/connect/token` and `/graphql`; otherwise "Resend code" fails with `405`.

### User and organization modes

Every user and every organization has an MFA mode, kept in the platform object settings `MultiFactorAuthentication.UserMode` and `MultiFactorAuthentication.OrganizationMode`. Only the stores with `AdminManaged` or `CustomerChoice` read them; the other store modes, the global setting and the Backoffice sign-in ignore them.

| Mode | Set by | `AdminManaged` store | `CustomerChoice` store |
| --- | --- | --- | --- |
| `Off` (default) | Administrator, or the customer / organization maintainer in the storefront | Not required | Not required |
| `Enabled` | Administrator, or the customer / organization maintainer in the storefront | Not required | Required; the storefront can turn it off |
| `Required` | Administrator only | Required | Required; the storefront can't turn it off |

A user needs the code when their own mode or the mode of any of their contact's organizations requires it. The organization comes first: while the organization requires the code, the user's own mode changes nothing, and the storefront shows no switch for it.

In the Backoffice, the "Multi-factor authentication" widget of the user account and of the organization opens the mode blade. The blade lists every store with its MFA setting, whether the sign-in needs the code there and why; for a Backoffice user it also shows the Backoffice sign-in. The list follows the selected mode before it is saved.

### Permissions

| Permission | Allows |
| --- | --- |
| `mfa:read` | See the MFA mode and the status of users and organizations: the Backoffice widget and blade, and the `GET` endpoints below. |
| `mfa:manage` | Change the MFA mode of users and organizations, including `Required`. Give it together with `mfa:read`. |
| `xapi:my_organization:mfa:manage` | Storefront permission: change the MFA mode of the user's own organization in a `CustomerChoice` store. Add it to an organization role, for example "Organization maintainer". |

### Dependencies

The xAPI module (`VirtoCommerce.Xapi`) is an optional dependency. Without it the module protects every password sign-in as described here and the Backoffice manages the user and organization modes, but the storefront can't show or change them: the GraphQL schema below is registered only when xAPI is installed. The package includes `VirtoCommerce.Xapi.Core.dll` (listed in `module.keep`), so the module loads without xAPI.

## How it works

### Backoffice

1. The user signs in with the password on the Backoffice login page. When MFA is required, the platform sign-in answers `requiresTwoFactor` and Identity keeps the user in its two-factor cookie.
2. The login page goes to the code screen of the module, which sends the code with `POST /api/mfa/signin/send`.
3. The user enters the code, and `POST /api/mfa/signin/verify` (`code`, `rememberMe`, optional `provider`) completes the sign-in with `SignInManager.TwoFactorSignInAsync`. A wrong code counts toward the wrong code limit and the lockout. A correct code resets the failed attempt count and signs the user in.

### Token endpoint (storefront, apps)

1. The client signs in with the password grant: `POST /connect/token` (`grant_type=password`, `username`, `password`, optional `storeId`, optional `provider`). When MFA is required and the password is correct, the platform does not issue tokens. The module sends a code and the response is `400` with the `two_factor_required` error code:

    ```json
    {
      "error": "invalid_grant",
      "errors": [{ "code": "two_factor_required", "description": "..." }],
      "twoFactorToken": "CfDJ8...",
      "provider": "Email",
      "providers": [{ "name": "Email", "destination": "j•••h@acme.com" }],
      "resendSecondsRemaining": 30
    }
    ```

2. The client exchanges the code for tokens: `POST /connect/token` (`grant_type=mfa`, `two_factor_token`, `code`, and `storeId` when the first step named a store).
3. To send the code again, the client calls `POST /api/mfa/resend` (`twoFactorToken`, optional `provider`). The response has a new `twoFactorToken` to use from then on, which expires at the same time as the first one, and the `provider` the code was sent with. Within the time step of the Identity provider, the same code is sent again.

The two-factor token of a store sign-in can only be redeemed with the same `storeId`, and a token issued to confirm a mode change (see below) can't be redeemed for a sign-in.

`resendSecondsRemaining` tells the client how many seconds remain before it can request a new code. It comes with every response that sends a code and with the `resend_too_soon` error.

The two-factor token proves that the password was verified. It is an encrypted ticket with its id, the user, the store, the user's security stamp and the provider the current code was sent with, valid for `MultiFactorAuthentication:TwoFactorTokenLifetime` (10 minutes by default) from the password step. `provider` names the provider the code was sent with, and `providers` lists every provider available to the user with its masked destination. The tokens issued by the `mfa` grant, and the tokens refreshed from them, have the `amr` claim `mfa`. Codes are generated and verified by the Identity two-factor provider (`GenerateTwoFactorTokenAsync`, `VerifyTwoFactorTokenAsync`), which also defines their length and lifetime. The Backoffice code screen takes the six digits of the `Email` provider. The module adds no tables: the user and organization modes are platform object settings, and the wrong code limit, the redeemed two-factor tokens and the resend limit are kept in the memory of the platform instance.

### Storefront (xAPI)

With the xAPI module installed, the storefront reads and changes the modes over GraphQL. Every change is confirmed with a code, so a stolen session can't turn MFA off:

* `mfaSettings(storeId, organizationId)` returns the store mode, whether the current user's sign-in needs the code and what requires it (`requiredBy`: `None`, `Global`, `Store`, `Organization` or `User`), the user's and the organization's modes, whether the user may change them, and the masked destination of the code.
* `requestMfaChangeCode(command: { storeId })` sends a code and returns a two-factor token for the change.
* `updateMfaUserMode(command: { storeId, enabled, twoFactorToken, code })` and `updateMfaOrganizationMode(command: { storeId, organizationId, enabled, twoFactorToken, code })` set the mode to `Enabled` or `Off` and return the new settings.

A change is refused with `mfa_self_service_not_allowed` outside a `CustomerChoice` store, `mfa_locked_by_administrator` when the mode is `Required`, and `mfa_organization_not_allowed` when the user lacks `xapi:my_organization:mfa:manage` or the organization isn't theirs. A refused code returns the same error codes as the sign-in.

### Backoffice and REST API

| Endpoint | Permission | Description |
| --- | --- | --- |
| `GET /api/mfa/modes/users/{userId}`, `GET /api/mfa/modes/organizations/{organizationId}` | `mfa:read` | The mode, for example `{ "mode": "Off" }`. |
| `PUT /api/mfa/modes/users/{userId}`, `PUT /api/mfa/modes/organizations/{organizationId}` | `mfa:manage` | Sets the mode, body `{ "mode": "Required" }`. An unknown mode returns `400`. |
| `GET /api/mfa/modes/users/{userId}/status`, `GET /api/mfa/modes/organizations/{organizationId}/status` | `mfa:read` | Whether the sign-in needs the code in every store and what requires it, the global setting and, for a Backoffice user, the Backoffice sign-in. The optional `mode` query parameter evaluates that mode instead of the saved one. |

The code is sent by the `MfaSignInEmailNotification` notification in the user's default language, or the store's default language. A sign-in to a store uses the store's template and is sent from the store's email (`AdminEmail`, then `Email`); a sign-in without a store uses the global template and the default sender. If the notification is not active, no code is sent: the sign-in reports `mfa_provider_not_available` and a warning is logged. Like any notification, the email with the code is saved to the notification journal, so access to the journal should be limited to the people who may see sign-in codes.

## Rules

* MFA applies to password sign-ins only. External sign-in (Google, Microsoft Entra ID, etc.) uses the `bypassTwoFactor` option of Identity: the external provider is responsible for its own second factor. **If users sign in to the Backoffice with an external provider, enable MFA in that provider** (for example, Conditional Access in Microsoft Entra ID); `MultiFactorAuthentication.Enabled` does not protect those sign-ins. OTP sign-in, refresh tokens and API keys are not affected.
* Basic authentication is refused for users who require MFA; use an API key instead.
* A user whose own Identity two-factor setting is enabled (`TwoFactorEnabled` with a confirmed email) requires the code regardless of the MFA settings, as Identity defines.
* Accounts in the customer role (`__customer`) do not sign in to the Backoffice, with or without the code: the same rule as the Backoffice password sign-in.
* Accounts with lockout disabled can't complete a sign-in that requires MFA.
* Wrong codes count toward the platform account lockout, shared with wrong passwords. While the code is pending, a correct password does not reset the failed attempt count; a correct code resets it. A locked account isn't sent a code.
* A user can enter at most `MultiFactorAuthentication:MaxCodeAttempts` wrong codes within `MultiFactorAuthentication:TwoFactorTokenLifetime` from the first one, whatever two-factor token or Backoffice session they come with, counted on each platform instance. Each code is counted before it is checked, so parallel requests can't check more. Then no code is checked or sent and every MFA step answers `code_attempts_exceeded` until the period ends; a correct code clears the count. The account lockout stays the second limit. A two-factor token is also refused when a wrong code can't be counted toward the lockout.
* Only someone who knows the password can reach the code step, so a `code_attempts_exceeded` the user didn't cause means someone else knows the password. The user should change it; the sign-in log shows the `InvalidCode` and `CodeAttemptsExceeded` records of those attempts.
* Every MFA step runs after the password is verified, so `lockout_disabled` and `mfa_provider_not_available` are always reported: they reveal nothing to someone without the password and tell the user what to fix. A lockout is reported at the token endpoint and by the resend endpoint only when `PasswordLogin:DetailedErrors` is enabled (otherwise `invalid_code` from the `mfa` grant and `login_failed` from the resend endpoint), and always on the Backoffice code screen, like the Backoffice login page. The sign-in log always records the real reason: the password step with `RequiresTwoFactor`, the code step with the `MFA` sign-in type, the provider and the user of the two-factor token. The Backoffice also logs why a code can't be sent. A refused two-factor token is logged with its user when the token can still be decrypted, for example an expired one.
* The email is both the second factor and the password recovery channel: whoever controls the mailbox can reset the password and receive the code. Email codes protect against a leaked password, not against a compromised mailbox; an authenticator app or a passkey is needed for that.
* A code is sent to a user by the same provider at most once per `MultiFactorAuthentication:CodeResendInterval`, whichever sign-in asks for it: the password grant, the resend endpoint or the Backoffice code screen. A request that comes too soon sends nothing and returns `resend_too_soon` with `resendSecondsRemaining`. The password grant still answers `two_factor_required` with a new two-factor token: the code sent shortly before is still valid. An accepted code ends the limit, so a sign-in right after it gets a new code at once. The limit is kept in the memory of each platform instance.
* A code completes one sign-in, and only the last code sent is valid. The module replaces the Identity `Email` token provider with `MfaEmailTokenProvider`, which adds the user's code stamp to two-factor codes. The code stamp is kept in the Identity user tokens (`AspNetUserTokens`, login provider `VirtoCommerce.MFA`, name `CodeStamp`) and changes when a code is sent and when a code is accepted. So an accepted code is refused afterwards, also with a new two-factor token, in a new Backoffice session or on another platform instance; a new sign-in gets a new code at once; and a resend invalidates the code sent before. A refused code answers `invalid_code` and counts as a wrong code. Other purposes of the `Email` provider are not affected.
* A two-factor token completes one sign-in: it is redeemed when the code is accepted, so two requests with the same token and code can't both sign in on one platform instance, and it is refused once the user has signed in after it was issued (the user's last login date).
* Any change of the user's security stamp, for example a password change or a logout in another session, invalidates the two-factor token and the codes already sent.
* Apps that sign in to the token endpoint with a password without a store, such as vc-shell apps, must support the `two_factor_required` step when global MFA is enabled.

### Errors

| Code | Endpoint | Meaning |
| --- | --- | --- |
| `two_factor_required` | password grant | The password is correct, the code is required. |
| `missing_parameter` | `mfa` grant | Required parameters are missing. |
| `invalid_two_factor_token` | `mfa` grant, resend, Backoffice code screen | The sign-in session is invalid, expired, already used or belongs to another sign-in. Sign in with the password again. |
| `code_attempts_exceeded` | all | Too many wrong codes. Sign in again after `TwoFactorTokenLifetime`. |
| `invalid_code` | `mfa` grant, Backoffice code screen | The code is wrong, expired, already used or replaced by a newer code. |
| `sign_in_not_allowed` | `mfa` grant, Backoffice code screen | The account can't sign in: to the Backoffice, or at all, for example when it is not active. |
| `lockout_disabled` | all | The account has lockout disabled, so it can't pass the second factor. Enable lockout for the account. |
| `mfa_provider_not_available` | all | No provider can deliver a code to the account, for example the email is not confirmed or the notification is not active. Confirm the email or activate the notification. |
| `resend_too_soon` | resend, Backoffice code screen | A code has just been sent. Request a new one after `resendSecondsRemaining` seconds. |
| `user_is_locked_out`, `user_is_temporary_locked_out` | all | The account is locked; `lockoutSecondsRemaining` is returned. |
| `login_failed` | password grant, resend, Backoffice code screen | Any other failure, for example a lockout at the token endpoint without `PasswordLogin:DetailedErrors`, or several accounts with the same email. |

## Extending

A new way to deliver the code, for example SMS, is added by registering an `IMfaProvider` implementation named after the ASP.NET Core Identity two-factor provider that generates and verifies its codes. Identity decides whether the provider is available to a user. A provider whose codes the user already has, such as an authenticator app, returns `false` from `SendsCode`: the module then sends nothing and applies no resend limit.

The wrong code limit (`IMfaCodeAttemptLimiter`), the redeemed two-factor tokens (`ITwoFactorTicketStore`) and the resend limit (`IMfaCodeDeliveryLimiter`) are kept in the memory of each platform instance. Their interfaces are asynchronous, so they can be replaced with an implementation shared across instances, for example on Redis.

Who requires MFA is decided by `IMfaRequirementService.GetRequirementAsync`; override it to change the rules. The sign-in (`IsRequiredAsync`, `MfaTwoFactorPolicy`), the Backoffice status (`IMfaStatusService`) and the storefront (`IMfaSelfService`) all go through it, so they follow the changed rules. Overriding only `IsRequiredAsync` or `MfaTwoFactorPolicy` changes the sign-in but not what the Backoffice and the storefront show. The modes are read and saved by `IMfaModeService`. The errors and the sign-in log reasons of every outcome come from `MfaErrorDescriber`; register a derived class with `AddSingleton<MfaErrorDescriber, ...>()` to change them.

## References

* [Deployment](https://docs.virtocommerce.org/platform/developer-guide/Tutorials-and-How-tos/Tutorials/deploy-module-from-source-code/)
* [Installation](https://docs.virtocommerce.org/platform/user-guide/modules-installation/)
* [Home](https://virtocommerce.com)
* [Community](https://www.virtocommerce.org)

## License

Copyright (c) Virto Solutions LTD.  All rights reserved.

Licensed under the Virto Commerce Open Software License (the "License"); you
may not use this file except in compliance with the License. You may
obtain a copy of the License at

<https://virtocommerce.com/open-source-license>

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
implied.
