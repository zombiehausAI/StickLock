using StickLock.Models;

namespace StickLock.Services;

public interface IAppConfigService {
    AppConfig Config { get; }
    string BaseDirectory { get; }
    string MappingsDirectory { get; }
    event Action? ConfigChanged;
    Task LoadConfigAsync();
    Task SaveConfigAsync();
    Task SetRunOnStartupAsync(bool enable);
    Task SetAutoSyncAsync(bool enable);
    Task SetStartMinimizedAsync(bool enable);
    Task SetAllowSyncWhileGameRunningAsync(bool enable);
    Task SetAutoDeployToMappingsFolderAsync(bool enable);
    Task SetFlightAxisModeAsync(FlightAxisMode mode);
    Task SetAutoEnsureFlightAxesAsync(bool enable);
    Task SetSyncAllDetectedInstallsAsync(bool enable);
    Task SetStarCitizenPathAsync(string path);
    Task SetStarCitizenRootFolderAsync(string folderPath);
    Task SwitchEnvironmentAsync(ScEnvironment env);
    IReadOnlyList<DetectedScInstall> GetDetectedInstalls();
    Task SetCanonicalStickAsync(int slot, DirectInputDevice device, string friendlyName);
    Task RemoveCanonicalStickAsync(int slot);
    string ResolveActionMapsPath(string currentPath, ScEnvironment targetEnv);
    bool TryAutoDetectStarCitizenRoot(out string detectedRoot);
    bool TryAutoDetectStarCitizenPath(out string detectedPath);
    Task ExportConfigAsync(string destinationPath);
    Task ImportConfigAsync(string sourcePath);
    Task<string> BackupCurrentSettingsAsync();
    IReadOnlyList<string> GetSettingsBackups();
    Task RestoreSettingsBackupAsync(string backupFilePath);
}
