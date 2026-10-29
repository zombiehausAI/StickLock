using System.Text.Json;
using Microsoft.Win32;
using StickLock.Models;

namespace StickLock.Services;

public class AppConfigService : IAppConfigService {
    private const string StartupRegistryKeyName = "StickLock";
    private const string RunRegistrySubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly string _configFilePath;
    private AppConfig _config = new();

    public AppConfig Config => _config;
    public event Action? ConfigChanged;

    public AppConfigService() {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localAppData, "StickLock");
        Directory.CreateDirectory(appFolder);
        _configFilePath = Path.Combine(appFolder, "config.json");
    }

    public async Task LoadConfigAsync() {
        if (!File.Exists(_configFilePath)) {
            // Check BaseDirectory fallback
            var baseDirConfig = Path.Combine(AppContext.BaseDirectory, "config.json");
            if (File.Exists(baseDirConfig)) {
                try {
                    var legacyJson = await File.ReadAllTextAsync(baseDirConfig);
                    _config = JsonSerializer.Deserialize<AppConfig>(legacyJson) ?? new AppConfig();
                    await SaveConfigAsync();
                    ConfigChanged?.Invoke();
                    return;
                } catch {
                    // Ignore and create fresh
                }
            }

            _config = new AppConfig();
            if (TryAutoDetectStarCitizenPath(out var detectedPath)) {
                _config.StarCitizenPath = detectedPath;
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

        if (string.IsNullOrWhiteSpace(_config.StarCitizenPath) && TryAutoDetectStarCitizenPath(out var autoPath)) {
            _config.StarCitizenPath = autoPath;
            await SaveConfigAsync();
        }

        ConfigChanged?.Invoke();
    }

    public async Task SaveConfigAsync() {
        var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_configFilePath, json);
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

    public async Task SetStartMinimizedAsync(bool enable) {
        _config.StartMinimizedToTray = enable;
        await SaveConfigAsync();
    }

    public async Task SetStarCitizenPathAsync(string path) {
        _config.StarCitizenPath = path.Trim();
        await SaveConfigAsync();
    }

    public async Task SwitchEnvironmentAsync(ScEnvironment env) {
        _config.SelectedEnvironment = env;
        if (!string.IsNullOrWhiteSpace(_config.StarCitizenPath)) {
            _config.StarCitizenPath = ResolveActionMapsPath(_config.StarCitizenPath, env);
        }
        await SaveConfigAsync();
    }

    public async Task SetCanonicalStickAsync(int slot, DirectInputDevice device, string friendlyName) {
        _config.CanonicalSticks.RemoveAll(s => s.RoleSlot == slot);
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
        var knownEnvs = new[] { "LIVE", "PTU", "EPTU", "TECHPREVIEW", "HOTFIX" };

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

    public bool TryAutoDetectStarCitizenPath(out string detectedPath) {
        detectedPath = string.Empty;
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

