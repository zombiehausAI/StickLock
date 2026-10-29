using StickLock.Models;

namespace StickLock.Services;

public interface IAppConfigService {
    AppConfig Config { get; }
    string BaseDirectory { get; }
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
    Task ExportConfigAsync(string destinationPath);
    Task ImportConfigAsync(string sourcePath);
    Task<string> BackupCurrentSettingsAsync();
    IReadOnlyList<string> GetSettingsBackups();
    Task RestoreSettingsBackupAsync(string backupFilePath);
}
