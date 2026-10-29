namespace StickLock.Models;

public class AppConfig {
    public string StarCitizenPath { get; set; } = string.Empty;
    public ScEnvironment SelectedEnvironment { get; set; } = ScEnvironment.LIVE;
    public bool AutoSyncOnUsbChange { get; set; } = true;
    public bool StartMinimizedToTray { get; set; } = false;
    public bool RunOnWindowsStartup { get; set; } = false;
    public List<CanonicalStick> CanonicalSticks { get; set; } = new();
    public int MaxBackupsToKeep { get; set; } = 10;
}
