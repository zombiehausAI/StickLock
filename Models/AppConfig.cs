namespace StickLock.Models;

public class AppConfig {
    public string StarCitizenRootPath { get; set; } = string.Empty;
    public string StarCitizenPath { get; set; } = string.Empty;
    public ScEnvironment SelectedEnvironment { get; set; } = ScEnvironment.LIVE;
    public bool SyncAllDetectedInstalls { get; set; } = true;
    public bool AutoSyncOnUsbChange { get; set; } = true;
    public bool StartMinimizedToTray { get; set; } = false;
    public bool RunOnWindowsStartup { get; set; } = false;
    public bool AllowSyncWhileGameRunning { get; set; } = false;
    public bool AutoDeployToMappingsFolder { get; set; } = true;
    public bool HasShownTrayMinimizeTip { get; set; } = false;
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
    public int? WindowWidth { get; set; }
    public int? WindowHeight { get; set; }
    public bool WindowMaximized { get; set; } = false;
    public List<CanonicalStick> CanonicalSticks { get; set; } = new();
    public int MaxBackupsToKeep { get; set; } = 10;
}

