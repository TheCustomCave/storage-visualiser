using System;
using System.IO;
using Shouldly;
using StorageVisualiser.Core.Formatting;
using StorageVisualiser.Core.Settings;
using StorageVisualiser.Core.Treemap;
using Xunit;

namespace StorageVisualiser.Core.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _testDir;

    public SettingsServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"StorageVisualiser_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }
        catch
        {
            // Ignore cleanup failures in temp dir
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Load_NonExistentFile_ReturnsDefaults()
    {
        var settingsFile = Path.Combine(_testDir, "settings.json");
        var service = new SettingsService(settingsFile);

        var settings = service.Current;
        settings.ShouldNotBeNull();
        settings.ColorMode.ShouldBe(TreemapColorMode.DepthRainbow);
        settings.ColorBlindSafe.ShouldBeFalse();
        settings.UnitSystem.ShouldBe(UnitSystem.Windows);
        settings.UseAllocatedSize.ShouldBeFalse();
        settings.DefaultDetailLevel.ShouldBe(3);
        settings.LayoutBias.ShouldBe(TreemapBias.Equal);
        settings.ShowFreeSpace.ShouldBeTrue();
        settings.TreePercentageRelativeToTotal.ShouldBeTrue();
        settings.ConfirmBeforeDelete.ShouldBeTrue();
        settings.AutoRescanAfterDelete.ShouldBeTrue();
    }

    [Fact]
    public void SaveAndLoad_CustomSettings_PersistsCorrectly()
    {
        var settingsFile = Path.Combine(_testDir, "custom_settings.json");
        var service = new SettingsService(settingsFile);

        var custom = new AppSettings
        {
            ColorMode = TreemapColorMode.FileTypeCategory,
            ColorBlindSafe = true,
            UnitSystem = UnitSystem.Iec,
            UseAllocatedSize = true,
            DefaultDetailLevel = 5,
            LayoutBias = TreemapBias.Horizontal,
            ShowFreeSpace = false,
            TreePercentageRelativeToTotal = false,
            ConfirmBeforeDelete = false,
            AutoRescanAfterDelete = false
        };

        bool saved = service.Save(custom);
        saved.ShouldBeTrue();
        File.Exists(settingsFile).ShouldBeTrue();

        // Create new service instance to reload from disk
        var reloadedService = new SettingsService(settingsFile);
        var loaded = reloadedService.Current;

        loaded.ColorMode.ShouldBe(TreemapColorMode.FileTypeCategory);
        loaded.ColorBlindSafe.ShouldBeTrue();
        loaded.UnitSystem.ShouldBe(UnitSystem.Iec);
        loaded.UseAllocatedSize.ShouldBeTrue();
        loaded.DefaultDetailLevel.ShouldBe(5);
        loaded.LayoutBias.ShouldBe(TreemapBias.Horizontal);
        loaded.ShowFreeSpace.ShouldBeFalse();
        loaded.TreePercentageRelativeToTotal.ShouldBeFalse();
        loaded.ConfirmBeforeDelete.ShouldBeFalse();
        loaded.AutoRescanAfterDelete.ShouldBeFalse();
    }

    [Fact]
    public void Load_CorruptedJson_FallsBackToDefaultsGracefully()
    {
        var settingsFile = Path.Combine(_testDir, "corrupted.json");
        File.WriteAllText(settingsFile, "{ this is not valid json! @#$%^ }");

        var service = new SettingsService(settingsFile);
        var settings = service.Load();

        settings.ShouldNotBeNull();
        settings.ColorMode.ShouldBe(TreemapColorMode.DepthRainbow);
        settings.DefaultDetailLevel.ShouldBe(3);
    }

    [Fact]
    public void Save_TriggersSettingsChangedEvent()
    {
        var settingsFile = Path.Combine(_testDir, "event_settings.json");
        var service = new SettingsService(settingsFile);

        AppSettings? eventReceived = null;
        service.SettingsChanged += s => eventReceived = s;

        var updated = new AppSettings
        {
            ColorMode = TreemapColorMode.FileAge,
            UnitSystem = UnitSystem.Si
        };

        service.Save(updated);

        eventReceived.ShouldNotBeNull();
        eventReceived.ColorMode.ShouldBe(TreemapColorMode.FileAge);
        eventReceived.UnitSystem.ShouldBe(UnitSystem.Si);
    }
}
