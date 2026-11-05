namespace StickLock.Models;

public class ControllerMappingItem {
    public int StickInstance { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceGuid { get; set; } = string.Empty;
    public string InputCode { get; set; } = string.Empty;
    public string InputType { get; set; } = "Button";
    public string DisplayInput { get; set; } = string.Empty;
    public string ActionMap { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string DisplayCategory { get; set; } = string.Empty;
    public string DisplayAction { get; set; } = string.Empty;
    public bool IsInverted { get; set; }
    public string MultiTap { get; set; } = string.Empty;
}
