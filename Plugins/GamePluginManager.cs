using StickLock.Services;

namespace StickLock.Plugins;

public class GamePluginManager : IGamePluginManager {
    private readonly List<IGamePlugin> _plugins = new();
    private readonly IEnumerable<IGamePlugin> _nativePlugins;
    private readonly GamePluginLoader? _pluginLoader;
    private readonly IServiceProvider? _serviceProvider;
    private readonly IAppConfigService _configService;
    private IGamePlugin _activePlugin;

    public IReadOnlyList<IGamePlugin> AvailablePlugins => _plugins;
    public IGamePlugin ActivePlugin => _activePlugin;
    public string UserPluginsDirectory => _pluginLoader?.UserPluginsDirectory
        ?? Path.Combine(_configService.BaseDirectory, "plugins");
    public event Action<IGamePlugin>? ActivePluginChanged;

    public GamePluginManager(
        IEnumerable<IGamePlugin> nativePlugins,
        GamePluginLoader pluginLoader,
        IServiceProvider serviceProvider,
        IAppConfigService configService)
        : this(nativePlugins, configService, pluginLoader, serviceProvider) {
    }

    public GamePluginManager(
        IEnumerable<IGamePlugin> nativePlugins,
        IAppConfigService configService,
        GamePluginLoader? pluginLoader = null,
        IServiceProvider? serviceProvider = null) {
        _nativePlugins = nativePlugins;
        _pluginLoader = pluginLoader;
        _serviceProvider = serviceProvider;
        _configService = configService;

        LoadAllPlugins();

        var savedId = _configService.Config.ActiveGamePluginId;
        _activePlugin = _plugins.FirstOrDefault(p => string.Equals(p.Id, savedId, StringComparison.OrdinalIgnoreCase))
            ?? _plugins.FirstOrDefault()
            ?? throw new InvalidOperationException("No game plugins available.");
    }

    private void LoadAllPlugins() {
        _plugins.Clear();
        _plugins.AddRange(_nativePlugins);

        if (_pluginLoader != null && _serviceProvider != null) {
            var external = _pluginLoader.LoadExternalPlugins(_serviceProvider);
            foreach (var ext in external) {
                if (!_plugins.Any(p => string.Equals(p.Id, ext.Id, StringComparison.OrdinalIgnoreCase))) {
                    _plugins.Add(ext);
                }
            }
        }
    }

    public Task ReloadPluginsAsync() {
        LoadAllPlugins();
        var currentId = _activePlugin.Id;
        _activePlugin = _plugins.FirstOrDefault(p => string.Equals(p.Id, currentId, StringComparison.OrdinalIgnoreCase))
            ?? _plugins.FirstOrDefault()
            ?? throw new InvalidOperationException("No game plugins available.");
        ActivePluginChanged?.Invoke(_activePlugin);
        return Task.CompletedTask;
    }

    public async Task SetActivePluginAsync(string pluginId) {
        var match = GetPlugin(pluginId);
        if (match != null && match.Id != _activePlugin.Id) {
            _activePlugin = match;
            await _configService.SetActiveGamePluginIdAsync(match.Id);
            ActivePluginChanged?.Invoke(match);
        }
    }

    public IGamePlugin? GetPlugin(string pluginId) {
        return _plugins.FirstOrDefault(p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
    }
}

