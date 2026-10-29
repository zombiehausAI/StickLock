using StickLock.Models;

namespace StickLock.Services;

public interface IAppConfigService {
    AppConfig Config { get; }
    event Action? ConfigChanged;
    Task LoadConfigAsync();
    Task SaveConfigAsync();
    Task SetRunOnStartupAsync(bool enable);
    Task SetAutoSyncAsync(bool enable);
    Task SetStartMinimizedAsync(bool enable);
    Task SetStarCitizenPathAsync(string path);
    Task SwitchEnvironmentAsync(ScEnvironment env);
    Task SetCanonicalStickAsync(int slot, DirectInputDevice device, string friendlyName);
    Task RemoveCanonicalStickAsync(int slot);
    string ResolveActionMapsPath(string currentPath, ScEnvironment targetEnv);
    bool TryAutoDetectStarCitizenPath(out string detectedPath);
}

