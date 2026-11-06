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
    private readonly List<(int Slot, IDirectInputDevice8 Device)> _monitoredDevices = new();

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

            var targets = slotIndex > 0
                ? _currentDevices.Where(d => d.SlotIndex == slotIndex).ToList()
                : _currentDevices.ToList();

            // If requested slot wasn't found in current devices, monitor all available controllers as fallback
            if (targets.Count == 0) {
                targets = _currentDevices.ToList();
            }

            foreach (var devInfo in targets) {
                try {
                    var dev = _directInput.CreateDevice(devInfo.InstanceGuid);
                    dev.SetDataFormat<RawJoystickState>();
                    dev.SetCooperativeLevel(IntPtr.Zero, CooperativeLevel.NonExclusive | CooperativeLevel.Background);
                    dev.Acquire();
                    _monitoredDevices.Add((devInfo.SlotIndex, dev));
                } catch {
                    // Ignore non-acquirable devices
                }
            }

            if (_monitoredDevices.Count == 0) {
                return;
            }
        }

        _monitoringCts = new CancellationTokenSource();
        var token = _monitoringCts.Token;

        Task.Run(async () => {
            var state = new JoystickState();
            while (!token.IsCancellationRequested) {
                List<(int Slot, IDirectInputDevice8 Device)> pollList;
                lock (_lock) {
                    pollList = _monitoredDevices.ToList();
                }

                if (pollList.Count == 0) {
                    break;
                }

                foreach (var (slot, dev) in pollList) {
                    try {
                        dev.Poll();
                        dev.GetCurrentJoystickState(ref state);

                        if (state.Buttons != null) {
                            for (int i = 0; i < state.Buttons.Length; i++) {
                                if (state.Buttons[i]) {
                                    ButtonInputDetected?.Invoke(slot, $"button{i + 1}");
                                }
                            }
                        }

                        if (state.PointOfViewControllers != null && state.PointOfViewControllers.Length > 0) {
                            var pov = state.PointOfViewControllers[0];
                            if (pov >= 0) {
                                string hatDir = (pov > 31500 || pov < 4500) ? "hat1_up" :
                                                (pov >= 4500 && pov < 13500) ? "hat1_right" :
                                                (pov >= 13500 && pov < 22500) ? "hat1_down" : "hat1_left";
                                ButtonInputDetected?.Invoke(slot, hatDir);
                            }
                        }
                    } catch {
                        // Ignore transient polling exceptions
                    }
                }

                try {
                    await Task.Delay(25, token);
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
            foreach (var (_, dev) in _monitoredDevices) {
                try {
                    dev.Unacquire();
                    dev.Dispose();
                } catch {
                    // Ignore disposal errors
                }
            }
            _monitoredDevices.Clear();
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

