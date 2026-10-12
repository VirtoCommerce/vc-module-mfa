# Virto Commerce Multi-Factor Authentication Module

## Overview

The module adds multi-factor authentication (MFA) to password sign-ins: after the password, the user enters a one-time code sent by email. It covers the Backoffice and any other password sign-in without a store, and the storefront password sign-in of each store.

The module is built on the ASP.NET Core Identity two-factor mechanism. It tells Identity which sign-ins require a second factor through the platform `ITwoFactorPolicy`, which the platform `SignInManager.IsTwoFactorEnabledAsync` consults. Identity then answers `RequiresTwoFactor` to password sign-ins and keeps the failed access count until the second factor is passed.

## Configuration

| Setting | Level and place in the Backoffice | Description | Default |
| --- | --- | --- | --- |
| `MultiFactorAuthentication.Enabled` | Global: Settings > Platform > Security | Requires the code for every password sign-in without a store: the Backoffice, and apps and API clients that sign in with a password. Stores follow this setting unless they override it. | `false` |
| `MultiFactorAuthentication.StoreMode` | Store: Stores > store > Settings > Store > Security | `Use global setting`: follow `MultiFactorAuthentication.Enabled`. `Enabled`: required for every password sign-in to the store and for every password sign-in of the store's own users, whatever store they name. `Disabled`: not required for the store's own customers. | `Use global setting` |

Tokens are not limited to the store the request names, so the rules look at the user's own store too. A store that enables MFA requires it from its own users even when they name another store or none. A store can relax the global setting for its own customer accounts only: users whose `UserType` is `Customer`, whose store is this store and who are not administrators. Everyone else follows the global setting in that store, including Backoffice users and customers of other stores that trust it, so naming a store in the token request cannot switch the second factor off. A request that names an unknown store also follows the global setting.

| appsettings.json | Description | Default |
| --- | --- | --- |
| `MultiFactorAuthentication:TwoFactorTokenLifetime` | How long the two-factor token of the token endpoint sign-in stays valid. | `00:10:00` |

Codes are sent with the ASP.NET Core Identity `Email` two-factor provider, which requires a confirmed email. **Before enabling MFA, confirm the email of the users it applies to** (the account blade in the Backoffice has an "Email confirmed" switch); a user without a confirmed email cannot complete the sign-in.

## How it works

### Backoffice

1. The user signs in with the password on the Backoffice login page. When MFA is required, the platform sign-in answers `requiresTwoFactor` and Identity keeps the user in its two-factor cookie.
2. The login page goes to the code screen of the module, which sends the code with `POST /api/mfa/signin/send`.
3. The user enters the code, and `POST /api/mfa/signin/verify` (`code`, `rememberMe`, optional `provider`) completes the sign-in with `SignInManager.TwoFactorSignInAsync`. A wrong code counts toward the lockout. A correct code resets the failed attempt count and signs the user in.

### Token endpoint (storefront, apps)

1. The client signs in with the password grant: `POST /connect/token` (`grant_type=password`, `username`, `password`, optional `storeId`, optional `provider`). When MFA is required and the password is correct, the platform does not issue tokens. The module sends a code and the response is `400` with the `two_factor_required` error code:

    ```json
    {
      "error": "invalid_grant",
      "errors": [{ "code": "two_factor_required", "description": "..." }],
      "twoFactorToken": "CfDJ8...",
      "provider": "Email",
      "providers": [{ "name": "Email", "destination": "j•••h@acme.com" }]
    }
    ```

2. The client exchanges the code for tokens: `POST /connect/token` (`grant_type=mfa`, `two_factor_token`, `code`, and `storeId` when the first step named a store).
3. To send the code again, the client calls `POST /api/mfa/resend` (`twoFactorToken`, optional `provider`). The response has a new `twoFactorToken` to use from then on, which expires at the same time as the first one, and the `provider` the code was sent with. Within the time step of the Identity provider, the same code is sent again.

