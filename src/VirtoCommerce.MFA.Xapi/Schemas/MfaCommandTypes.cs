using GraphQL.Types;
using VirtoCommerce.MFA.Xapi.Commands;
using VirtoCommerce.MFA.Xapi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.MFA.Xapi.Schemas;

public class InputRequestMfaChangeCodeType : InputObjectGraphType<RequestMfaChangeCodeCommand>
{
    public InputRequestMfaChangeCodeType()
    {
        Field(x => x.StoreId);
    }
}

public class InputUpdateMfaUserModeType : InputObjectGraphType<UpdateMfaUserModeCommand>
{
    public InputUpdateMfaUserModeType()
    {
        Field(x => x.StoreId);
        Field(x => x.Enabled);
        Field(x => x.TwoFactorToken).Description("The two-factor token from requestMfaChangeCode.");
        Field(x => x.Code).Description("The code sent by requestMfaChangeCode.");
    }
}

public class InputUpdateMfaOrganizationModeType : InputObjectGraphType<UpdateMfaOrganizationModeCommand>
{
    public InputUpdateMfaOrganizationModeType()
    {
        Field(x => x.StoreId);
        Field(x => x.OrganizationId);
        Field(x => x.Enabled);
        Field(x => x.TwoFactorToken).Description("The two-factor token from requestMfaChangeCode.");
        Field(x => x.Code).Description("The code sent by requestMfaChangeCode.");
    }
}

public class MfaCodeChallengeType : ExtendableGraphType<MfaCodeChallenge>
{
    public MfaCodeChallengeType()
    {
        Name = "MfaCodeChallenge";

        Field(x => x.Succeeded);
        Field(x => x.TwoFactorToken, nullable: true);
        Field(x => x.Provider, nullable: true);
        Field(x => x.Destination, nullable: true);
        Field(x => x.ResendSecondsRemaining, nullable: true);
        Field(x => x.LockoutSecondsRemaining, nullable: true);
        Field(x => x.ErrorCode, nullable: true);
        Field(x => x.ErrorDescription, nullable: true);
    }
}

public class MfaChangeResponseType : ExtendableGraphType<MfaChangeResponse>
{
    public MfaChangeResponseType()
    {
        Name = "MfaChangeResponse";

        Field(x => x.Succeeded);
        Field(x => x.ErrorCode, nullable: true);
        Field(x => x.ErrorDescription, nullable: true);
        Field(x => x.LockoutSecondsRemaining, nullable: true);
        Field<MfaSettingsType>(nameof(MfaChangeResponse.Settings)).Resolve(context => context.Source.Settings);
    }
}
