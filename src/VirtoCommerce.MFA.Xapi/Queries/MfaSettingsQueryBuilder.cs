using System.Threading.Tasks;
using GraphQL;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Xapi.Authorization;
using VirtoCommerce.MFA.Xapi.Schemas;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.MFA.Xapi.Queries;

public class MfaSettingsQueryBuilder(IAuthorizationService authorizationService, MfaUserResolver userResolver)
    : QueryBuilder<MfaSettingsQuery, MfaSettings, MfaSettingsType>(authorizationService)
{
    protected override string Name => "mfaSettings";

    protected override async Task BeforeMediatorSend(IResolveFieldContext<object> context, MfaSettingsQuery request)
    {
        await base.BeforeMediatorSend(context, request);

        request.User = await userResolver.GetCurrentUserAsync(context, isChange: false);
        request.CanManageOrganization = await userResolver.CanManageOrganizationAsync(context, request.User, request.OrganizationId);
    }
}
