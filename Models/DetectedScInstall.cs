namespace StickLock.Models;

public class DetectedScInstall {
    public ScEnvironment Environment { get; set; } = ScEnvironment.LIVE;
    public string ChannelName { get; set; } = string.Empty;
    public string ChannelDirectory { get; set; } = string.Empty;
    public string ActionMapsPath { get; set; } = string.Empty;
    public string MappingsDirectory { get; set; } = string.Empty;
    public bool IsDirectoryPresent { get; set; }
    public bool HasActionMapsXml { get; set; }
}

