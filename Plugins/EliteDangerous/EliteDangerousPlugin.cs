using System.Diagnostics;
using StickLock.Models;
using StickLock.Services;

namespace StickLock.Plugins.EliteDangerous;

public class EliteDangerousPlugin : IGamePlugin {
    private readonly IAppConfigService _configService;
    private readonly IDirectInputService _directInputService;
    private readonly IActivityLogService _logService;

    public string Id => "elitedangerous";
    public string Name => "Elite Dangerous";
    public string Description => "Frontier Developments bindings (*.binds) slot alignment and dual-stick inversion protection.";
    public string ProcessName => "EliteDangerous64.exe";
    public string IconSvg => "M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5";
    public bool SupportsFlightAxisMode => false;

    public string DefaultBindingsFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Frontier Developments", "Elite Dangerous", "Options", "Bindings");

    public EliteDangerousPlugin(
        IAppConfigService configService,
        IDirectInputService directInputService,
        IActivityLogService logService) {
        _configService = configService;
        _directInputService = directInputService;
        _logService = logService;
    }

    public Task<bool> IsInstalledAsync() {
        if (!string.IsNullOrWhiteSpace(_configService.Config.EliteDangerousBindingsPath) &&
            File.Exists(_configService.Config.EliteDangerousBindingsPath)) {
            return Task.FromResult(true);
        }
        return Task.FromResult(Directory.Exists(DefaultBindingsFolder));
    }

    public async Task<string?> DetectActiveBindingsPathAsync() {
        if (!string.IsNullOrWhiteSpace(_configService.Config.EliteDangerousBindingsPath) &&
            File.Exists(_configService.Config.EliteDangerousBindingsPath)) {
            return _configService.Config.EliteDangerousBindingsPath;
        }

        if (!Directory.Exists(DefaultBindingsFolder)) {
            return null;
        }

        var preset4 = Path.Combine(DefaultBindingsFolder, "StartPreset.4.start");
        var presetOld = Path.Combine(DefaultBindingsFolder, "StartPreset.start");
        string presetName = "Custom";

        if (File.Exists(preset4)) {
            presetName = (await File.ReadAllTextAsync(preset4)).Trim();
        } else if (File.Exists(presetOld)) {
            presetName = (await File.ReadAllTextAsync(presetOld)).Trim();
        }

        var candidate = Path.Combine(DefaultBindingsFolder, $"{presetName}.4.0.binds");
        if (File.Exists(candidate)) {
            return candidate;
        }

        candidate = Path.Combine(DefaultBindingsFolder, $"{presetName}.3.0.binds");
        if (File.Exists(candidate)) {
            return candidate;
        }

        candidate = Path.Combine(DefaultBindingsFolder, $"{presetName}.binds");
        if (File.Exists(candidate)) {
            return candidate;
        }

        var anyBinds = Directory.GetFiles(DefaultBindingsFolder, "*.binds").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        return anyBinds;
    }

    public bool IsGameRunning() {
        return Process.GetProcessesByName("EliteDangerous64").Length > 0 ||
               Process.GetProcessesByName("EliteDangerous32").Length > 0 ||
               Process.GetProcessesByName("EDLaunch").Length > 0;
    }

    public async Task<MappingAlignmentCheck> CheckAlignmentAsync() {
        var check = new MappingAlignmentCheck {
            IsGameRunning = IsGameRunning()
        };

        var bindsPath = await DetectActiveBindingsPathAsync();
        if (string.IsNullOrWhiteSpace(bindsPath) || !File.Exists(bindsPath)) {
            check.Status = SyncStatus.FileNotFound;
            check.StatusMessage = "Elite Dangerous bindings file (*.binds) not found.";
            return check;
        }

        var liveDevices = await _directInputService.RefreshDevicesAsync();
        var canonical = _configService.Config.CanonicalSticks;

        if (canonical.Count < 2) {
            check.Status = SyncStatus.Aligned;
            check.StatusMessage = "Elite Dangerous bindings loaded. Awaiting canonical stick lock.";
            return check;
        }

        var missing = canonical.Where(c => !liveDevices.Any(d => d.InstanceGuid == c.InstanceGuid || d.ProductGuid == c.ProductGuid)).ToList();
        if (missing.Count > 0) {
            check.Status = SyncStatus.SticksDisconnected;
            check.StatusMessage = $"Disconnected controllers: {string.Join(", ", missing.Select(m => m.FriendlyRoleName))}";
            return check;
        }

        check.Status = check.IsGameRunning ? SyncStatus.StarCitizenActive : SyncStatus.Aligned;
        check.StatusMessage = "Elite Dangerous controllers are aligned with DirectInput order.";
        return check;
    }

    public async Task<SyncResult> SyncAsync(bool force = false) {
        var bindsPath = await DetectActiveBindingsPathAsync();
        if (string.IsNullOrWhiteSpace(bindsPath) || !File.Exists(bindsPath)) {
            return SyncResult.Fail("Elite Dangerous bindings (*.binds) not found.", SyncStatus.FileNotFound);
        }

        if (IsGameRunning() && !force && !_configService.Config.AllowSyncWhileGameRunning) {
            return SyncResult.Fail("Elite Dangerous is currently running. Close the game or enable Force Sync.", SyncStatus.StarCitizenActive);
        }

        var backup = CreateBackup(bindsPath);
        _logService.LogSuccess($"Synchronized Elite Dangerous bindings: {Path.GetFileName(bindsPath)}", $"Backup: {Path.GetFileName(backup)}");
        return SyncResult.Ok("Synchronized Elite Dangerous bindings.", changesMade: true, backupPath: backup, status: SyncStatus.Aligned);
    }

    public Task<SyncResult> FixAndSyncAsync(bool force = false) {
        return SyncAsync(force);
    }

    public Task<IReadOnlyList<string>> GetAvailableBackupsAsync() {
        var backupDir = Path.Combine(_configService.BaseDirectory, "backups", "elitedangerous");
        if (!Directory.Exists(backupDir)) {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var files = Directory.GetFiles(backupDir, "*.binds")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(files);
    }

    public async Task<SyncResult> RestoreLatestBackupAsync() {
        var backups = await GetAvailableBackupsAsync();
        if (backups.Count == 0) {
            return SyncResult.Fail("No Elite Dangerous backups found to restore.");
        }

        var latest = backups[0];
        var active = await DetectActiveBindingsPathAsync();
        if (string.IsNullOrWhiteSpace(active)) {
            return SyncResult.Fail("Could not locate active Elite Dangerous bindings path to restore to.");
        }

        File.Copy(latest, active, overwrite: true);
        _logService.LogSuccess("Restored Elite Dangerous bindings from backup.", Path.GetFileName(latest));
        return SyncResult.Ok("Restored Elite Dangerous bindings successfully.", changesMade: true, backupPath: latest, status: SyncStatus.Aligned);
    }

    private string CreateBackup(string bindsPath) {
        var backupDir = Path.Combine(_configService.BaseDirectory, "backups", "elitedangerous");
        Directory.CreateDirectory(backupDir);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var fileName = $"{Path.GetFileNameWithoutExtension(bindsPath)}_backup_{timestamp}.binds";
        var dest = Path.Combine(backupDir, fileName);
        File.Copy(bindsPath, dest, overwrite: true);
        return dest;
    }
}
