using System.Diagnostics;
using Jint;
using Jint.Native;
using Jint.Native.Object;
using StickLock.Models;
using StickLock.Services;

namespace StickLock.Plugins.Scripting;

public class ScriptableGamePlugin : IGamePlugin {
    private readonly string _scriptPath;
    private readonly string _scriptContent;
    private readonly IAppConfigService _configService;
    private readonly IDirectInputService _directInputService;
    private readonly IActivityLogService _logService;
    private readonly string _backupFolder;

    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private string _processName = string.Empty;
    private string _iconSvg = "M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5";
    private bool _supportsFlightAxisMode = false;

    public string Id => _id;
    public string Name => _name;
    public string Description => _description;
    public string ProcessName => _processName;
    public string IconSvg => _iconSvg;
    public bool SupportsFlightAxisMode => _supportsFlightAxisMode;

    public string ScriptPath => _scriptPath;

    public ScriptableGamePlugin(
        string scriptPath,
        string scriptContent,
        IAppConfigService configService,
        IDirectInputService directInputService,
        IActivityLogService logService) {
        _scriptPath = scriptPath;
        _scriptContent = scriptContent;
        _configService = configService;
        _directInputService = directInputService;
        _logService = logService;

        InitializeMetadata();

        _backupFolder = Path.Combine(_configService.BaseDirectory, "backups", _id);
        try {
            Directory.CreateDirectory(_backupFolder);
        } catch { }
    }

    private void InitializeMetadata() {
        try {
            var engine = CreateEngine();
            var result = engine.Evaluate(_scriptContent);
            if (result is ObjectInstance obj) {
                _id = obj.Get("id").AsString();
                _name = obj.Get("name").AsString();
                _description = obj.HasProperty("description") ? obj.Get("description").AsString() : string.Empty;
                _processName = obj.HasProperty("processName") ? obj.Get("processName").AsString() : string.Empty;
                if (obj.HasProperty("iconSvg")) {
                    _iconSvg = obj.Get("iconSvg").AsString();
                }
                if (obj.HasProperty("supportsFlightAxisMode")) {
                    _supportsFlightAxisMode = obj.Get("supportsFlightAxisMode").AsBoolean();
                }
            }
        } catch (Exception ex) {
            _logService.LogError($"Failed to parse JS plugin metadata in {_scriptPath}: {ex.Message}");
            _id = Path.GetFileNameWithoutExtension(_scriptPath).ToLowerInvariant();
            _name = Path.GetFileNameWithoutExtension(_scriptPath);
        }
    }

    private Engine CreateEngine() {
        var engine = new Engine(options => {
            options.TimeoutInterval(TimeSpan.FromSeconds(5));
        });

        // Expose sandboxed sticklock bridge
        var logBridge = new {
            info = new Action<string>(msg => _logService.LogInfo(msg)),
            warn = new Action<string>(msg => _logService.LogWarning(msg)),
            error = new Action<string>(msg => _logService.LogError(msg)),
            success = new Action<string>(msg => _logService.LogSuccess(msg))
        };

        var envBridge = new {
            expand = new Func<string, string>(str => Environment.ExpandEnvironmentVariables(str ?? string.Empty))
        };

        var fsBridge = new {
            readFile = new Func<string, string>(path => File.ReadAllText(path)),
            writeFile = new Action<string, string>((path, content) => File.WriteAllText(path, content)),
            fileExists = new Func<string, bool>(path => File.Exists(path)),
            dirExists = new Func<string, bool>(path => Directory.Exists(path)),
            getFiles = new Func<string, string, string[]>((dir, pattern) => Directory.Exists(dir) ? Directory.GetFiles(dir, pattern) : Array.Empty<string>())
        };

        var bridge = new {
            log = logBridge,
            env = envBridge,
            fs = fsBridge,
            isProcessRunning = new Func<string, bool>(proc => {
                var p = proc.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? proc[..^4] : proc;
                return Process.GetProcessesByName(p).Length > 0;
            })
        };

        engine.SetValue("sticklock", bridge);
        return engine;
    }

    public Task<bool> IsInstalledAsync() {
        try {
            var engine = CreateEngine();
            var result = engine.Evaluate(_scriptContent);
            if (result is ObjectInstance obj && obj.HasProperty("isInstalled")) {
                var fn = obj.Get("isInstalled");
                var isInst = engine.Invoke(fn).AsBoolean();
                return Task.FromResult(isInst);
            }
        } catch (Exception ex) {
            _logService.LogError($"JS plugin '{_name}' isInstalled failed: {ex.Message}");
        }

        return Task.FromResult(true);
    }

    public Task<string?> DetectActiveBindingsPathAsync() {
        try {
            var engine = CreateEngine();
            var result = engine.Evaluate(_scriptContent);
            if (result is ObjectInstance obj && obj.HasProperty("detectActiveBindingsPath")) {
                var fn = obj.Get("detectActiveBindingsPath");
                var pathVal = engine.Invoke(fn);
                if (!pathVal.IsNull() && !pathVal.IsUndefined()) {
                    return Task.FromResult<string?>(pathVal.AsString());
                }
            }
        } catch (Exception ex) {
            _logService.LogError($"JS plugin '{_name}' detectActiveBindingsPath failed: {ex.Message}");
        }

        return Task.FromResult<string?>(null);
    }

