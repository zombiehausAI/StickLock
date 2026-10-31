using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using StickLock.Models;

namespace StickLock.Services;

public class UpdateService : IUpdateService {
    private const string GitHubApiUrl = "https://api.github.com/repos/zombiehausAI/StickLock/releases/latest";
    private readonly HttpClient _httpClient;
    private readonly IActivityLogService _logService;
    private AppUpdateInfo? _cachedUpdateInfo;

    public AppUpdateInfo? CachedUpdateInfo => _cachedUpdateInfo;

    public UpdateService(IActivityLogService logService, HttpClient? httpClient = null) {
        _logService = logService;
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent")) {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "StickLock-App");
        }
    }

    public async Task<AppUpdateInfo> CheckForUpdateAsync(bool force = false) {
        if (!force && _cachedUpdateInfo != null) {
            return _cachedUpdateInfo;
        }

        var currentVerStr = GetCurrentAppVersion();
        var info = new AppUpdateInfo {
            CurrentVersion = currentVerStr
        };

        try {
            using var request = new HttpRequestMessage(HttpMethod.Get, GitHubApiUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) {
                info.ErrorMessage = $"GitHub API returned {response.StatusCode}";
                _logService.LogWarning("Update check failed", info.ErrorMessage);
                _cachedUpdateInfo = info;
                return info;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
            var cleanTag = tagName.TrimStart('v', 'V');
            info.LatestVersion = cleanTag;

            if (root.TryGetProperty("body", out var bodyProp)) {
                info.ReleaseNotes = bodyProp.GetString() ?? "";
            }

            if (root.TryGetProperty("published_at", out var pubProp) && pubProp.TryGetDateTime(out var pubDate)) {
                info.PublishedAt = pubDate;
            }

            // Find setup executable asset
            if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array) {
                foreach (var asset in assetsProp.EnumerateArray()) {
                    var name = asset.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "";
                    if (name.EndsWith("-Setup.exe", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) {
                        info.InstallerFileName = name;
                        info.DownloadUrl = asset.TryGetProperty("browser_download_url", out var urlProp) ? urlProp.GetString() ?? "" : "";
                        info.FileSizeBytes = asset.TryGetProperty("size", out var sizeProp) ? sizeProp.GetInt64() : 0;
                        break;
                    }
                }
            }

            info.IsUpdateAvailable = IsNewerVersion(cleanTag, currentVerStr);
            if (info.IsUpdateAvailable) {
                _logService.LogSuccess($"New StickLock version available: v{info.LatestVersion} (Current: v{info.CurrentVersion})");
            } else {
                _logService.LogInfo($"StickLock is up to date (v{info.CurrentVersion}).");
            }
        } catch (Exception ex) {
            info.ErrorMessage = ex.Message;
            _logService.LogWarning("Failed to check for updates", ex.Message);
        }

        _cachedUpdateInfo = info;
        return info;
    }

    public async Task<string> DownloadUpdateAsync(AppUpdateInfo updateInfo, IProgress<int>? progress = null, CancellationToken cancellationToken = default) {
        if (string.IsNullOrWhiteSpace(updateInfo.DownloadUrl)) {
            throw new InvalidOperationException("No download URL available for update.");
        }

        var fileName = !string.IsNullOrWhiteSpace(updateInfo.InstallerFileName)
            ? updateInfo.InstallerFileName
            : $"StickLock-v{updateInfo.LatestVersion}-Setup.exe";

        var targetPath = Path.Combine(Path.GetTempPath(), fileName);

        using var response = await _httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? updateInfo.FileSizeBytes;
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        var totalRead = 0L;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0) {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalRead += bytesRead;
            if (totalBytes > 0 && progress != null) {
                var percent = (int)((totalRead * 100) / totalBytes);
                progress.Report(percent);
            }
        }

        progress?.Report(100);
        _logService.LogSuccess("Downloaded update installer successfully", targetPath);
        return targetPath;
    }

    public void LaunchInstallerAndExit(string installerPath) {
        if (!File.Exists(installerPath)) {
            throw new FileNotFoundException("Installer not found", installerPath);
        }

        _logService.LogInfo("Launching update installer and terminating active StickLock instance...");
        var psi = new ProcessStartInfo {
            FileName = installerPath,
            UseShellExecute = true
        };

        Process.Start(psi);
        Environment.Exit(0);
    }

    private static string GetCurrentAppVersion() {
        var infoVer = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (string.IsNullOrWhiteSpace(infoVer)) {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        }

        var plusIdx = infoVer.IndexOf('+');
        if (plusIdx > 0) {
            infoVer = infoVer.Substring(0, plusIdx);
        }

        return infoVer.TrimStart('v', 'V');
    }

    private static bool IsNewerVersion(string latestVerStr, string currentVerStr) {
        if (string.IsNullOrWhiteSpace(latestVerStr)) {
            return false;
        }

        var cleanCurrent = currentVerStr.Split('-')[0];
        var cleanLatest = latestVerStr.Split('-')[0];

        if (Version.TryParse(cleanLatest, out var latestVer) && Version.TryParse(cleanCurrent, out var currentVer)) {
            if (latestVer > currentVer) {
                return true;
            }
            if (latestVer == currentVer) {
                // If current is a prerelease dev build (e.g. 1.0.0-dev.9) and latest is official 1.0.0 release
                if (currentVerStr.Contains("-dev", StringComparison.OrdinalIgnoreCase) && !latestVerStr.Contains("-dev", StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
        }

        return false;
    }
}

