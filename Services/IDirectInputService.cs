using StickLock.Models;

namespace StickLock.Services;

public interface IDirectInputService {
    IReadOnlyList<DirectInputDevice> CurrentDevices { get; }
    event Action<IReadOnlyList<DirectInputDevice>>? DevicesRefreshed;
    IReadOnlyList<DirectInputDevice> RefreshDevices();
    Task<IReadOnlyList<DirectInputDevice>> RefreshDevicesAsync();
}
