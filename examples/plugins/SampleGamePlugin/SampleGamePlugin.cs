using StickLock.Models;
using StickLock.Plugins;

namespace StickLock.Plugins.Sample;

/// <summary>
/// Reference out-of-tree plugin demonstrating how third-party developers can implement
/// custom game support for StickLock without needing access to the main StickLock source tree.
/// </summary>
public class SampleFlightSimPlugin : GamePluginBase {
    public override string Id => "sampleflightsim";
    public override string Name => "Sample Flight Simulator";
    public override string Description => "Reference out-of-tree plugin demonstrating DirectInput slot management.";
    public override string ProcessName => "FlightSim.exe";
    public override bool SupportsFlightAxisMode => false;

    private readonly string _mockConfigDirectory;

    public SampleFlightSimPlugin() {
        _mockConfigDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SampleFlightSim", "Config");
    }

    public override Task<bool> IsInstalledAsync() {
        // Return true if the game's configuration folder or registry keys exist
        var exists = Directory.Exists(_mockConfigDirectory);
        return Task.FromResult(exists);
    }

    public override Task<string?> DetectActiveBindingsPathAsync() {
        var configPath = Path.Combine(_mockConfigDirectory, "devices.cfg");
        if (File.Exists(configPath)) {
            return Task.FromResult<string?>(configPath);
        }
        return Task.FromResult<string?>(null);
    }

    public override async Task<MappingAlignmentCheck> CheckAlignmentAsync() {
        var path = await DetectActiveBindingsPathAsync();
        if (path == null) {
            return new MappingAlignmentCheck {
                IsMisaligned = false,
                Status = SyncStatus.FileNotFound,
                StatusMessage = "devices.cfg not found"
            };
        }

        if (IsGameRunning()) {
            return new MappingAlignmentCheck {
                IsMisaligned = false,
                IsGameRunning = true,
                Status = SyncStatus.StarCitizenActive,
                StatusMessage = "Game is currently running"
            };
        }

        // Example: parse file and check alignment against connected DirectInput devices
        return new MappingAlignmentCheck {
            IsMisaligned = false,
            Status = SyncStatus.Aligned,
            StatusMessage = "Sample devices are properly aligned"
        };
    }

    public override async Task<SyncResult> SyncAsync(bool force = false) {
        var path = await DetectActiveBindingsPathAsync();
        if (path == null) {
            return SyncResult.Fail("Configuration file not found.");
        }

        if (IsGameRunning()) {
            return SyncResult.Fail("Cannot sync while game is running.");
        }

        // Create timestamped backup using helper from GamePluginBase
        var backupPath = CreateBackup(path);

        // Perform file modifications here...
        // For example: re-order joystick entries to match DirectInput sequence.

        return SyncResult.Ok("Device configuration successfully aligned.", changesMade: true, backupPath: backupPath, status: SyncStatus.Aligned);
    }
}
