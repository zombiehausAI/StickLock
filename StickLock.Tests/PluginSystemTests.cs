using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public async Task DeclarativeGamePlugin_LoadsManifestAndExecutesLifecycle() {
        var pluginDir = Path.Combine(_tempDir, "declarative_test_game");
        Directory.CreateDirectory(pluginDir);
        var configFile = Path.Combine(pluginDir, "input_settings.cfg");
        await File.WriteAllTextAsync(configFile, "controller_1=VKB_Gladiator\ncontroller_2=TWCS_Throttle");

        var manifest = new StickLock.Plugins.Declarative.DeclarativePluginManifest {
            Id = "testgame",
            Name = "Test Sim Game",
            Description = "A declarative test sim",
            ProcessName = "TestSim.exe",
            ConfigDirectory = pluginDir,
            FilePattern = "*.cfg"
        };

        var configService = new AppConfigService(_configPath);
        var diService = new TestDirectInputService();
        var logService = new ActivityLogService();

        var plugin = new StickLock.Plugins.Declarative.DeclarativeGamePlugin(manifest, configService, diService, logService);

        Assert.Equal("testgame", plugin.Id);
        Assert.Equal("Test Sim Game", plugin.Name);
        Assert.True(await plugin.IsInstalledAsync());

        var detectedFile = await plugin.DetectActiveBindingsPathAsync();
        Assert.Equal(configFile, detectedFile);

        var syncResult = await plugin.SyncAsync(force: true);
        Assert.True(syncResult.Success);
        Assert.NotNull(syncResult.BackupPath);
        Assert.True(File.Exists(syncResult.BackupPath));

        var backups = await plugin.GetAvailableBackupsAsync();
        Assert.Single(backups);

        // Corrupt and restore
        await File.WriteAllTextAsync(configFile, "corrupted");
        var restoreResult = await plugin.RestoreLatestBackupAsync();
        Assert.True(restoreResult.Success);
        var restored = await File.ReadAllTextAsync(configFile);
        Assert.Contains("controller_1=VKB_Gladiator", restored);
    }

    [Fact]
    public async Task ScriptableGamePlugin_ExecutesJavaScriptAndLifecycle() {
        var jsDir = Path.Combine(_tempDir, "js_test_game");
        Directory.CreateDirectory(jsDir);
        var configFile = Path.Combine(jsDir, "binds.txt");
        await File.WriteAllTextAsync(configFile, "slot1=stickA;slot2=stickB;");

        var jsCode = $@"
        ({{
            id: 'jsgame',
            name: 'JavaScript Flight Sim',
            description: 'A JS powered plugin',
            processName: 'JsFlightSim.exe',
            isInstalled: function() {{
                return sticklock.fs.dirExists('{jsDir.Replace("\\", "\\\\")}');
            }},
            detectActiveBindingsPath: function() {{
                return '{configFile.Replace("\\", "\\\\")}';
            }},
            checkAlignment: function(context) {{
                return {{
                    status: 'Aligned',
                    statusMessage: 'JS checked: all controllers aligned'
                }};
            }},
            sync: function(context) {{
                sticklock.log.info('Running JS sync for ' + context.activePath);
                return {{
                    success: true,
                    message: 'Custom JS sync executed perfectly'
                }};
            }}
        }})
        ";

        var configService = new AppConfigService(_configPath);
        var diService = new TestDirectInputService();
        var logService = new ActivityLogService();

        var plugin = new StickLock.Plugins.Scripting.ScriptableGamePlugin("test.js", jsCode, configService, diService, logService);

        Assert.Equal("jsgame", plugin.Id);
        Assert.Equal("JavaScript Flight Sim", plugin.Name);
        Assert.True(await plugin.IsInstalledAsync());

        var detected = await plugin.DetectActiveBindingsPathAsync();
        Assert.Equal(configFile, detected);

        var check = await plugin.CheckAlignmentAsync();
        Assert.Equal(SyncStatus.Aligned, check.Status);
        Assert.Contains("JS checked", check.StatusMessage);

        var syncResult = await plugin.SyncAsync(force: true);
        Assert.True(syncResult.Success);
        Assert.Contains("Custom JS sync executed perfectly", syncResult.Message);
        Assert.NotNull(syncResult.BackupPath);
        Assert.True(File.Exists(syncResult.BackupPath));
    }

    [Fact]
    public async Task GamePluginLoader_DiscoversDeclarativeJsonAndScriptableJsPlugins() {
        var userPluginsDir = Path.Combine(_tempDir, "loader_test_plugins");
        Directory.CreateDirectory(userPluginsDir);

        // 1. Create a JSON plugin
        var jsonPluginDir = Path.Combine(userPluginsDir, "MyJsonGame");
        Directory.CreateDirectory(jsonPluginDir);
        var jsonPath = Path.Combine(jsonPluginDir, "game.json");
        await File.WriteAllTextAsync(jsonPath, @"{
            ""id"": ""json-game"",
            ""name"": ""JSON Game"",
            ""description"": ""Declarative test game"",
            ""processName"": ""JsonGame.exe"",
            ""configDirectory"": ""%USERPROFILE%""
        }");

        // 2. Create a JS plugin
        var jsPluginDir = Path.Combine(userPluginsDir, "MyJsGame");
        Directory.CreateDirectory(jsPluginDir);
        var jsPath = Path.Combine(jsPluginDir, "plugin.js");
        await File.WriteAllTextAsync(jsPath, @"({
            id: 'js-game',
            name: 'JS Game',
            description: 'JS test game',
            processName: 'JsGame.exe'
        })");

        var configService = new AppConfigService(Path.Combine(userPluginsDir, "..", "config.json"));
        // Override user plugins directory by creating a custom loader or passing config
        var logService = new ActivityLogService();
        var diService = new TestDirectInputService();

        // Create loader pointing to configService
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSingleton<IDirectInputService>(diService);
        var sp = services.BuildServiceProvider();

        // Test loader with userPluginsDir
        var loader = new GamePluginLoader(configService, logService);
        // Copy files into loader.UserPluginsDirectory so loader discovers them
        var destJson = Path.Combine(loader.UserPluginsDirectory, "game.json");
        var destJs = Path.Combine(loader.UserPluginsDirectory, "plugin.js");
        File.Copy(jsonPath, destJson, true);
        File.Copy(jsPath, destJs, true);

        var loaded = loader.LoadExternalPlugins(sp);
        Assert.Contains(loaded, p => p.Id == "json-game");
        Assert.Contains(loaded, p => p.Id == "js-game");
    }

    public void Dispose() {
        try {
            if (Directory.Exists(_tempDir)) {
                Directory.Delete(_tempDir, true);
            }
        } catch { }
    }
}

