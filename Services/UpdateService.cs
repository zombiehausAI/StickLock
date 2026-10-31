using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using StickLock.Models;

namespace StickLock.Services;

public class UpdateService : IUpdateService {
    private const string GitHubReleasesApiUrl = "https://api.github.com/repos/zombiehausAI/StickLock/releases";
    private readonly HttpClient _httpClient;
    private readonly IActivityLogService _logService;
    private readonly IAppConfigService? _configService;
    private AppUpdateInfo? _cachedUpdateInfo;

    public AppUpdateInfo? CachedUpdateInfo => _cachedUpdateInfo;

    public string CurrentChannel {
        get {
            if (_configService != null && !string.IsNullOrWhiteSpace(_configService.Config.UpdateChannel)) {
                return _configService.Config.UpdateChannel;
            }
            var ver = GetCurrentAppVersion();
            if (ver.Contains("-alpha", StringComparison.OrdinalIgnoreCase)) {
                return "Alpha";
            }
            if (ver.Contains("-beta", StringComparison.OrdinalIgnoreCase) || ver.Contains("-dev", StringComparison.OrdinalIgnoreCase)) {
                return "Beta";
            }
            return "Release";
        }
    }

    public UpdateService(IActivityLogService logService, HttpClient? httpClient = null)
        : this(logService, null, httpClient) {
    }

