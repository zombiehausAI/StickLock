using StickLock.Services;

namespace StickLock.Plugins;

public class GamePluginManager : IGamePluginManager {
    private readonly IReadOnlyList<IGamePlugin> _plugins;
    private readonly IAppConfigService _configService;
    private IGamePlugin _activePlugin;

    public IReadOnlyList<IGamePlugin> AvailablePlugins => _plugins;
    public IGamePlugin ActivePlugin => _activePlugin;
    public event Action<IGamePlugin>? ActivePluginChanged;

    public GamePluginManager(IEnumerable<IGamePlugin> plugins, IAppConfigService configService) {
        _plugins = plugins.ToList();
        _configService = configService;

        var savedId = _configService.Config.ActiveGamePluginId;
        _activePlugin = _plugins.FirstOrDefault(p => string.Equals(p.Id, savedId, StringComparison.OrdinalIgnoreCase))
            ?? _plugins.FirstOrDefault()
            ?? throw new InvalidOperationException("No game plugins registered.");
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
