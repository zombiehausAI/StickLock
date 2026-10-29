namespace StickLock.Services;

public interface IFileDialogService {
    Task<string?> PickActionMapsXmlFileAsync(string? initialDirectory = null);
    Task<string?> PickStarCitizenFolderAsync(string? initialDirectory = null);
    Task<string?> SaveActionMapsXmlFileAsync(string defaultFileName, string? initialDirectory = null);
    Task<string?> PickXmlFileToImportAsync(string? initialDirectory = null);
    Task<string?> SaveJsonConfigFileAsync(string defaultFileName, string? initialDirectory = null);
    Task<string?> PickJsonConfigFileAsync(string? initialDirectory = null);
}
