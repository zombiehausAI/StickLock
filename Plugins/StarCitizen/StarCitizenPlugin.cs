using StickLock.Models;
using StickLock.Services;

namespace StickLock.Plugins.StarCitizen;

public class StarCitizenPlugin : IGamePlugin {
    private readonly IActionMapsXmlService _xmlService;
    private readonly IAppConfigService _configService;
    private readonly IStarCitizenDetector _gameDetector;

    public string Id => "starcitizen";
    public string Name => "Star Citizen";
    public string Description => "DirectInput controller slot synchronization, actionmaps.xml alignment, and in-game console resort for Star Citizen.";
    public string ProcessName => "StarCitizen.exe";
    public string IconSvg => "M12 2L4.5 20.29l.71.71L12 18l6.79 3 .71-.71z"; // Spaceship rocket icon
    public bool SupportsFlightAxisMode => true;

    public StarCitizenPlugin(
        IActionMapsXmlService xmlService,
        IAppConfigService configService,
        IStarCitizenDetector gameDetector) {
        _xmlService = xmlService;
        _configService = configService;
        _gameDetector = gameDetector;
    }

    public Task<bool> IsInstalledAsync() {
        var path = _configService.Config.StarCitizenPath;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) {
            return Task.FromResult(true);
        }
        var root = _configService.Config.StarCitizenRootPath;
        if (!string.IsNullOrWhiteSpace(root) && Directory.Exists(root)) {
            return Task.FromResult(true);
        }
        return Task.FromResult(_configService.TryAutoDetectStarCitizenPath(out _));
    }

    public Task<string?> DetectActiveBindingsPathAsync() {
        var path = _configService.Config.StarCitizenPath;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) {
            return Task.FromResult<string?>(path);
        }
        if (_configService.TryAutoDetectStarCitizenPath(out var detected)) {
            return Task.FromResult<string?>(detected);
        }
        return Task.FromResult<string?>(null);
    }

    public Task<MappingAlignmentCheck> CheckAlignmentAsync() {
        return _xmlService.CheckAlignmentAsync();
    }

    public Task<SyncResult> SyncAsync(bool force = false) {
        return _xmlService.SyncAsync(force);
    }

    public Task<SyncResult> FixAndSyncAsync(bool force = false) {
        return _xmlService.FixAndSyncAsync(force);
    }

    public Task<IReadOnlyList<string>> GetAvailableBackupsAsync() {
        return Task.FromResult(_xmlService.GetAvailableBackups());
    }

    public Task<SyncResult> RestoreLatestBackupAsync() {
        return _xmlService.RestoreLatestBackupAsync();
    }
}
