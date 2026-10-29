using StickLock.Models;

namespace StickLock.Services;

public interface IUsbWatcherService {
    event Action<IReadOnlyList<DirectInputDevice>>? UsbDevicesChanged;
    void Start();
    void Stop();
    Task TriggerManualScanAsync();
}

