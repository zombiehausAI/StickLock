using System.Management;
using StickLock.Models;

namespace StickLock.Services;

public class UsbWatcherService : IUsbWatcherService, IDisposable {
    private readonly IDirectInputService _directInputService;
    private readonly IActionMapsXmlService _actionMapsXmlService;
    private readonly IStarCitizenDetector _starCitizenDetector;
    private readonly IAppConfigService _configService;
    private readonly IActivityLogService _logService;

    private ManagementEventWatcher? _watcher;
    private System.Threading.Timer? _debounceTimer;
    private readonly object _timerLock = new();
    private const int DebounceMs = 1500;
    private bool _isDisposed;

    public event Action<IReadOnlyList<DirectInputDevice>>? UsbDevicesChanged;

    public UsbWatcherService(
        IDirectInputService directInputService,
        IActionMapsXmlService actionMapsXmlService,
        IStarCitizenDetector starCitizenDetector,
        IAppConfigService configService,
        IActivityLogService logService) {
        _directInputService = directInputService;
        _actionMapsXmlService = actionMapsXmlService;
        _starCitizenDetector = starCitizenDetector;
        _configService = configService;
        _logService = logService;
    }

    public void Start() {
        if (_watcher != null) {
            return;
        }

        try {
            var query = new WqlEventQuery("SELECT * FROM Win32_DeviceChangeEvent");
            _watcher = new ManagementEventWatcher(query);
            _watcher.EventArrived += OnDeviceChangeEventArrived;
            _watcher.Start();
            _logService.LogInfo("USB hardware watcher initialized and monitoring.");
        } catch (Exception ex) {
            _logService.LogWarning($"Could not attach WMI USB watcher: {ex.Message}. Manual sync still available.");
        }
    }

    public void Stop() {
        if (_watcher == null) {
            return;
        }

        try {
            _watcher.Stop();
            _watcher.EventArrived -= OnDeviceChangeEventArrived;
            _watcher.Dispose();
            _watcher = null;
        } catch {
            // Ignore teardown errors
        }
    }

    public async Task TriggerManualScanAsync() {
        await ExecuteScanAndSyncAsync();
    }

    private void OnDeviceChangeEventArrived(object sender, EventArrivedEventArgs e) {
        lock (_timerLock) {
            _debounceTimer?.Dispose();
            _debounceTimer = new System.Threading.Timer(async _ => {
                await ExecuteScanAndSyncAsync();
            }, null, DebounceMs, Timeout.Infinite);
        }
    }

    private async Task ExecuteScanAndSyncAsync() {
        if (_isDisposed) {
            return;
        }

        _logService.LogInfo("USB device change detected. Re-enumerating DirectInput controllers...");
        var liveDevices = _directInputService.RefreshDevices();
        UsbDevicesChanged?.Invoke(liveDevices);

        if (!_configService.Config.AutoSyncOnUsbChange) {
            _logService.LogInfo("Auto-sync on USB change is disabled in settings.");
            return;
        }

        if (_starCitizenDetector.IsGameRunning()) {
            _logService.LogWarning(
                "USB change detected while Star Citizen is running. Auto-sync skipped.",
                "Use in-game console command 'pp_resortdevices' or restart game to apply changes.");
            return;
        }

        _logService.LogInfo("Evaluating actionmaps.xml alignment...");
        var syncResult = await _actionMapsXmlService.SyncAsync();
        if (syncResult.ChangesMade) {
            _logService.LogSuccess("Auto-sync completed successfully.", syncResult.Message);
        }
    }

    public void Dispose() {
        _isDisposed = true;
        Stop();
        lock (_timerLock) {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }
    }
}
