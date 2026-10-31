using System.Diagnostics;
using StickLock.Models;

namespace StickLock.Plugins;

/// <summary>
/// Convenient abstract base class for out-of-tree StickLock game plugins.
/// Provides automated timestamped backups, safe file updates, and process checks.
/// </summary>
public abstract class GamePluginBase : IGamePlugin {
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string ProcessName { get; }
    public virtual string IconSvg => "M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5";
    public virtual bool SupportsFlightAxisMode => false;

    /// <summary>
    /// Base directory inside ~/.sticklock/backups/{Id}/ where backups are archived.
    /// </summary>
    protected virtual string BackupDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".sticklock", "backups", Id);

    public abstract Task<bool> IsInstalledAsync();
    public abstract Task<string?> DetectActiveBindingsPathAsync();
    public abstract Task<MappingAlignmentCheck> CheckAlignmentAsync();
    public abstract Task<SyncResult> SyncAsync(bool force = false);

    public virtual Task<SyncResult> FixAndSyncAsync(bool force = false) {
        return SyncAsync(force);
    }

    /// <summary>
    /// Checks whether the game's executable is currently running.
    /// </summary>
    public virtual bool IsGameRunning() {
        if (string.IsNullOrWhiteSpace(ProcessName)) {
            return false;
        }

        var procNameWithoutExt = Path.GetFileNameWithoutExtension(ProcessName);
        try {
            return Process.GetProcessesByName(procNameWithoutExt).Length > 0;
        } catch {
            return false;
        }
    }

    /// <summary>
    /// Creates a timestamped backup copy of the specified file in the plugin's backup directory.
    /// </summary>
    protected virtual string CreateBackup(string sourceFilePath) {
        if (!File.Exists(sourceFilePath)) {
            throw new FileNotFoundException("Cannot backup non-existent file", sourceFilePath);
        }

        Directory.CreateDirectory(BackupDirectory);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var ext = Path.GetExtension(sourceFilePath);
        var baseName = Path.GetFileNameWithoutExtension(sourceFilePath);
        var backupPath = Path.Combine(BackupDirectory, $"{baseName}_backup_{timestamp}{ext}");

        File.Copy(sourceFilePath, backupPath, overwrite: true);
        return backupPath;
    }

    public virtual Task<IReadOnlyList<string>> GetAvailableBackupsAsync() {
        if (!Directory.Exists(BackupDirectory)) {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var files = Directory.GetFiles(BackupDirectory)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    public virtual async Task<SyncResult> RestoreLatestBackupAsync() {
        var backups = await GetAvailableBackupsAsync();
        if (backups.Count == 0) {
            return SyncResult.Fail($"No backups found for {Name}.");
        }

        var latest = backups[0];
        var activePath = await DetectActiveBindingsPathAsync();
        if (string.IsNullOrWhiteSpace(activePath)) {
            return SyncResult.Fail($"Active configuration path not found for {Name}.");
        }

        try {
            File.Copy(latest, activePath, overwrite: true);
            return SyncResult.Ok($"Restored {Name} bindings from backup.", changesMade: true, backupPath: latest, status: SyncStatus.Aligned);
        } catch (Exception ex) {
            return SyncResult.Fail($"Failed to restore backup: {ex.Message}");
        }
    }
}

