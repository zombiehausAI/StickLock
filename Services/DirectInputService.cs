using Vortice.DirectInput;
using StickLock.Models;

namespace StickLock.Services;

public class DirectInputService : IDirectInputService, IDisposable {
    private readonly object _lock = new();
    private List<DirectInputDevice> _currentDevices = new();
    private IDirectInput8? _directInput;

    public IReadOnlyList<DirectInputDevice> CurrentDevices {
        get {
            lock (_lock) {
                return _currentDevices.ToList();
            }
        }
    }

    public event Action<IReadOnlyList<DirectInputDevice>>? DevicesRefreshed;

    public DirectInputService() {
        try {
            _directInput = DInput.DirectInput8Create();
        } catch {
            _directInput = null;
        }

        RefreshDevices();
    }

    public IReadOnlyList<DirectInputDevice> RefreshDevices() {
        lock (_lock) {
            var result = new List<DirectInputDevice>();

            if (_directInput == null) {
                try {
                    _directInput = DInput.DirectInput8Create();
                } catch {
                    _currentDevices = result;
                    DevicesRefreshed?.Invoke(result);
                    return result;
                }
            }

            try {
                var devices = _directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly);
                var slot = 1;
                foreach (var device in devices) {
                    var friendlyName = !string.IsNullOrWhiteSpace(device.InstanceName)
                        ? device.InstanceName
                        : (!string.IsNullOrWhiteSpace(device.ProductName) ? device.ProductName : $"Joystick #{slot}");

                    result.Add(new DirectInputDevice {
                        SlotIndex = slot,
                        ProductName = friendlyName.Trim(),
                        InstanceGuid = device.InstanceGuid,
                        ProductGuid = device.ProductGuid
                    });
                    slot++;
                }
            } catch {
                // If DirectInput enumerations fail, return whatever we have
            }

            _currentDevices = result;
            DevicesRefreshed?.Invoke(result);
            return result;
        }
    }

    public void Dispose() {
        lock (_lock) {
            _directInput?.Dispose();
            _directInput = null;
        }
    }
}