    public UpdateService(
        IActivityLogService logService,
        IAppConfigService? configService = null,
        HttpClient? httpClient = null) {
        _logService = logService;
        _configService = configService;
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent")) {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "StickLock-App");
        }
    }

    public async Task SetUpdateChannelAsync(string channel) {
        var normalized = string.IsNullOrWhiteSpace(channel) ? "Release" : channel.Trim();
        if (_configService != null) {
            await _configService.SetUpdateChannelAsync(normalized);
        }
        _cachedUpdateInfo = null;
        _logService.LogInfo($"Update channel switched to {normalized}.");
    }

    public async Task<AppUpdateInfo> CheckForUpdateAsync(bool force = false, string? channel = null) {
        var activeChannel = channel ?? CurrentChannel;

        if (!force && _cachedUpdateInfo != null && string.Equals(_cachedUpdateInfo.Channel, activeChannel, StringComparison.OrdinalIgnoreCase)) {
            return _cachedUpdateInfo;
        }

        var currentVerStr = GetCurrentAppVersion();
        var info = new AppUpdateInfo {
            CurrentVersion = currentVerStr,
            Channel = activeChannel
        };

        try {
            using var request = new HttpRequestMessage(HttpMethod.Get, GitHubReleasesApiUrl);
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

            var releasesList = new List<JsonElement>();
            if (root.ValueKind == JsonValueKind.Array) {
                foreach (var el in root.EnumerateArray()) {
                    releasesList.Add(el);
                }
            } else if (root.ValueKind == JsonValueKind.Object) {
                releasesList.Add(root);
            } else {
                info.ErrorMessage = "Unexpected response from GitHub Releases API.";
                _cachedUpdateInfo = info;
                return info;
            }

            JsonElement? matchedRelease = null;

            foreach (var release in releasesList) {
                var isDraft = release.TryGetProperty("draft", out var draftProp) && draftProp.GetBoolean();
                if (isDraft) {
                    continue;
                }

                var isPrerelease = release.TryGetProperty("prerelease", out var preProp) && preProp.GetBoolean();
                var targetBranch = release.TryGetProperty("target_commitish", out var branchProp) ? branchProp.GetString() ?? "" : "";
                var tagName = release.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
                var relName = release.TryGetProperty("name", out var rNameProp) ? rNameProp.GetString() ?? "" : "";

                if (string.Equals(activeChannel, "Release", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(activeChannel, "Stable", StringComparison.OrdinalIgnoreCase)) {
                    // Stable / Release channel: only official non-prereleases
                    if (!isPrerelease) {
                        matchedRelease = release;
                        break;
                    }
                } else if (string.Equals(activeChannel, "Beta", StringComparison.OrdinalIgnoreCase)) {
                    // Beta channel: dev branch builds (-beta or -dev) or official release
                    var isBetaOrDev = tagName.Contains("-beta", StringComparison.OrdinalIgnoreCase)
                        || tagName.Contains("-dev", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(targetBranch, "dev", StringComparison.OrdinalIgnoreCase);

                    if (isPrerelease && isBetaOrDev) {
                        matchedRelease = release;
                        break;
                    }
                } else if (string.Equals(activeChannel, "Alpha", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(activeChannel, "Development", StringComparison.OrdinalIgnoreCase)) {
                    // Alpha channel: any feature preview or dev build (prerelease)
                    if (isPrerelease) {
                        matchedRelease = release;
                        break;
                    }
                } else {
                    // Specific branch channel (e.g. "feature/plugin-system")
                    var matchesBranch = string.Equals(targetBranch, activeChannel, StringComparison.OrdinalIgnoreCase)
                        || tagName.Contains(activeChannel, StringComparison.OrdinalIgnoreCase);

                    if (matchesBranch) {
                        matchedRelease = release;
                        break;
                    }
                }
            }

            // Fallback: if no specific match found, take the newest release available
            if (matchedRelease == null && releasesList.Count > 0) {
                matchedRelease = releasesList[0];
            }

            if (matchedRelease == null) {
                info.ErrorMessage = "No releases found on GitHub.";
                _cachedUpdateInfo = info;
                return info;
            }

            var selected = matchedRelease.Value;
            var matchedTag = selected.TryGetProperty("tag_name", out var sTagProp) ? sTagProp.GetString() ?? "" : "";
            var cleanTag = matchedTag.TrimStart('v', 'V');
            info.LatestVersion = cleanTag;
            info.IsPrerelease = selected.TryGetProperty("prerelease", out var sPreProp) && sPreProp.GetBoolean();
            info.TargetBranch = selected.TryGetProperty("target_commitish", out var sBranchProp) ? sBranchProp.GetString() : "";

            if (selected.TryGetProperty("body", out var bodyProp)) {
                info.ReleaseNotes = bodyProp.GetString() ?? "";
            }

            if (selected.TryGetProperty("published_at", out var pubProp) && pubProp.TryGetDateTime(out var pubDate)) {
                info.PublishedAt = pubDate;
            }

            // Find setup executable asset
            if (selected.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array) {
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
                _logService.LogSuccess($"New StickLock version available on [{activeChannel}]: v{info.LatestVersion} (Current: v{info.CurrentVersion})");
            } else {
                _logService.LogInfo($"StickLock is up to date on [{activeChannel}] (v{info.CurrentVersion}).");
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

    public static bool IsNewerVersion(string candidateVerStr, string currentVerStr) {
        if (string.IsNullOrWhiteSpace(candidateVerStr)) {
            return false;
        }

        var candidateBase = candidateVerStr.Split('-')[0];
        var currentBase = currentVerStr.Split('-')[0];

        if (Version.TryParse(candidateBase, out var candidateVer) && Version.TryParse(currentBase, out var currentVer)) {
            if (candidateVer > currentVer) {
                return true;
            }
            if (candidateVer < currentVer) {
                return false;
            }

            // Both base versions are equal (e.g. 1.1.0 vs 1.1.0)
            int candidateRank = GetTierRank(candidateVerStr);
            int currentRank = GetTierRank(currentVerStr);

            if (candidateRank > currentRank) {
                return true;
            }
            if (candidateRank < currentRank) {
                return false;
            }

            // Both have identical tier rank (e.g. both alpha or both beta)
            if (candidateRank < 3) {
                int candidateBuild = ExtractBuildNumber(candidateVerStr);
                int currentBuild = ExtractBuildNumber(currentVerStr);
                return candidateBuild > currentBuild;
            }
        }

        return false;
    }

    private static int GetTierRank(string verStr) {
        if (verStr.Contains("-alpha", StringComparison.OrdinalIgnoreCase)) {
            return 1;
        }
        if (verStr.Contains("-beta", StringComparison.OrdinalIgnoreCase) || verStr.Contains("-dev", StringComparison.OrdinalIgnoreCase)) {
            return 2;
        }
        return 3; // Official release
    }

    private static int ExtractBuildNumber(string verStr) {
        var match = Regex.Match(verStr, @"-(alpha|beta|dev)\.?(\d+)");
        if (match.Success && int.TryParse(match.Groups[2].Value, out var num)) {
            return num;
        }
        return 0;
    }
}
