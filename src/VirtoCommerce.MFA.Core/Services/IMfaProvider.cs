using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.StoreModule.Core.Model;

namespace VirtoCommerce.MFA.Core.Services;

public interface IMfaProvider
{
    string Name { get; }

    string GetDestination(ApplicationUser user);

    Task SendCodeAsync(Store store, ApplicationUser user, string code);
}
