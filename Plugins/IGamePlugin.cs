using StickLock.Models;

namespace StickLock.Plugins;

public interface IGamePlugin {
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string ProcessName { get; }
    string IconSvg { get; }
    bool SupportsFlightAxisMode { get; }

    Task<bool> IsInstalledAsync();
    Task<string?> DetectActiveBindingsPathAsync();
    Task<MappingAlignmentCheck> CheckAlignmentAsync();
    Task<SyncResult> SyncAsync(bool force = false);
    Task<SyncResult> FixAndSyncAsync(bool force = false);
    Task<IReadOnlyList<string>> GetAvailableBackupsAsync();
    Task<SyncResult> RestoreLatestBackupAsync();
}
