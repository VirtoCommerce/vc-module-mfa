using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using VirtoCommerce.MFA.Core;
using VirtoCommerce.MFA.Data.Services;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using Xunit;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public class MfaBackofficeExemptionServiceTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData("true", false)]
    public async Task IsExemptAsync_Should_RequireAnExplicitBooleanTrue(object savedValue, bool expected)
    {
        var settingsManager = new Mock<ISettingsManager>();
        settingsManager.Setup(x => x.GetObjectSettingsAsync(
                It.IsAny<IEnumerable<string>>(), nameof(ApplicationUser), "user-1"))
            .ReturnsAsync(savedValue == null ? [] : [new ObjectSettingEntry { Value = savedValue }]);
        var service = new MfaBackofficeExemptionService(settingsManager.Object);

        Assert.Equal(expected, await service.IsExemptAsync("user-1"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetExemptionAsync_Should_PersistAndReadBackTheUserSetting(bool isExempt)
    {
        var settingsManager = new Mock<ISettingsManager>();
        var settings = new List<ObjectSettingEntry>();
        settingsManager.Setup(x => x.SaveObjectSettingsAsync(It.IsAny<IEnumerable<ObjectSettingEntry>>()))
            .Callback((IEnumerable<ObjectSettingEntry> entries) => settings = entries.ToList())
            .Returns(Task.CompletedTask);
        settingsManager.Setup(x => x.GetObjectSettingsAsync(
                It.IsAny<IEnumerable<string>>(), nameof(ApplicationUser), "user-1"))
            .ReturnsAsync(() => settings);
        var service = new MfaBackofficeExemptionService(settingsManager.Object);

        await service.SetExemptionAsync("user-1", isExempt);

        var setting = Assert.Single(settings);
        Assert.Equal(ModuleConstants.Settings.General.BackofficeExempt.Name, setting.Name);
        Assert.Equal(nameof(ApplicationUser), setting.ObjectType);
        Assert.Equal("user-1", setting.ObjectId);
        Assert.Equal(isExempt, setting.Value);
        Assert.Equal(isExempt, await service.IsExemptAsync("user-1"));
    }
}
