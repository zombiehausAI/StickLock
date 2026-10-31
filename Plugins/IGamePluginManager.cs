namespace StickLock.Plugins;

public interface IGamePluginManager {
    IReadOnlyList<IGamePlugin> AvailablePlugins { get; }
    IGamePlugin ActivePlugin { get; }
    string UserPluginsDirectory { get; }
    event Action<IGamePlugin>? ActivePluginChanged;

    Task SetActivePluginAsync(string pluginId);
    Task ReloadPluginsAsync();
    IGamePlugin? GetPlugin(string pluginId);
}