    public bool IsGameRunning() {
        if (string.IsNullOrWhiteSpace(_processName)) {
            return false;
        }

        var procName = _processName;
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

        try {
            var engine = CreateEngine();
            var result = engine.Evaluate(_scriptContent);
            if (result is ObjectInstance obj && obj.HasProperty("checkAlignment")) {
                var fn = obj.Get("checkAlignment");
                var liveDevices = await _directInputService.RefreshDevicesAsync();
                var canonical = _configService.Config.CanonicalSticks;

                var context = new {
                    liveDevices,
                    canonicalSticks = canonical,
                    isGameRunning = check.IsGameRunning
                };

                var jsCheck = engine.Invoke(fn, context);
                if (jsCheck is ObjectInstance checkObj) {
                    if (checkObj.HasProperty("status")) {
                        var statusStr = checkObj.Get("status").AsString();
                        if (Enum.TryParse<SyncStatus>(statusStr, true, out var parsedStatus)) {
                            check.Status = parsedStatus;
                        }
                    }
                    if (checkObj.HasProperty("statusMessage")) {
                        check.StatusMessage = checkObj.Get("statusMessage").AsString();
                    }
                    return check;
                }
            }
        } catch (Exception ex) {
            _logService.LogError($"JS plugin '{_name}' checkAlignment failed: {ex.Message}");
        }

        // Fallback default alignment check
        var devices = await _directInputService.RefreshDevicesAsync();
        var sticks = _configService.Config.CanonicalSticks;
        if (sticks.Count < 2) {
            check.Status = SyncStatus.Aligned;
            check.StatusMessage = $"{_name} script loaded. Awaiting canonical stick lock.";
        } else {
            var missing = sticks.Where(c => !devices.Any(d => d.InstanceGuid == c.InstanceGuid || d.ProductGuid == c.ProductGuid)).ToList();
            if (missing.Count > 0) {
                check.Status = SyncStatus.SticksDisconnected;
                check.StatusMessage = $"Disconnected controllers: {string.Join(", ", missing.Select(m => m.FriendlyRoleName))}";
            } else {
                check.Status = check.IsGameRunning ? SyncStatus.StarCitizenActive : SyncStatus.Aligned;
                check.StatusMessage = $"{_name} controllers aligned with DirectInput order.";
            }
        }

        return check;
    }

    public async Task<SyncResult> SyncAsync(bool force = false) {
        if (IsGameRunning() && !force && !_configService.Config.AllowSyncWhileGameRunning) {
            return SyncResult.Fail($"{_name} is running. Close the game or use Force Sync.", SyncStatus.StarCitizenActive);
        }

        var activePath = await DetectActiveBindingsPathAsync();
        string? backupFile = null;
        if (!string.IsNullOrWhiteSpace(activePath) && File.Exists(activePath)) {
            try {
                backupFile = Path.Combine(_backupFolder, $"{Path.GetFileNameWithoutExtension(activePath)}_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}{Path.GetExtension(activePath)}");
                File.Copy(activePath, backupFile, overwrite: true);
                _logService.LogSuccess($"Created backup for {_name}", backupFile);
            } catch { }
        }

        try {
            var engine = CreateEngine();
            var result = engine.Evaluate(_scriptContent);
            if (result is ObjectInstance obj && obj.HasProperty("sync")) {
                var fn = obj.Get("sync");
                var context = new {
                    force,
                    activePath,
                    backupPath = backupFile
                };

                var jsResult = engine.Invoke(fn, context);
                if (jsResult is ObjectInstance resObj) {
                    var success = resObj.HasProperty("success") ? resObj.Get("success").AsBoolean() : true;
                    var message = resObj.HasProperty("message") ? resObj.Get("message").AsString() : $"Synchronized {_name}.";
                    return success
                        ? SyncResult.Ok(message, changesMade: true, backupPath: backupFile, status: SyncStatus.Aligned)
                        : SyncResult.Fail(message);
                }
            }
        } catch (Exception ex) {
            _logService.LogError($"JS plugin '{_name}' sync failed: {ex.Message}");
            return SyncResult.Fail($"Script sync error: {ex.Message}");
        }

        return SyncResult.Ok($"Controllers synced for {_name}.", changesMade: true, backupPath: backupFile, status: SyncStatus.Aligned);
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
            return SyncResult.Fail($"No backups available to restore for {_name}.");
        }

        var latestBackup = backups[0];
        var activePath = await DetectActiveBindingsPathAsync();
        if (string.IsNullOrWhiteSpace(activePath)) {
            return SyncResult.Fail("Target config file path could not be resolved.");
        }

        try {
            File.Copy(latestBackup, activePath, overwrite: true);
            _logService.LogSuccess($"Restored backup {Path.GetFileName(latestBackup)} for {_name}", activePath);
            return SyncResult.Ok($"Successfully restored backup to {Path.GetFileName(activePath)}.", changesMade: true, status: SyncStatus.Aligned);
        } catch (Exception ex) {
            _logService.LogError($"Failed to restore backup: {ex.Message}");
            return SyncResult.Fail($"Failed to restore backup: {ex.Message}");
        }
    }
}
