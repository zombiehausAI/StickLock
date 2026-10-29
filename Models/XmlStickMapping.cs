namespace StickLock.Models;

public class XmlStickMapping {
    public int Instance { get; set; }
    public string ProductAttribute { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public Guid ExtractedGuid { get; set; } = Guid.Empty;

    public string SlotName => $"js{Instance}";
    public string ExtractedGuidString => ExtractedGuid != Guid.Empty ? ExtractedGuid.ToString("D").ToUpperInvariant() : "N/A";
}

