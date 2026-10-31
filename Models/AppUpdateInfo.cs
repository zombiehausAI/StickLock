namespace StickLock.Models;

public class AppUpdateInfo {
    public string CurrentVersion { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public bool IsUpdateAvailable { get; set; }
    public string ReleaseNotes { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string InstallerFileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string Channel { get; set; } = "Stable";
    public bool IsPrerelease { get; set; }
    public string? TargetBranch { get; set; }
    public string? ErrorMessage { get; set; }
}

