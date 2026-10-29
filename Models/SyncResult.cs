namespace StickLock.Models;

public class SyncResult {
    public bool Success { get; set; }
    public bool ChangesMade { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? BackupPath { get; set; }
    public SyncStatus Status { get; set; } = SyncStatus.Unknown;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public static SyncResult Ok(string message, bool changesMade = false, string? backupPath = null, SyncStatus status = SyncStatus.Aligned) {
        return new SyncResult {
            Success = true,
            ChangesMade = changesMade,
            Message = message,
            BackupPath = backupPath,
            Status = status
        };
    }

    public static SyncResult Fail(string message, SyncStatus status = SyncStatus.Error) {
        return new SyncResult {
            Success = false,
            ChangesMade = false,
            Message = message,
            Status = status
        };
    }
}
