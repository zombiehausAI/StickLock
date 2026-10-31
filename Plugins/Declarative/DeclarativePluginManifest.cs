using System.Text.Json.Serialization;

namespace StickLock.Plugins.Declarative;

public class DeclarativePluginManifest {
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("processName")]
    public string ProcessName { get; set; } = string.Empty;

    [JsonPropertyName("iconSvg")]
    public string IconSvg { get; set; } = "M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5";

    [JsonPropertyName("configDirectory")]
    public string ConfigDirectory { get; set; } = string.Empty;

    [JsonPropertyName("filePattern")]
    public string FilePattern { get; set; } = "*.*";

    [JsonPropertyName("matchStrategy")]
    public string MatchStrategy { get; set; } = "GuidInFileName";

    [JsonPropertyName("supportsFlightAxisMode")]
    public bool SupportsFlightAxisMode { get; set; } = false;
}
