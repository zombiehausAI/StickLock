using StickLock.Models;

namespace StickLock.Services;

public interface IActionMapsXmlService {
    Task<SyncStatus> CheckSyncStatusAsync();
    Task<SyncResult> SyncAsync();
    Task<IReadOnlyList<XmlStickMapping>> GetXmlMappingsAsync();
    Task<string> GetConsoleResortCommandAsync();
    Task<SyncResult> RestoreLatestBackupAsync();
    IReadOnlyList<string> GetAvailableBackups();
    Task<SyncResult> ExportActiveXmlAsync(string destinationPath);
    Task<SyncResult> ImportAndApplyXmlAsync(string sourcePath);
    Task<XmlProfileSummary> InspectXmlProfileAsync(string filePath);
}
