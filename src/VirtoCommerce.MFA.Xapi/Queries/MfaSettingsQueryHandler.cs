using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.MFA.Core.Models;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.MFA.Xapi.Queries;

public class MfaSettingsQueryHandler(IMfaSelfService selfService) : IQueryHandler<MfaSettingsQuery, MfaSettings>
{
    public virtual Task<MfaSettings> Handle(MfaSettingsQuery request, CancellationToken cancellationToken)
    {
        return selfService.GetSettingsAsync(request.User, request.StoreId, request.OrganizationId, request.CanManageOrganization);
    }
}
