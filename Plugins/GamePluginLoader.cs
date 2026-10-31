using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;
using StickLock.Services;

namespace StickLock.Plugins;

public class GamePluginLoader {
    private readonly IActivityLogService _logService;
    private readonly string _userPluginsDir;
    private readonly string _appPluginsDir;

    public string UserPluginsDirectory => _userPluginsDir;

    public GamePluginLoader(IAppConfigService configService, IActivityLogService logService) {
        _logService = logService;
        _userPluginsDir = Path.Combine(configService.BaseDirectory, "plugins");
        _appPluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");

        try {
            Directory.CreateDirectory(_userPluginsDir);
        } catch { }
    }

    /// <summary>
    /// Scans ~/.sticklock/plugins/ and application plugins folder for external out-of-tree plugins.
    /// </summary>
    public IReadOnlyList<IGamePlugin> LoadExternalPlugins(IServiceProvider serviceProvider) {
        var loaded = new List<IGamePlugin>();
        var searchDirs = new[] { _userPluginsDir, _appPluginsDir };

        foreach (var dir in searchDirs) {
            if (!Directory.Exists(dir)) {
                continue;
            }

            var dllFiles = Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories);
            foreach (var dll in dllFiles) {
                var fileName = Path.GetFileName(dll);
                // Skip core assemblies
                if (fileName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
                    fileName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
                    fileName.StartsWith("Vortice.", StringComparison.OrdinalIgnoreCase) ||
                    fileName.StartsWith("MudBlazor", StringComparison.OrdinalIgnoreCase) ||
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

                            if (plugin != null) {
                                loaded.Add(plugin);
                                _logService.LogSuccess($"Loaded external plugin: {plugin.Name} (v{asm.GetName().Version})", dll);
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

        return loaded;
    }
}

