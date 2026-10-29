using System.Text.Json;
using Microsoft.Win32;
using StickLock.Models;

namespace StickLock.Services;

public class AppConfigService : IAppConfigService {
    private const string StartupRegistryKeyName = "StickLock";
    private const string RunRegistrySubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly string _baseDirectory;
    private readonly string _configFilePath;
    private readonly string _mappingsDirectory;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private AppConfig _config = new();

    public AppConfig Config => _config;
    public string BaseDirectory => _baseDirectory;
    public string MappingsDirectory => _mappingsDirectory;
    public event Action? ConfigChanged;

    public AppConfigService(string? customConfigFilePath = null) {
        if (!string.IsNullOrWhiteSpace(customConfigFilePath)) {
            _configFilePath = customConfigFilePath;
            _baseDirectory = Path.GetDirectoryName(_configFilePath)
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sticklock");
        } else {
            var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _baseDirectory = Path.Combine(userHome, ".sticklock");
            _configFilePath = Path.Combine(_baseDirectory, "config.json");
        }

        _mappingsDirectory = Path.Combine(_baseDirectory, "mappings");
        Directory.CreateDirectory(_baseDirectory);
        Directory.CreateDirectory(_mappingsDirectory);
        Directory.CreateDirectory(Path.Combine(_baseDirectory, "settings_backups"));
        Directory.CreateDirectory(Path.Combine(_baseDirectory, "backups"));
        Directory.CreateDirectory(Path.Combine(_baseDirectory, "exports"));
    }

    public async Task LoadConfigAsync() {
        if (!File.Exists(_configFilePath)) {
            // Check legacy LocalAppData or BaseDirectory fallback for smooth migration
            var legacyLocal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StickLock", "config.json");
            var baseDirConfig = Path.Combine(AppContext.BaseDirectory, "config.json");
            var migrationSource = File.Exists(legacyLocal) ? legacyLocal : (File.Exists(baseDirConfig) ? baseDirConfig : null);

            if (migrationSource != null) {
                try {
                    var legacyJson = await File.ReadAllTextAsync(migrationSource);
                    _config = JsonSerializer.Deserialize<AppConfig>(legacyJson) ?? new AppConfig();
                    await SaveConfigAsync();
                    ConfigChanged?.Invoke();
                    return;
                } catch {
                    // Ignore and create fresh
                }
            }

            _config = new AppConfig();
            if (TryAutoDetectStarCitizenRoot(out var detectedRoot)) {
                _config.StarCitizenRootPath = detectedRoot;
            }
            if (TryAutoDetectStarCitizenPath(out var detectedPath)) {
                _config.StarCitizenPath = detectedPath;
                if (string.IsNullOrWhiteSpace(_config.StarCitizenRootPath)) {
                    _config.StarCitizenRootPath = ExtractStarCitizenRoot(detectedPath);
                }
            }
            await SaveConfigAsync();
            ConfigChanged?.Invoke();
            return;
        }

        try {
            var json = await File.ReadAllTextAsync(_configFilePath);
            var loaded = JsonSerializer.Deserialize<AppConfig>(json);
            if (loaded != null) {
                _config = loaded;
            }
        } catch {
            _config = new AppConfig();
        }

        var changed = false;
        var purgedBogusSticks = _config.CanonicalSticks.RemoveAll(s =>
            s.InstanceGuid == Guid.Empty &&
            s.ProductGuid == Guid.Empty &&
            (string.IsNullOrWhiteSpace(s.ProductName) || s.ProductName.StartsWith("Joystick ", StringComparison.OrdinalIgnoreCase)));
        if (purgedBogusSticks > 0) {
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(_config.StarCitizenPath) && TryAutoDetectStarCitizenPath(out var autoPath)) {
            _config.StarCitizenPath = autoPath;
            changed = true;
        }
        if (string.IsNullOrWhiteSpace(_config.StarCitizenRootPath)) {
            if (!string.IsNullOrWhiteSpace(_config.StarCitizenPath)) {
                var inferredRoot = ExtractStarCitizenRoot(_config.StarCitizenPath);
                if (!string.IsNullOrWhiteSpace(inferredRoot)) {
                    _config.StarCitizenRootPath = inferredRoot;
                    changed = true;
                }
            } else if (TryAutoDetectStarCitizenRoot(out var autoRoot)) {
                _config.StarCitizenRootPath = autoRoot;
                changed = true;
            }
        }
        if (changed) {
            await SaveConfigAsync();
        }

        ConfigChanged?.Invoke();
    }

