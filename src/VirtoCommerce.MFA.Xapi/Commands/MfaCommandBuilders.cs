using System.Threading.Tasks;
using GraphQL;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.MFA.Xapi.Authorization;
using VirtoCommerce.MFA.Xapi.Models;
using VirtoCommerce.MFA.Xapi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.MFA.Xapi.Commands;

public class RequestMfaChangeCodeCommandBuilder(IAuthorizationService authorizationService, MfaUserResolver userResolver)
    : CommandBuilder<RequestMfaChangeCodeCommand, MfaCodeChallenge, InputRequestMfaChangeCodeType, MfaCodeChallengeType>(authorizationService)
{
    protected override string Name => "requestMfaChangeCode";

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, RequestMfaChangeCodeCommand request)
    {
        await base.BeforeMediatorSend(context, request);

        request.User = await userResolver.GetCurrentUserAsync(context, isChange: true);
    }
}

public class UpdateMfaUserModeCommandBuilder(IAuthorizationService authorizationService, MfaUserResolver userResolver)
    : CommandBuilder<UpdateMfaUserModeCommand, MfaChangeResponse, InputUpdateMfaUserModeType, MfaChangeResponseType>(authorizationService)
{
    protected override string Name => "updateMfaUserMode";

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, UpdateMfaUserModeCommand request)
    {
        await base.BeforeMediatorSend(context, request);

        request.User = await userResolver.GetCurrentUserAsync(context, isChange: true);
    }
}

public class UpdateMfaOrganizationModeCommandBuilder(IAuthorizationService authorizationService, MfaUserResolver userResolver)
    : CommandBuilder<UpdateMfaOrganizationModeCommand, MfaChangeResponse, InputUpdateMfaOrganizationModeType, MfaChangeResponseType>(authorizationService)
{
    protected override string Name => "updateMfaOrganizationMode";

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, UpdateMfaOrganizationModeCommand request)
    {
        await base.BeforeMediatorSend(context, request);

        request.User = await userResolver.GetCurrentUserAsync(context, isChange: true);
        request.CanManageOrganization = await userResolver.CanManageOrganizationAsync(context, request.User, request.OrganizationId);
    }
}
