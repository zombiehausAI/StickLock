namespace StickLock.Services;

public interface IFileDialogService {
    Task<string?> PickActionMapsXmlFileAsync(string? initialDirectory = null);
    Task<string?> PickStarCitizenFolderAsync(string? initialDirectory = null);
}
