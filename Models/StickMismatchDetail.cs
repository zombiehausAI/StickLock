namespace StickLock.Models;

public class StickMismatchDetail {
    public string DeviceName { get; set; } = string.Empty;
    public int XmlInstance { get; set; }
    public int LiveWindowsSlot { get; set; }
    public bool IsFlipped => XmlInstance != LiveWindowsSlot;
}
