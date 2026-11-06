using StickLock.Models;

namespace StickLock.Services;

public interface IDirectInputService {
    IReadOnlyList<DirectInputDevice> CurrentDevices { get; }
    event Action<IReadOnlyList<DirectInputDevice>>? DevicesRefreshed;
    event Action<int, string>? ButtonInputDetected;
    IReadOnlyList<DirectInputDevice> RefreshDevices();
    Task<IReadOnlyList<DirectInputDevice>> RefreshDevicesAsync();
    void StartInputMonitoring(int slotIndex);
    void StopInputMonitoring();
}
