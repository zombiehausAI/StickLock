using StickLock.Models;
using StickLock.Plugins;
using StickLock.Plugins.EliteDangerous;
using StickLock.Plugins.StarCitizen;
using StickLock.Services;
using Xunit;

namespace StickLock.Tests;

public class PluginSystemTests : IDisposable {
    private readonly string _tempDir;
    private readonly string _scXmlPath;
    private readonly string _edBindingsDir;
    private readonly string _configPath;

    public PluginSystemTests() {
        _tempDir = Path.Combine(Path.GetTempPath(), "StickLock_Plugin_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _scXmlPath = Path.Combine(_tempDir, "actionmaps.xml");
        File.WriteAllText(_scXmlPath, "<ActionMaps><actionmap name=\"spaceship_movement\"/></ActionMaps>");

        _edBindingsDir = Path.Combine(_tempDir, "EliteBindings");
        Directory.CreateDirectory(_edBindingsDir);
        File.WriteAllText(Path.Combine(_edBindingsDir, "StartPreset.4.start"), "Custom");
        File.WriteAllText(Path.Combine(_edBindingsDir, "Custom.4.0.binds"), "<Root PresetName=\"Custom\"><PitchAxisRaw><Binding Device=\"231D0200\" Key=\"Joy_YAxis\" /></PitchAxisRaw></Root>");

        _configPath = Path.Combine(_tempDir, ".sticklock", "config.json");
    }

    [Fact]
    public async Task GamePluginManager_InitializesWithRegisteredPlugins_AndAllowsSwitching() {
        var configService = new AppConfigService(_configPath);
        await configService.SetStarCitizenPathAsync(_scXmlPath);

        var diService = new TestDirectInputService();
        var scDetector = new TestStarCitizenDetector();
        var logService = new ActivityLogService();
        var xmlService = new ActionMapsXmlService(configService, diService, scDetector, logService);

        var scPlugin = new StarCitizenPlugin(xmlService, configService, scDetector);
        var edPlugin = new EliteDangerousPlugin(configService, diService, logService);

        var manager = new GamePluginManager(new IGamePlugin[] { scPlugin, edPlugin }, configService);

        Assert.Equal(2, manager.AvailablePlugins.Count);
        Assert.Equal("starcitizen", manager.ActivePlugin.Id);

        IGamePlugin? eventPlugin = null;
        manager.ActivePluginChanged += p => eventPlugin = p;

        await manager.SetActivePluginAsync("elitedangerous");

        Assert.Equal("elitedangerous", manager.ActivePlugin.Id);
        Assert.NotNull(eventPlugin);
        Assert.Equal("elitedangerous", eventPlugin.Id);
        Assert.Equal("elitedangerous", configService.Config.ActiveGamePluginId);
    }

    [Fact]
    public async Task EliteDangerousPlugin_DetectsPreset_CreatesBackup_AndRestores() {
        var configService = new AppConfigService(_configPath);
        var edBindsFile = Path.Combine(_edBindingsDir, "Custom.4.0.binds");
        await configService.SetEliteDangerousBindingsPathAsync(edBindsFile);

        var diService = new TestDirectInputService();
        var logService = new ActivityLogService();
        var edPlugin = new EliteDangerousPlugin(configService, diService, logService);

        Assert.True(await edPlugin.IsInstalledAsync());
        var activePath = await edPlugin.DetectActiveBindingsPathAsync();
        Assert.Equal(edBindsFile, activePath);

        // Perform sync (creates backup)
        var syncResult = await edPlugin.SyncAsync(force: true);
        Assert.True(syncResult.Success);
        Assert.NotNull(syncResult.BackupPath);
        Assert.True(File.Exists(syncResult.BackupPath));

        var backups = await edPlugin.GetAvailableBackupsAsync();
        Assert.Single(backups);

        // Modify active file, then restore
        await File.WriteAllTextAsync(edBindsFile, "<CorruptedRoot />");
        var restoreResult = await edPlugin.RestoreLatestBackupAsync();
        Assert.True(restoreResult.Success);

        var restoredContent = await File.ReadAllTextAsync(edBindsFile);
        Assert.Contains("PresetName=\"Custom\"", restoredContent);
    }

    [Fact]
    public async Task GamePluginLoader_And_Manager_HandlesExternalPluginsAndReload() {
        var configService = new AppConfigService(_configPath);
        var diService = new TestDirectInputService();
        var scDetector = new TestStarCitizenDetector();
        var logService = new ActivityLogService();
        var xmlService = new ActionMapsXmlService(configService, diService, scDetector, logService);

        var scPlugin = new StarCitizenPlugin(xmlService, configService, scDetector);
        var edPlugin = new EliteDangerousPlugin(configService, diService, logService);

        var loader = new GamePluginLoader(configService, logService);
        var manager = new GamePluginManager(new IGamePlugin[] { scPlugin, edPlugin }, configService, loader);

        Assert.Equal(2, manager.AvailablePlugins.Count);
        Assert.Equal(Path.Combine(configService.BaseDirectory, "plugins"), manager.UserPluginsDirectory);

        // Test reload does not crash and maintains active selection
        await manager.ReloadPluginsAsync();
        Assert.Equal("starcitizen", manager.ActivePlugin.Id);
    }

    [Fact]
    public async Task MappingsDirectory_IsPartitionedByGamePlugin_AndMigratesLegacyFiles() {
        var baseDir = Path.Combine(_tempDir, "mappings_migration_test");
        var legacyMappingsDir = Path.Combine(baseDir, "mappings");
        Directory.CreateDirectory(legacyMappingsDir);

        // Place a legacy profile directly in ~/.sticklock/mappings/
        var legacyFile = Path.Combine(legacyMappingsDir, "layout_Legacy_Profile.xml");
        await File.WriteAllTextAsync(legacyFile, "<ActionMaps></ActionMaps>");

        var configFilePath = Path.Combine(baseDir, "config.json");
        var configService = new AppConfigService(configFilePath);

        // Check active defaults to starcitizen
        Assert.EndsWith(Path.Combine("mappings", "starcitizen"), configService.MappingsDirectory);

        // Check migration: legacy file moved into mappings/starcitizen/
        var migratedFile = Path.Combine(configService.MappingsDirectory, "layout_Legacy_Profile.xml");
        Assert.True(File.Exists(migratedFile));
        Assert.False(File.Exists(legacyFile));

        // Switch to Elite Dangerous
        await configService.SetActiveGamePluginIdAsync("elitedangerous");
        Assert.EndsWith(Path.Combine("mappings", "elitedangerous"), configService.MappingsDirectory);
        Assert.True(Directory.Exists(configService.MappingsDirectory));
    }

    public void Dispose() {
        try {
            if (Directory.Exists(_tempDir)) {
                Directory.Delete(_tempDir, true);
            }
        } catch { }
    }
}

