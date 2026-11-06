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
    public event Action<int, string>? ButtonInputDetected;
    private CancellationTokenSource? _monitoringCts;
    private IDirectInputDevice8? _monitoredDevice;

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

    public Task<IReadOnlyList<DirectInputDevice>> RefreshDevicesAsync() {
        return Task.Run(() => RefreshDevices());
    }

    public void StartInputMonitoring(int slotIndex) {
        StopInputMonitoring();

        lock (_lock) {
            if (_directInput == null) {
                return;
            }

            var targetDev = _currentDevices.FirstOrDefault(d => d.SlotIndex == slotIndex);
            if (targetDev == null) {
                return;
            }

            try {
                _monitoredDevice = _directInput.CreateDevice(targetDev.InstanceGuid);
                _monitoredDevice.SetDataFormat<RawJoystickState>();
                _monitoredDevice.SetCooperativeLevel(IntPtr.Zero, CooperativeLevel.NonExclusive | CooperativeLevel.Background);
                _monitoredDevice.Acquire();
            } catch {
                _monitoredDevice?.Dispose();
                _monitoredDevice = null;
                return;
            }
        }

        _monitoringCts = new CancellationTokenSource();
        var token = _monitoringCts.Token;

        Task.Run(async () => {
            var state = new JoystickState();
            while (!token.IsCancellationRequested) {
                try {
                    lock (_lock) {
                        if (_monitoredDevice == null) {
                            break;
                        }
                        _monitoredDevice.Poll();
                        _monitoredDevice.GetCurrentJoystickState(ref state);
                    }

                    if (state.Buttons != null) {
                        for (int i = 0; i < state.Buttons.Length; i++) {
                            if (state.Buttons[i]) {
                                ButtonInputDetected?.Invoke(slotIndex, $"button{i + 1}");
                            }
                        }
                    }

                    if (state.PointOfViewControllers != null && state.PointOfViewControllers.Length > 0) {
                        var pov = state.PointOfViewControllers[0];
                        if (pov >= 0) {
                            string hatDir;
                            if (pov > 31500 || pov < 4500) {
                                hatDir = "hat1_up";
                            } else if (pov >= 4500 && pov < 13500) {
                                hatDir = "hat1_right";
                            } else if (pov >= 13500 && pov < 22500) {
                                hatDir = "hat1_down";
                            } else {
                                hatDir = "hat1_left";
                            }
                            ButtonInputDetected?.Invoke(slotIndex, hatDir);
                        }
                    }
                } catch {
                    // Ignore transient device polling exceptions
                }

                try {
                    await Task.Delay(30, token);
                } catch {
                    break;
                }
            }
        }, token);
    }

    public void StopInputMonitoring() {
        _monitoringCts?.Cancel();
        _monitoringCts?.Dispose();
        _monitoringCts = null;

        lock (_lock) {
            try {
                _monitoredDevice?.Unacquire();
                _monitoredDevice?.Dispose();
            } catch {
                // Ignore disposal errors
            }
            _monitoredDevice = null;
        }
    }

    public void Dispose() {
        StopInputMonitoring();
        lock (_lock) {
            _directInput?.Dispose();
            _directInput = null;
        }
    }
}

