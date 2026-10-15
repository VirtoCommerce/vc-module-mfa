using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.StoreModule.Core.Model;

namespace VirtoCommerce.MFA.Core.Services;

public interface IMfaProvider
{
    string Name { get; }

    bool SendsCode { get; }

    string GetDestination(ApplicationUser user);

    Task<bool> SendCodeAsync(Store store, ApplicationUser user, string code);
}
