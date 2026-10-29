namespace StickLock.Models;

public class DirectInputDevice {
    public int SlotIndex { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Guid InstanceGuid { get; set; }
    public Guid ProductGuid { get; set; }

    public string SlotName => $"js{SlotIndex}";
    public string InstanceGuidString => InstanceGuid.ToString("D").ToUpperInvariant();
    public string ProductGuidString => ProductGuid.ToString("D").ToUpperInvariant();
}
