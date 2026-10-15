using System.Security.Claims;
using System.Threading.Tasks;
using OpenIddict.Abstractions;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Data.OpenIddict;
using VirtoCommerce.Platform.Security.OpenIddict;
using Xunit;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace VirtoCommerce.MFA.Tests.OpenIddict;

[Trait("Category", "Unit")]
public class MfaAuthenticationMethodClaimProviderTests
{
    private readonly MfaAuthenticationMethodClaimProvider _provider = new();

    [Theory]
    [InlineData(ModuleConstants.Security.GrantType, false, true)]
    [InlineData(GrantTypes.RefreshToken, true, true)]
    [InlineData(GrantTypes.RefreshToken, false, false)]
    [InlineData(GrantTypes.Password, false, false)]
    public async Task SetClaimsAsync_Should_MarkTheTokensOfAnMfaSignIn(string grantType, bool refreshedTokenHasMfa, bool expectMfa)
    {
        // Arrange
        var refreshedIdentity = new ClaimsIdentity();
        if (refreshedTokenHasMfa)
        {
            refreshedIdentity.AddClaim(new Claim(Claims.AuthenticationMethodReference, ModuleConstants.Security.AuthenticationMethod));
        }

        var context = new TokenRequestContext
        {
            Request = new OpenIddictRequest { GrantType = grantType },
            Principal = new ClaimsPrincipal(refreshedIdentity),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        await _provider.SetClaimsAsync(principal, context);

        // Assert
        var claim = principal.FindFirst(Claims.AuthenticationMethodReference);
        Assert.Equal(expectMfa, claim?.Value == ModuleConstants.Security.AuthenticationMethod);
        if (expectMfa)
        {
            Assert.Contains(Destinations.AccessToken, claim.GetDestinations());
        }
    }
}
