using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.MFA.Data.Services;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using Xunit;
using MfaModuleSettings = VirtoCommerce.MFA.Core.ModuleConstants.Settings.General;
using Modes = VirtoCommerce.MFA.Core.ModuleConstants.Modes;

namespace VirtoCommerce.MFA.Tests.Services;

[Trait("Category", "Unit")]
public class MfaModeServiceTests
{
    private readonly Dictionary<(string Name, string ObjectType, string ObjectId), object> _settings = new();
    private readonly Mock<ISettingsManager> _settingsManager = new();
    private readonly MfaModeService _modeService;

    public MfaModeServiceTests()
    {
        _settingsManager
            .Setup(x => x.GetObjectSettingsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((IEnumerable<string> names, string objectType, string objectId) => names
                .Where(name => _settings.ContainsKey((name, objectType, objectId)))
                .Select(name => new ObjectSettingEntry { Name = name, ObjectType = objectType, ObjectId = objectId, Value = _settings[(name, objectType, objectId)] })
                .ToList());

        _settingsManager
            .Setup(x => x.SaveObjectSettingsAsync(It.IsAny<IEnumerable<ObjectSettingEntry>>()))
            .Callback((IEnumerable<ObjectSettingEntry> entries) =>
            {
                foreach (var entry in entries)
                {
                    _settings[(entry.Name, entry.ObjectType, entry.ObjectId)] = entry.Value;
                }
            })
            .Returns(Task.CompletedTask);

        _modeService = new MfaModeService(_settingsManager.Object);
    }

    [Theory]
    [InlineData("required", Modes.Required)]
    [InlineData("ENABLED", Modes.Enabled)]
    [InlineData("Bogus", Modes.Off)]
    [InlineData(null, Modes.Off)]
    public async Task SetUserModeAsync_Should_SaveTheKnownModeInTheUserSettings(string mode, string expectedMode)
    {
        // Act
        await _modeService.SetUserModeAsync("user-1", mode);

        // Assert
        Assert.Equal(expectedMode, _settings[(MfaModuleSettings.UserMode.Name, nameof(ApplicationUser), "user-1")]);
        Assert.Equal(expectedMode, await _modeService.GetUserModeAsync("user-1"));
    }

    [Fact]
    public async Task SetOrganizationModeAsync_Should_SaveTheModeInTheOrganizationSettings()
    {
        // Act
        await _modeService.SetOrganizationModeAsync("organization-1", Modes.Required);

        // Assert
        Assert.Equal(Modes.Required, _settings[(MfaModuleSettings.OrganizationMode.Name, nameof(Organization), "organization-1")]);
        Assert.Equal(Modes.Required, await _modeService.GetOrganizationModeAsync("organization-1"));
        Assert.Equal(Modes.Off, await _modeService.GetUserModeAsync("organization-1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown-user")]
    public async Task GetUserModeAsync_Should_ReturnOff_When_NoModeIsSaved(string userId)
    {
        // Act
        var mode = await _modeService.GetUserModeAsync(userId);

        // Assert
        Assert.Equal(Modes.Off, mode);
    }

    [Fact]
    public async Task GetUserModeAsync_Should_ReturnOff_When_TheSavedValueIsUnknown()
    {
        // Arrange
        _settings[(MfaModuleSettings.UserMode.Name, nameof(ApplicationUser), "user-1")] = "Bogus";

        // Act
        var mode = await _modeService.GetUserModeAsync("user-1");

        // Assert
        Assert.Equal(Modes.Off, mode);
    }

    [Fact]
    public async Task GetOrganizationModesAsync_Should_ReturnEveryOrganizationOnce()
    {
        // Arrange
        await _modeService.SetOrganizationModeAsync("organization-1", Modes.Enabled);

        // Act
        var modes = await _modeService.GetOrganizationModesAsync(["organization-1", "organization-2", "organization-1", null, ""]);

        // Assert
        Assert.Equal(2, modes.Count);
        Assert.Equal(Modes.Enabled, modes["organization-1"]);
        Assert.Equal(Modes.Off, modes["organization-2"]);
    }
}
