using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using VirtoCommerce.MFA.Data.Services;
using VirtoCommerce.Platform.Security;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public class BackofficeExemptionIdentityTests : MfaTestsBase
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Exemption_Should_NotOverrideTheIdentityTwoFactorRequirement(bool identityEnabled, bool expected)
    {
        IsGloballyEnabled = true;
        BackofficeUser.TwoFactorEnabled = identityEnabled;
        BackofficeExemptionService.Setup(x => x.IsExemptAsync(BackofficeUserId)).ReturnsAsync(true);
        var policy = new MfaTwoFactorPolicy(RequirementService, Options.Create(MfaOptions), new HttpContextAccessor());
        var service = new TwoFactorSignInService(UserManager.Object, [policy], []);

        Assert.Equal(expected, await service.IsTwoFactorRequiredAsync(BackofficeUser));
    }
}
