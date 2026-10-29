using StickLock.Models;
using StickLock.Services;

namespace StickLock.Tests;

public class TestDirectInputService : IDirectInputService {
    private List<DirectInputDevice> _devices = new();

    public IReadOnlyList<DirectInputDevice> CurrentDevices => _devices;
    public event Action<IReadOnlyList<DirectInputDevice>>? DevicesRefreshed;

    public void SetDevices(List<DirectInputDevice> devices) {
        _devices = devices;
        DevicesRefreshed?.Invoke(_devices);
    }

    public IReadOnlyList<DirectInputDevice> RefreshDevices() {
        DevicesRefreshed?.Invoke(_devices);
        return _devices;
    }
}
