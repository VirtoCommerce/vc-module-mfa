using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.MFA.Data.Providers;
using VirtoCommerce.NotificationsModule.Core.Services;
using VirtoCommerce.Platform.Core.Security;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Providers;

[Trait("Category", "Unit")]
public class EmailMfaProviderTests
{
    [Theory]
    [InlineData("john.smith@acme.com", "j•••h@acme.com")]
    [InlineData("jo@acme.com", "j•••@acme.com")]
    [InlineData("j@acme.com", "•••@acme.com")]
    [InlineData("not-an-email", "not-an-email")]
    [InlineData(null, null)]
    public void GetDestination_Should_MaskTheLocalPart(string email, string expected)
    {
        // Arrange
        var provider = new EmailMfaProvider(Mock.Of<IMemberService>(), Mock.Of<INotificationSearchService>(),
            Mock.Of<INotificationSender>(), NullLogger<EmailMfaProvider>.Instance);

        // Act
        var destination = provider.GetDestination(new ApplicationUser { Email = email });

        // Assert
        Assert.Equal(expected, destination);
    }
}