The two-factor token proves that the password was verified. It is an encrypted ticket with the user, the store, the user's security stamp and the provider the current code was sent with, valid for `MultiFactorAuthentication:TwoFactorTokenLifetime` (10 minutes by default) from the password step. `provider` names the provider the code was sent with, and `providers` lists every provider available to the user with its masked destination. The tokens issued by the `mfa` grant, and the tokens refreshed from them, have the `amr` claim `mfa`. Codes are generated and verified by the Identity two-factor provider (`GenerateTwoFactorTokenAsync`, `VerifyTwoFactorTokenAsync`), which also defines their length and lifetime. The module stores nothing.

The code is sent by the `MfaSignInEmailNotification` notification in the user's default language, or the store's default language. A sign-in to a store uses the store's template, a sign-in without a store uses the global one.

## Rules

* MFA applies to password sign-ins only. External sign-in (Google, Microsoft Entra ID, etc.) uses the `bypassTwoFactor` option of Identity: the external provider is responsible for its own second factor. **If users sign in to the Backoffice with an external provider, enable MFA in that provider** (for example, Conditional Access in Microsoft Entra ID); `MultiFactorAuthentication.Enabled` does not protect those sign-ins. OTP sign-in, refresh tokens and API keys are not affected.
* Basic authentication is refused for users who require MFA; use an API key instead.
* A user whose own Identity two-factor setting is enabled (`TwoFactorEnabled` with a confirmed email) requires the code regardless of the MFA settings, as Identity defines.
* Accounts in the customer role (`__customer`) do not sign in to the Backoffice, with or without the code: the same rule as the Backoffice password sign-in. Note that this is the role, while the store setting above looks at the user type.
* Accounts with lockout disabled can't complete a sign-in that requires MFA.
* Wrong codes count toward the platform account lockout, shared with wrong passwords. While the code is pending, a correct password does not reset the failed attempt count; a correct code resets it. A locked account isn't sent a code.
* Every MFA step runs after the password is verified, so `lockout_disabled` and `mfa_provider_not_available` are always reported: they reveal nothing to someone without the password and tell the user what to fix. A lockout is reported at the token endpoint only when `PasswordLogin:DetailedErrors` is enabled (otherwise `invalid_code`), and always on the Backoffice code screen, like the Backoffice login page. The sign-in log always records the real reason: the password step with `RequiresTwoFactor`, the code step with the `MFA` sign-in type.
* The email is both the second factor and the password recovery channel: whoever controls the mailbox can reset the password and receive the code. Email codes protect against a leaked password, not against a compromised mailbox; an authenticator app or a passkey is needed for that.
* Code resends are not rate-limited.
* A two-factor token completes one sign-in: it is refused once the user has signed in after it was issued (the user's last login date).
* Any change of the user's security stamp, for example a password change or a logout in another session, invalidates the two-factor token and the codes already sent.
* Apps that sign in to the token endpoint with a password without a store, such as vc-shell apps, must support the `two_factor_required` step when global MFA is enabled.

### Errors

| Code | Endpoint | Meaning |
| --- | --- | --- |
| `two_factor_required` | password grant | The password is correct, the code has been sent. |
| `missing_parameter` | `mfa` grant | Required parameters are missing. |
| `invalid_two_factor_token` | `mfa` grant, resend, Backoffice code screen | The sign-in session is invalid, expired or belongs to another sign-in. Sign in with the password again. |
| `invalid_code` | `mfa` grant, Backoffice code screen | The code is wrong or expired. |
| `sign_in_not_allowed` | `mfa` grant, Backoffice code screen | The account can't sign in: to the Backoffice, or at all, for example when it is not active. |
| `lockout_disabled` | all | The account has lockout disabled, so it can't pass the second factor. Enable lockout for the account. |
| `mfa_provider_not_available` | all | No provider can deliver a code to the account, for example the email is not confirmed. Confirm the email. |
| `user_is_locked_out`, `user_is_temporary_locked_out` | all | The account is locked; `lockoutSecondsRemaining` is returned. |
| `login_failed` | password grant, resend, Backoffice code screen | Any other failure, for example a lockout at the token endpoint without `PasswordLogin:DetailedErrors`, or several accounts with the same email. |

## Extending

A new way to deliver the code, for example SMS, is added by registering an `IMfaProvider` implementation named after the ASP.NET Core Identity two-factor provider that generates and verifies its codes. Identity decides whether the provider is available to a user.

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