    public async Task SaveConfigAsync() {
        await _fileLock.WaitAsync();
        try {
            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configFilePath, json);
        } finally {
            _fileLock.Release();
        }
        ConfigChanged?.Invoke();
    }

    public async Task SetRunOnStartupAsync(bool enable) {
        _config.RunOnWindowsStartup = enable;
        ApplyStartupRegistry(enable);
        await SaveConfigAsync();
    }

    public async Task SetAutoSyncAsync(bool enable) {
        _config.AutoSyncOnUsbChange = enable;
        await SaveConfigAsync();
    }

    public async Task SetSyncAllDetectedInstallsAsync(bool enable) {
        _config.SyncAllDetectedInstalls = enable;
        await SaveConfigAsync();
    }

    public async Task SetStartMinimizedAsync(bool enable) {
        _config.StartMinimizedToTray = enable;
        await SaveConfigAsync();
    }

    public async Task SetAllowSyncWhileGameRunningAsync(bool enable) {
        _config.AllowSyncWhileGameRunning = enable;
        await SaveConfigAsync();
    }

    public async Task SetAutoDeployToMappingsFolderAsync(bool enable) {
        _config.AutoDeployToMappingsFolder = enable;
        await SaveConfigAsync();
    }

    public async Task SetFlightAxisModeAsync(FlightAxisMode mode) {
        _config.FlightAxisMode = mode;
        await SaveConfigAsync();
    }

    public async Task SetAutoEnsureFlightAxesAsync(bool enable) {
        _config.AutoEnsureFlightAxes = enable;
        await SaveConfigAsync();
    }

    public async Task SetStarCitizenPathAsync(string path) {
        _config.StarCitizenPath = path.Trim();
        var inferredRoot = ExtractStarCitizenRoot(_config.StarCitizenPath);
        if (!string.IsNullOrWhiteSpace(inferredRoot)) {
            _config.StarCitizenRootPath = inferredRoot;
        }
        await SaveConfigAsync();
    }

    public async Task SetStarCitizenRootFolderAsync(string folderPath) {
        var root = ExtractStarCitizenRoot(folderPath.Trim());
        _config.StarCitizenRootPath = !string.IsNullOrWhiteSpace(root) ? root : folderPath.Trim();

        var installs = GetDetectedInstalls();
        var preferred = installs.FirstOrDefault(i => i.Environment == _config.SelectedEnvironment && i.HasActionMapsXml)
                        ?? installs.FirstOrDefault(i => i.HasActionMapsXml)
                        ?? installs.FirstOrDefault(i => i.IsDirectoryPresent)
                        ?? installs.FirstOrDefault();

        if (preferred != null) {
            _config.SelectedEnvironment = preferred.Environment;
            _config.StarCitizenPath = preferred.ActionMapsPath;
        }

        await SaveConfigAsync();
    }

    public async Task SwitchEnvironmentAsync(ScEnvironment env) {
        _config.SelectedEnvironment = env;
        var installs = GetDetectedInstalls();
        var match = installs.FirstOrDefault(i => i.Environment == env);
        if (match != null && !string.IsNullOrWhiteSpace(match.ActionMapsPath) && !string.IsNullOrWhiteSpace(_config.StarCitizenRootPath)) {
            _config.StarCitizenPath = match.ActionMapsPath;
        } else if (!string.IsNullOrWhiteSpace(_config.StarCitizenPath)) {
            _config.StarCitizenPath = ResolveActionMapsPath(_config.StarCitizenPath, env);
        }
        await SaveConfigAsync();
    }

    public IReadOnlyList<DetectedScInstall> GetDetectedInstalls() {
        var root = _config.StarCitizenRootPath;
        if (string.IsNullOrWhiteSpace(root) && !string.IsNullOrWhiteSpace(_config.StarCitizenPath)) {
            root = ExtractStarCitizenRoot(_config.StarCitizenPath);
        }

        var results = new List<DetectedScInstall>();
        var channels = new (ScEnvironment Env, string DisplayName, string[] FolderNames)[] {
            (ScEnvironment.LIVE, "LIVE", new[] { "LIVE" }),
            (ScEnvironment.PTU, "PTU", new[] { "PTU" }),
            (ScEnvironment.EPTU, "EPTU", new[] { "EPTU" }),
            (ScEnvironment.TechPreview, "TECH-PREVIEW", new[] { "TECH-PREVIEW", "TECHPREVIEW" })
        };

        foreach (var (env, displayName, folderNames) in channels) {
            var resolvedFolder = folderNames[0];
            var channelDir = string.Empty;
            var actionMapsPath = string.Empty;
            var mappingsDir = string.Empty;
            var dirPresent = false;
            var xmlPresent = false;

            if (!string.IsNullOrWhiteSpace(root)) {
                foreach (var candidateFolder in folderNames) {
                    var candidateDir = Path.Combine(root, candidateFolder);
                    if (Directory.Exists(candidateDir)) {
                        resolvedFolder = candidateFolder;
                        break;
                    }
                }

                channelDir = Path.Combine(root, resolvedFolder);
                actionMapsPath = Path.Combine(channelDir, "USER", "Client", "0", "Profiles", "default", "actionmaps.xml");
                mappingsDir = Path.Combine(channelDir, "USER", "Client", "0", "Controls", "Mappings");
                dirPresent = Directory.Exists(channelDir);
                xmlPresent = File.Exists(actionMapsPath);
            } else if (!string.IsNullOrWhiteSpace(_config.StarCitizenPath) && _config.SelectedEnvironment == env) {
                actionMapsPath = _config.StarCitizenPath;
                xmlPresent = File.Exists(actionMapsPath);
                dirPresent = xmlPresent;
            }

            results.Add(new DetectedScInstall {
                Environment = env,
                ChannelName = displayName,
                ChannelDirectory = channelDir,
                ActionMapsPath = actionMapsPath,
                MappingsDirectory = mappingsDir,
                IsDirectoryPresent = dirPresent,
                HasActionMapsXml = xmlPresent
            });
        }

        return results;
    }

    public static string ExtractStarCitizenRoot(string path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return string.Empty;
        }

        var trimmed = path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var knownChannels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "LIVE", "PTU", "EPTU", "TECH-PREVIEW", "TECHPREVIEW", "HOTFIX"
        };

        // Check if path points directly to "Roberts Space Industries" parent containing "StarCitizen"
        var scChild = Path.Combine(trimmed, "StarCitizen");
        if (Directory.Exists(scChild)) {
            return scChild;
        }

        // Check if path itself has LIVE/PTU/EPTU/TECH-PREVIEW subdirectories or is named StarCitizen
        foreach (var ch in knownChannels) {
            if (Directory.Exists(Path.Combine(trimmed, ch))) {
                return trimmed;
            }
        }

        // Walk up directory segments to see if an ancestor is a known channel folder (LIVE, PTU, EPTU, TECH-PREVIEW)
        var current = trimmed;
        while (!string.IsNullOrWhiteSpace(current)) {
            var name = Path.GetFileName(current);
            var parent = Path.GetDirectoryName(current);
            if (!string.IsNullOrWhiteSpace(name) && knownChannels.Contains(name) && !string.IsNullOrWhiteSpace(parent)) {
                return parent;
            }
            if (string.Equals(name, "StarCitizen", StringComparison.OrdinalIgnoreCase)) {
                return current;
            }
            current = parent;
        }

        return string.Empty;
    }

    public async Task SetCanonicalStickAsync(int slot, DirectInputDevice device, string friendlyName) {
        _config.CanonicalSticks.RemoveAll(s =>
            s.RoleSlot == slot ||
            (device.InstanceGuid != Guid.Empty && s.InstanceGuid == device.InstanceGuid) ||
            (device.ProductGuid != Guid.Empty && s.ProductGuid == device.ProductGuid));
        _config.CanonicalSticks.Add(new CanonicalStick {
            RoleSlot = slot,
            FriendlyRoleName = friendlyName,
            InstanceGuid = device.InstanceGuid,
            ProductGuid = device.ProductGuid,
            ProductName = device.ProductName
        });
        _config.CanonicalSticks.Sort((a, b) => a.RoleSlot.CompareTo(b.RoleSlot));
        await SaveConfigAsync();
    }

    public async Task RemoveCanonicalStickAsync(int slot) {
        var removed = _config.CanonicalSticks.RemoveAll(s => s.RoleSlot == slot);
        if (removed > 0) {
            await SaveConfigAsync();
        }
    }

    public string ResolveActionMapsPath(string currentPath, ScEnvironment targetEnv) {
        if (string.IsNullOrWhiteSpace(currentPath)) {
            return string.Empty;
        }

        if (targetEnv == ScEnvironment.Custom) {
            return currentPath;
        }

        var targetEnvName = targetEnv.ToString().ToUpperInvariant();
        var knownEnvs = new[] { "LIVE", "PTU", "EPTU", "TECH-PREVIEW", "TECHPREVIEW", "HOTFIX" };

        foreach (var env in knownEnvs) {
            var token = Path.DirectorySeparatorChar + env + Path.DirectorySeparatorChar;
            if (currentPath.Contains(token, StringComparison.OrdinalIgnoreCase)) {
                return currentPath.Replace(token, Path.DirectorySeparatorChar + targetEnvName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
            }

            var altToken = "/" + env + "/";
            if (currentPath.Contains(altToken, StringComparison.OrdinalIgnoreCase)) {
                return currentPath.Replace(altToken, "/" + targetEnvName + "/", StringComparison.OrdinalIgnoreCase);
            }
        }

        return currentPath;
    }

    public bool TryAutoDetectStarCitizenRoot(out string detectedRoot) {
        detectedRoot = string.Empty;
        var driveLetters = DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .Select(d => d.Name.TrimEnd('\\', '/', ':'))
            .Union(new[] { "C", "D", "E", "F", "G" }, StringComparer.OrdinalIgnoreCase);

        var candidateSubDirs = new[] {
            @"Program Files\Roberts Space Industries\StarCitizen",
            @"Roberts Space Industries\StarCitizen",
            @"Games\Roberts Space Industries\StarCitizen",
            @"Games\StarCitizen",
            @"StarCitizen"
        };

        foreach (var drive in driveLetters) {
            foreach (var sub in candidateSubDirs) {
                var full = $@"{drive}:\{sub}";
                if (Directory.Exists(full)) {
                    detectedRoot = full;
                    return true;
                }
            }
        }

        return false;
    }

    public bool TryAutoDetectStarCitizenPath(out string detectedPath) {
        detectedPath = string.Empty;
        if (TryAutoDetectStarCitizenRoot(out var root)) {
            var channels = new[] { "LIVE", "PTU", "EPTU", "TECH-PREVIEW", "TECHPREVIEW", "HOTFIX" };
            foreach (var ch in channels) {
                var xml = Path.Combine(root, ch, "USER", "Client", "0", "Profiles", "default", "actionmaps.xml");
                if (File.Exists(xml)) {
                    detectedPath = xml;
                    return true;
                }
            }
        }

        var driveLetters = new[] { "C", "D", "E", "F", "G" };
        var candidateSubPaths = new[] {
            @"Program Files\Roberts Space Industries\StarCitizen\LIVE\USER\Client\0\Profiles\default\actionmaps.xml",
            @"Roberts Space Industries\StarCitizen\LIVE\USER\Client\0\Profiles\default\actionmaps.xml",
            @"Games\Roberts Space Industries\StarCitizen\LIVE\USER\Client\0\Profiles\default\actionmaps.xml",
            @"StarCitizen\LIVE\USER\Client\0\Profiles\default\actionmaps.xml"
        };

        foreach (var drive in driveLetters) {
            foreach (var sub in candidateSubPaths) {
                var full = $@"{drive}:\{sub}";
                if (File.Exists(full)) {
                    detectedPath = full;
                    return true;
                }
            }
        }

        return false;
    }

    public async Task ExportConfigAsync(string destinationPath) {
        var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(destinationPath, json);
    }

    public async Task ImportConfigAsync(string sourcePath) {
        if (!File.Exists(sourcePath)) {
            throw new FileNotFoundException("Configuration file not found.", sourcePath);
        }

        var json = await File.ReadAllTextAsync(sourcePath);
        var imported = JsonSerializer.Deserialize<AppConfig>(json);
        if (imported == null) {
            throw new InvalidOperationException("Failed to parse configuration file.");
        }

        _config = imported;
        ApplyStartupRegistry(_config.RunOnWindowsStartup);
        await SaveConfigAsync();
    }

    public async Task<string> BackupCurrentSettingsAsync() {
        var backupDir = Path.Combine(_baseDirectory, "settings_backups");
        Directory.CreateDirectory(backupDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupFile = Path.Combine(backupDir, $"settings_backup_{timestamp}.json");

        var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(backupFile, json);

        // Prune old settings backups
        var maxBackups = Math.Max(1, _config.MaxBackupsToKeep);
        var existing = Directory.GetFiles(backupDir, "settings_backup_*.json")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .ToList();

        if (existing.Count > maxBackups) {
            foreach (var old in existing.Skip(maxBackups)) {
                try {
                    old.Delete();
                } catch { }
            }
        }

        return backupFile;
    }

    public IReadOnlyList<string> GetSettingsBackups() {
        var backupDir = Path.Combine(_baseDirectory, "settings_backups");
        if (!Directory.Exists(backupDir)) {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(backupDir, "settings_backup_*.json")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .Select(f => f.FullName)
            .ToList();
    }

    public async Task RestoreSettingsBackupAsync(string backupFilePath) {
        await ImportConfigAsync(backupFilePath);
    }

    private void ApplyStartupRegistry(bool enable) {
        try {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistrySubKey, true);
            if (key == null) {
                return;
            }

            if (enable) {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath)) {
                    key.SetValue(StartupRegistryKeyName, $"\"{exePath}\" --minimized");
                }
            } else {
                key.DeleteValue(StartupRegistryKeyName, false);
            }
        } catch {
            // Registry access error handled gracefully
        }
    }
}
