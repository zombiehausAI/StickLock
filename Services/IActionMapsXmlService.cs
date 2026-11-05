using StickLock.Models;

namespace StickLock.Services;

public interface IActionMapsXmlService {
    Task<SyncStatus> CheckSyncStatusAsync();
    Task<MappingAlignmentCheck> CheckAlignmentAsync();
    Task<SyncResult> SyncAsync(bool force = false);
    Task<SyncResult> PropagateActiveToDetectedInstallsAsync(bool force = false);
    Task<SyncResult> FixAndSyncAsync(bool force = false);
    Task<SyncResult> AutoLockAndAlignFromXmlAsync(bool force = false);
    Task<IReadOnlyList<XmlStickMapping>> GetXmlMappingsAsync();
    Task<string> GetConsoleResortCommandAsync();
    Task<IReadOnlyList<string>> GetConsoleResortCommandsAsync();
    Task<SyncResult> RestoreLatestBackupAsync();
    IReadOnlyList<string> GetAvailableBackups();
    Task<SyncResult> ExportActiveXmlAsync(string destinationPath);
    Task<SyncResult> ImportAndApplyXmlAsync(string sourcePath);
    Task<XmlProfileSummary> InspectXmlProfileAsync(string filePath);
    Task<IReadOnlyList<SavedMappingProfile>> GetSavedMappingsAsync();
    Task<SyncResult> SaveActiveAsProfileAsync(string profileName);
    Task<SyncResult> CopyProfileToStarCitizenAsync(string profileFileName);
    Task<SyncResult> CopyAllProfilesToStarCitizenAsync();
    Task<SyncResult> PullProfilesFromStarCitizenAsync();
    Task<SyncResult> ApplySavedProfileToActiveAsync(string profileFileName);
    Task<bool> DeleteSavedProfileAsync(string profileFileName);
    Task<SyncResult> ApplyFlightAxisModeAsync(FlightAxisMode mode, bool force = false);
    Task<int> DetectFlightStickSlotFromXmlAsync(string? xmlPath = null);
    Task<IReadOnlyList<ControllerMappingItem>> GetDetailedMappingsAsync(string? filePath = null);
    Task<SyncResult> RebindActionAsync(string actionMapName, string actionName, int stickSlot, string inputCode);
    Task<SyncResult> ClearRebindActionAsync(string actionMapName, string actionName);
    string? GetStarCitizenMappingsDirectory();
    IReadOnlyList<string> GetStarCitizenCustomMappings();
}
