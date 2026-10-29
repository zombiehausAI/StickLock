using StickLock.Models;

namespace StickLock.Services;

public interface IActivityLogService {
    IReadOnlyList<ActivityLogEntry> Entries { get; }
    event Action<ActivityLogEntry>? EntryAdded;
    void LogInfo(string message, string details = "");
    void LogSuccess(string message, string details = "");
    void LogWarning(string message, string details = "");
    void LogError(string message, string details = "");
    void Clear();
}
