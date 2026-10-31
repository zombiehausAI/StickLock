using StickLock.Models;

namespace StickLock.Services;

public interface IUpdateService {
    AppUpdateInfo? CachedUpdateInfo { get; }
    string CurrentChannel { get; }
    Task<AppUpdateInfo> CheckForUpdateAsync(bool force = false, string? channel = null);
    Task SetUpdateChannelAsync(string channel);
    Task<string> DownloadUpdateAsync(AppUpdateInfo updateInfo, IProgress<int>? progress = null, CancellationToken cancellationToken = default);
    void LaunchInstallerAndExit(string installerPath);
}

