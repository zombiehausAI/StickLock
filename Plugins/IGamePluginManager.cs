namespace StickLock.Plugins;

public interface IGamePluginManager {
    IReadOnlyList<IGamePlugin> AvailablePlugins { get; }
    IGamePlugin ActivePlugin { get; }
    event Action<IGamePlugin>? ActivePluginChanged;

    Task SetActivePluginAsync(string pluginId);
    IGamePlugin? GetPlugin(string pluginId);
}
