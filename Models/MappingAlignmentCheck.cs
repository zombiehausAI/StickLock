namespace StickLock.Models;

public class MappingAlignmentCheck {
    public bool IsMisaligned { get; set; }
    public SyncStatus Status { get; set; } = SyncStatus.Unknown;
    public string StatusMessage { get; set; } = string.Empty;
    public List<StickMismatchDetail> Mismatches { get; set; } = new();
    public List<XmlStickMapping> ExistingXmlDevices { get; set; } = new();
    public bool HasCanonicalConfigured { get; set; }
    public bool CanAutoFix { get; set; }
    public string? LatestBackupPath { get; set; }
}

