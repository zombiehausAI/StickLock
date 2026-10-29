namespace StickLock.Models;

public class CanonicalStick {
    public int RoleSlot { get; set; } = 1;
    public string FriendlyRoleName { get; set; } = "Primary Stick";
    public Guid InstanceGuid { get; set; } = Guid.Empty;
    public Guid ProductGuid { get; set; } = Guid.Empty;
    public string ProductName { get; set; } = string.Empty;

    public string SlotName => $"js{RoleSlot}";
    public string InstanceGuidString => InstanceGuid.ToString("D").ToUpperInvariant();
    public string ProductGuidString => ProductGuid.ToString("D").ToUpperInvariant();
}
