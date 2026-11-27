using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.MFA.Core.Services;
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

    [Theory]
    [InlineData(null, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData("true", false)]
    public async Task IsBackofficeExemptAsync_Should_RequireAnExplicitBooleanTrue(object savedValue, bool expected)
    {
        if (savedValue != null)
        {
            _settings[(MfaModuleSettings.BackofficeExempt.Name, nameof(ApplicationUser), "user-1")] = savedValue;
        }

        Assert.Equal(expected, await _modeService.IsBackofficeExemptAsync("user-1"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetBackofficeExemptionAsync_Should_PersistTheFlagWithoutChangingTheUserMode(bool isExempt)
    {
        await _modeService.SetUserModeAsync("user-1", Modes.Required);

        await _modeService.SetBackofficeExemptionAsync("user-1", isExempt);

        Assert.Equal(isExempt, _settings[(MfaModuleSettings.BackofficeExempt.Name, nameof(ApplicationUser), "user-1")]);
        Assert.Equal(isExempt, await _modeService.IsBackofficeExemptAsync("user-1"));
        Assert.Equal(Modes.Required, await _modeService.GetUserModeAsync("user-1"));
        Assert.False(await _modeService.IsBackofficeExemptAsync("user-2"));
    }

    [Fact]
    public async Task LegacyModeService_Should_GrantNoBackofficeExemptionByDefault()
    {
        IMfaModeService service = new LegacyModeService();

        Assert.Equal(Modes.Off, await service.GetUserModeAsync("user-1"));
        Assert.False(await service.IsBackofficeExemptAsync("user-1"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LegacyModeService_Should_RejectUnsupportedExemptionWrites(bool isExempt)
    {
        IMfaModeService service = new LegacyModeService();

        await Assert.ThrowsAsync<NotSupportedException>(() => service.SetBackofficeExemptionAsync("user-1", isExempt));
        Assert.False(await service.IsBackofficeExemptAsync("user-1"));
    }

    private sealed class LegacyModeService : IMfaModeService
    {
        private readonly InMemoryMfaModeService _inner = new();

        public Task<string> GetUserModeAsync(string userId) => _inner.GetUserModeAsync(userId);

        public Task SetUserModeAsync(string userId, string mode) => _inner.SetUserModeAsync(userId, mode);

        public Task<string> GetOrganizationModeAsync(string organizationId) => _inner.GetOrganizationModeAsync(organizationId);

        public Task<IDictionary<string, string>> GetOrganizationModesAsync(IList<string> organizationIds) => _inner.GetOrganizationModesAsync(organizationIds);

        public Task SetOrganizationModeAsync(string organizationId, string mode) => _inner.SetOrganizationModeAsync(organizationId, mode);
    }
}
