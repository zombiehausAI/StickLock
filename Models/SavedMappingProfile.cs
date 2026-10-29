namespace StickLock.Models;

public class SavedMappingProfile {
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public long FileSizeBytes { get; set; }
    public int JoystickOptionsCount { get; set; }
    public int RebindCount { get; set; }
    public List<string> DeviceNames { get; set; } = new();
    public bool ExistsInStarCitizen { get; set; }
}

