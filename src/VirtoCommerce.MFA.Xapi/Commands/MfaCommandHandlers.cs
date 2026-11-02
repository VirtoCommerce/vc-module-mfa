using System.Threading;
using System.Threading.Tasks;
using MediatR;
using VirtoCommerce.MFA.Core.Services;
using VirtoCommerce.MFA.Xapi.Models;

namespace VirtoCommerce.MFA.Xapi.Commands;

public class RequestMfaChangeCodeCommandHandler(IMfaSelfService selfService, MfaResponseFactory responseFactory)
    : IRequestHandler<RequestMfaChangeCodeCommand, MfaCodeChallenge>
{
    public virtual async Task<MfaCodeChallenge> Handle(RequestMfaChangeCodeCommand request, CancellationToken cancellationToken)
    {
        var result = await selfService.SendChangeCodeAsync(request.User, request.StoreId);

        return responseFactory.ToCodeChallenge(result);
    }
}

public class UpdateMfaUserModeCommandHandler(IMfaSelfService selfService, MfaResponseFactory responseFactory)
    : IRequestHandler<UpdateMfaUserModeCommand, MfaChangeResponse>
{
    public virtual async Task<MfaChangeResponse> Handle(UpdateMfaUserModeCommand request, CancellationToken cancellationToken)
    {
        var result = await selfService.ChangeUserModeAsync(request.User, request.StoreId, request.Enabled, request.TwoFactorToken, request.Code);

        return responseFactory.ToChangeResponse(result, request.User);
    }
}

public class UpdateMfaOrganizationModeCommandHandler(IMfaSelfService selfService, MfaResponseFactory responseFactory)
    : IRequestHandler<UpdateMfaOrganizationModeCommand, MfaChangeResponse>
{
    public virtual async Task<MfaChangeResponse> Handle(UpdateMfaOrganizationModeCommand request, CancellationToken cancellationToken)
    {
        var result = await selfService.ChangeOrganizationModeAsync(request.User, request.StoreId, request.OrganizationId, request.CanManageOrganization,
            request.Enabled, request.TwoFactorToken, request.Code);

        return responseFactory.ToChangeResponse(result, request.User);
    }
}
