using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.MFA.Xapi.Queries;

public class MfaSettingsQuery : Query<MfaSettings>
{
    public string StoreId { get; set; }

    public string OrganizationId { get; set; }

    public ApplicationUser User { get; set; }

    public bool CanManageOrganization { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(StoreId));
        yield return Argument<StringGraphType>(nameof(OrganizationId));
    }

    public override void Map(IResolveFieldContext context)
    {
        StoreId = context.GetArgument<string>(nameof(StoreId));
        OrganizationId = context.GetArgument<string>(nameof(OrganizationId));
    }
}
