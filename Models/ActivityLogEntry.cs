namespace StickLock.Models;

public class ActivityLogEntry {
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Level { get; set; } = "Info"; // "Info", "Success", "Warning", "Error"

    public string TimeFormatted => Timestamp.ToString("HH:mm:ss");
}
