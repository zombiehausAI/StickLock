using StickLock.Models;

namespace StickLock.Services;

public interface IUpdateService {
    AppUpdateInfo? CachedUpdateInfo { get; }
    Task<AppUpdateInfo> CheckForUpdateAsync(bool force = false);
    Task<string> DownloadUpdateAsync(AppUpdateInfo updateInfo, IProgress<int>? progress = null, CancellationToken cancellationToken = default);
    void LaunchInstallerAndExit(string installerPath);
}
