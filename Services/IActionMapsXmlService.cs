using StickLock.Models;

namespace StickLock.Services;

public interface IActionMapsXmlService {
    Task<SyncStatus> CheckSyncStatusAsync();
    Task<SyncResult> SyncAsync();
    Task<IReadOnlyList<XmlStickMapping>> GetXmlMappingsAsync();
    Task<string> GetConsoleResortCommandAsync();
    Task<SyncResult> RestoreLatestBackupAsync();
    IReadOnlyList<string> GetAvailableBackups();
}
