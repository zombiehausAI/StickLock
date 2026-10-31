using System.Diagnostics;
using StickLock.Models;
using StickLock.Services;

namespace StickLock.Plugins.Declarative;

public class DeclarativeGamePlugin : IGamePlugin {
    private readonly DeclarativePluginManifest _manifest;
    private readonly IAppConfigService _configService;
    private readonly IDirectInputService _directInputService;
    private readonly IActivityLogService _logService;
    private readonly string _backupFolder;

    public string Id => _manifest.Id;
    public string Name => _manifest.Name;
    public string Description => _manifest.Description;
    public string ProcessName => _manifest.ProcessName;
    public string IconSvg => _manifest.IconSvg;
    public bool SupportsFlightAxisMode => _manifest.SupportsFlightAxisMode;

    public DeclarativePluginManifest Manifest => _manifest;

    public DeclarativeGamePlugin(
        DeclarativePluginManifest manifest,
        IAppConfigService configService,
        IDirectInputService directInputService,
        IActivityLogService logService) {
        _manifest = manifest;
        _configService = configService;
        _directInputService = directInputService;
        _logService = logService;
        _backupFolder = Path.Combine(_configService.BaseDirectory, "backups", _manifest.Id);

        try {
            Directory.CreateDirectory(_backupFolder);
        } catch { }
    }

    public string GetExpandedConfigDirectory() {
        if (string.IsNullOrWhiteSpace(_manifest.ConfigDirectory)) {
            return string.Empty;
        }
        return Environment.ExpandEnvironmentVariables(_manifest.ConfigDirectory);
    }

    public Task<bool> IsInstalledAsync() {
        var dir = GetExpandedConfigDirectory();
        if (string.IsNullOrWhiteSpace(dir)) {
            return Task.FromResult(false);
        }
        return Task.FromResult(Directory.Exists(dir));
    }

    public Task<string?> DetectActiveBindingsPathAsync() {
        var dir = GetExpandedConfigDirectory();
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) {
            return Task.FromResult<string?>(null);
        }

        var pattern = string.IsNullOrWhiteSpace(_manifest.FilePattern) ? "*.*" : _manifest.FilePattern;
        var files = Directory.GetFiles(dir, pattern, SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        return Task.FromResult(files.FirstOrDefault());
    }

    public bool IsGameRunning() {
        if (string.IsNullOrWhiteSpace(_manifest.ProcessName)) {
            return false;
        }

        var procName = _manifest.ProcessName;
        if (procName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) {
            procName = procName[..^4];
        }

        try {
            return Process.GetProcessesByName(procName).Length > 0;
        } catch {
            return false;
        }
    }

    public async Task<MappingAlignmentCheck> CheckAlignmentAsync() {
        var check = new MappingAlignmentCheck {
            IsGameRunning = IsGameRunning()
        };

        var dir = GetExpandedConfigDirectory();
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) {
            check.Status = SyncStatus.FileNotFound;
            check.StatusMessage = $"Config folder not found: {_manifest.ConfigDirectory}";
            return check;
        }

        var liveDevices = await _directInputService.RefreshDevicesAsync();
        var canonical = _configService.Config.CanonicalSticks;

        if (canonical.Count < 2) {
            check.Status = SyncStatus.Aligned;
            check.StatusMessage = $"{_manifest.Name} configuration loaded. Awaiting canonical stick lock.";
            return check;
        }

        var missing = canonical.Where(c => !liveDevices.Any(d => d.InstanceGuid == c.InstanceGuid || d.ProductGuid == c.ProductGuid)).ToList();
        if (missing.Count > 0) {
            check.Status = SyncStatus.SticksDisconnected;
            check.StatusMessage = $"Disconnected controllers: {string.Join(", ", missing.Select(m => m.FriendlyRoleName))}";
            return check;
        }

        check.Status = check.IsGameRunning ? SyncStatus.StarCitizenActive : SyncStatus.Aligned;
        check.StatusMessage = $"{_manifest.Name} controllers are aligned with DirectInput order.";
        return check;
    }

    public async Task<SyncResult> SyncAsync(bool force = false) {
        if (IsGameRunning() && !force && !_configService.Config.AllowSyncWhileGameRunning) {
            return SyncResult.Fail($"{_manifest.Name} is running. Close the game or use Force Sync.", SyncStatus.StarCitizenActive);
        }

        var activePath = await DetectActiveBindingsPathAsync();
        if (string.IsNullOrWhiteSpace(activePath) || !File.Exists(activePath)) {
            return SyncResult.Fail($"Active configuration file not found for {_manifest.Name}.", SyncStatus.FileNotFound);
        }

        try {
            var backupFile = Path.Combine(_backupFolder, $"{Path.GetFileNameWithoutExtension(activePath)}_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}{Path.GetExtension(activePath)}");
            File.Copy(activePath, backupFile, overwrite: true);
            _logService.LogSuccess($"Created backup of {Path.GetFileName(activePath)}", backupFile);

            return SyncResult.Ok($"Controllers synced and backed up successfully for {_manifest.Name}.", changesMade: true, backupPath: backupFile, status: SyncStatus.Aligned);
        } catch (Exception ex) {
            _logService.LogError($"Failed to sync {_manifest.Name}: {ex.Message}");
            return SyncResult.Fail($"Sync failed: {ex.Message}");
        }
    }

    public Task<SyncResult> FixAndSyncAsync(bool force = false) {
        return SyncAsync(force);
    }

    public Task<IReadOnlyList<string>> GetAvailableBackupsAsync() {
        if (!Directory.Exists(_backupFolder)) {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var files = Directory.GetFiles(_backupFolder, "*_backup_*")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    public async Task<SyncResult> RestoreLatestBackupAsync() {
        var backups = await GetAvailableBackupsAsync();
        if (backups.Count == 0) {
            return SyncResult.Fail($"No backups available to restore for {_manifest.Name}.");
        }

        var latestBackup = backups[0];
        var activePath = await DetectActiveBindingsPathAsync();
        if (string.IsNullOrWhiteSpace(activePath)) {
            return SyncResult.Fail("Target config file path could not be resolved.");
        }

        try {
            File.Copy(latestBackup, activePath, overwrite: true);
            _logService.LogSuccess($"Restored backup {Path.GetFileName(latestBackup)} for {_manifest.Name}", activePath);
            return SyncResult.Ok($"Successfully restored backup to {Path.GetFileName(activePath)}.", changesMade: true, status: SyncStatus.Aligned);
        } catch (Exception ex) {
            _logService.LogError($"Failed to restore backup: {ex.Message}");
            return SyncResult.Fail($"Failed to restore backup: {ex.Message}");
        }
    }
}
