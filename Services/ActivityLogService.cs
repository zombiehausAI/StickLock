using StickLock.Models;

namespace StickLock.Services;

public class ActivityLogService : IActivityLogService {
    private const int MaxEntries = 150;
    private readonly object _lock = new();
    private readonly List<ActivityLogEntry> _entries = new();

    public IReadOnlyList<ActivityLogEntry> Entries {
        get {
            lock (_lock) {
                return _entries.ToList();
            }
        }
    }

    public event Action<ActivityLogEntry>? EntryAdded;

    public void LogInfo(string message, string details = "") {
        AddEntry(new ActivityLogEntry {
            Timestamp = DateTime.Now,
            Message = message,
            Details = details,
            Level = "Info"
        });
    }

    public void LogSuccess(string message, string details = "") {
        AddEntry(new ActivityLogEntry {
            Timestamp = DateTime.Now,
            Message = message,
            Details = details,
            Level = "Success"
        });
    }

    public void LogWarning(string message, string details = "") {
        AddEntry(new ActivityLogEntry {
            Timestamp = DateTime.Now,
            Message = message,
            Details = details,
            Level = "Warning"
        });
    }

    public void LogError(string message, string details = "") {
        AddEntry(new ActivityLogEntry {
            Timestamp = DateTime.Now,
            Message = message,
            Details = details,
            Level = "Error"
        });
    }

    public void Clear() {
        lock (_lock) {
            _entries.Clear();
        }
    }

    private void AddEntry(ActivityLogEntry entry) {
        lock (_lock) {
            _entries.Insert(0, entry);
            if (_entries.Count > MaxEntries) {
                _entries.RemoveAt(_entries.Count - 1);
            }
        }
        EntryAdded?.Invoke(entry);
    }
}
