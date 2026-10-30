using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using GraphQL;
using Microsoft.AspNetCore.Authorization;
using Moq;
using VirtoCommerce.MFA.Xapi.Authorization;
using VirtoCommerce.Xapi.Core.Infrastructure;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Xapi;

[Trait("Category", "Unit")]
public class MfaUserResolverTests : MfaTestsBase
{
    private readonly Mock<IAuthorizationService> _authorizationService = new();

    [Theory]
    [InlineData(OrganizationId, true)]
    [InlineData("another-organization", false)]
    [InlineData(null, false)]
    public async Task IsOrganizationMemberAsync_Should_AcceptOnlyTheOrganizationsOfTheContact(string organizationId, bool expected)
    {
        // Act
        var isMember = await CreateResolver().IsOrganizationMemberAsync(CreateContext(), ActiveUser, organizationId);

        // Assert
        Assert.Equal(expected, isMember);
    }

    [Fact]
    public async Task IsOrganizationMemberAsync_Should_Refuse_When_TheUserHasNoContact()
    {
        // Act
        var isMember = await CreateResolver().IsOrganizationMemberAsync(CreateContext(), BackofficeUser, OrganizationId);

        // Assert
        Assert.False(isMember);
    }

    [Theory]
    [InlineData(true, OrganizationId, true)]
    [InlineData(true, "another-organization", false)]
    [InlineData(false, OrganizationId, false)]
    public async Task CanManageOrganizationAsync_Should_RequireThePermissionAndTheMembership(bool hasPermission, string organizationId, bool expected)
    {
        // Arrange
        _authorizationService
            .Setup(x => x.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(hasPermission ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        // Act
        var canManage = await CreateResolver().CanManageOrganizationAsync(CreateContext(), ActiveUser, organizationId);

        // Assert
        Assert.Equal(expected, canManage);
    }

    private MfaUserResolver CreateResolver()
    {
        return new MfaUserResolver(() => UserManager.Object, _authorizationService.Object, MemberService.Object);
    }

    private static IResolveFieldContext CreateContext()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, ActiveUserId)], "test"));

        var context = new Mock<IResolveFieldContext>();
        context.Setup(x => x.UserContext).Returns(new GraphQLUserContext(principal));
        context.Setup(x => x.RequestServices).Returns(Mock.Of<IServiceProvider>());

        return context.Object;
    }
}
