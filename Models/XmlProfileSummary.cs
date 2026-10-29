namespace StickLock.Models;

public class XmlProfileSummary {
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int JoystickOptionsCount { get; set; }
    public int RebindCount { get; set; }
    public List<string> DeviceNames { get; set; } = new();
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public string SummaryText => IsValid
        ? $"{JoystickOptionsCount} joystick device(s), {RebindCount} rebinds ({string.Join(", ", DeviceNames)})"
        : $"Invalid: {ErrorMessage}";
}
