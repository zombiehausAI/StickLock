using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using StickLock.Plugins.Declarative;
using StickLock.Plugins.Scripting;
using StickLock.Services;

namespace StickLock.Plugins;

public class GamePluginLoader {
    private readonly IAppConfigService _configService;
    private readonly IActivityLogService _logService;
    private readonly string _userPluginsDir;
    private readonly string _appPluginsDir;

    public string UserPluginsDirectory => _userPluginsDir;

    public GamePluginLoader(IAppConfigService configService, IActivityLogService logService) {
        _configService = configService;
        _logService = logService;
        _userPluginsDir = Path.Combine(configService.BaseDirectory, "plugins");
        _appPluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");

        try {
            Directory.CreateDirectory(_userPluginsDir);
        } catch { }
    }

    /// <summary>
    /// Scans ~/.sticklock/plugins/ and application plugins folder for external out-of-tree plugins (.dll, .json, .js).
    /// </summary>
    public IReadOnlyList<IGamePlugin> LoadExternalPlugins(IServiceProvider serviceProvider) {
        var loaded = new List<IGamePlugin>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var searchDirs = new[] { _userPluginsDir, _appPluginsDir };

        var diService = serviceProvider.GetService<IDirectInputService>() ?? new DirectInputService();

        foreach (var dir in searchDirs) {
            if (!Directory.Exists(dir)) {
                continue;
            }

            // 1. Discover C# SDK assemblies (*.dll)
            LoadDllPlugins(dir, serviceProvider, loaded, seenIds);

            // 2. Discover Declarative JSON plugins (game.json, manifest.json, *.plugin.json)
            LoadDeclarativeJsonPlugins(dir, diService, loaded, seenIds);

            // 3. Discover Scriptable JavaScript plugins (plugin.js, game.js, *.plugin.js)
            LoadScriptableJsPlugins(dir, diService, loaded, seenIds);
        }

        return loaded;
    }

    private void LoadDllPlugins(string dir, IServiceProvider serviceProvider, List<IGamePlugin> loaded, HashSet<string> seenIds) {
        var dllFiles = Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories);
        foreach (var dll in dllFiles) {
            var fileName = Path.GetFileName(dll);
            // Skip core assemblies
            if (fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("Vortice.", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("MudBlazor", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("Jint", StringComparison.OrdinalIgnoreCase) ||
                fileName.StartsWith("Acornima", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("StickLock.SDK.dll", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("StickLock.dll", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            try {
                var loadContext = new AssemblyLoadContext(fileName, isCollectible: true);
                var asm = loadContext.LoadFromAssemblyPath(dll);
                var pluginTypes = asm.GetTypes()
                    .Where(t => typeof(IGamePlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .ToList();

                foreach (var type in pluginTypes) {
                    try {
                        IGamePlugin? plugin = null;
                        try {
                            plugin = ActivatorUtilities.CreateInstance(serviceProvider, type) as IGamePlugin;
                        } catch {
                            plugin = Activator.CreateInstance(type) as IGamePlugin;
                        }

                        if (plugin != null && !seenIds.Contains(plugin.Id)) {
                            loaded.Add(plugin);
                            seenIds.Add(plugin.Id);
                            _logService.LogSuccess($"Loaded C# plugin: {plugin.Name} (v{asm.GetName().Version})", dll);
                        }
                    } catch (Exception ex) {
                        _logService.LogError($"Failed to activate plugin {type.FullName} from {fileName}: {ex.Message}");
                    }
                }
            } catch (Exception ex) {
                _logService.LogWarning($"Skipped loading assembly {fileName}: {ex.Message}");
            }
        }
    }

    private void LoadDeclarativeJsonPlugins(string dir, IDirectInputService diService, List<IGamePlugin> loaded, HashSet<string> seenIds) {
        var jsonFiles = Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories);
        foreach (var file in jsonFiles) {
            var name = Path.GetFileName(file);
            if (!name.Equals("game.json", StringComparison.OrdinalIgnoreCase) &&
                !name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) &&
                !name.EndsWith(".plugin.json", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            try {
                var json = File.ReadAllText(file);
                var manifest = JsonSerializer.Deserialize<DeclarativePluginManifest>(json, new JsonSerializerOptions {
                    PropertyNameCaseInsensitive = true
                });

                if (manifest != null && !string.IsNullOrWhiteSpace(manifest.Id) && !string.IsNullOrWhiteSpace(manifest.Name)) {
                    if (seenIds.Contains(manifest.Id)) {
                        continue;
                    }

                    var plugin = new DeclarativeGamePlugin(manifest, _configService, diService, _logService);
                    loaded.Add(plugin);
                    seenIds.Add(manifest.Id);
                    _logService.LogSuccess($"Loaded Declarative JSON plugin: {plugin.Name} [{plugin.Id}]", file);
                }
            } catch (Exception ex) {
                _logService.LogError($"Failed to load JSON plugin from {file}: {ex.Message}");
            }
        }
    }

    private void LoadScriptableJsPlugins(string dir, IDirectInputService diService, List<IGamePlugin> loaded, HashSet<string> seenIds) {
        var jsFiles = Directory.GetFiles(dir, "*.js", SearchOption.AllDirectories);
        foreach (var file in jsFiles) {
            var name = Path.GetFileName(file);
            if (!name.Equals("plugin.js", StringComparison.OrdinalIgnoreCase) &&
                !name.Equals("game.js", StringComparison.OrdinalIgnoreCase) &&
                !name.EndsWith(".plugin.js", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            try {
                var scriptContent = File.ReadAllText(file);
                var plugin = new ScriptableGamePlugin(file, scriptContent, _configService, diService, _logService);

                if (!string.IsNullOrWhiteSpace(plugin.Id) && !string.IsNullOrWhiteSpace(plugin.Name)) {
                    if (seenIds.Contains(plugin.Id)) {
                        continue;
                    }

                    loaded.Add(plugin);
                    seenIds.Add(plugin.Id);
                    _logService.LogSuccess($"Loaded JavaScript plugin: {plugin.Name} [{plugin.Id}]", file);
                }
            } catch (Exception ex) {
                _logService.LogError($"Failed to load JS plugin from {file}: {ex.Message}");
            }
        }
    }
}

