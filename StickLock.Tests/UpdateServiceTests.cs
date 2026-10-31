using System.Net;
using System.Reflection;
using System.Text;
using StickLock.Models;
using StickLock.Services;
using Xunit;

namespace StickLock.Tests;

public class UpdateServiceTests {
    private class MockHttpMessageHandler : HttpMessageHandler {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            return Task.FromResult(_handler(request));
        }
    }

    [Fact]
    public async Task CheckForUpdateAsync_DetectsUpdate_WhenRemoteIsNewer() {
        var json = @"{
            ""tag_name"": ""v2.5.0"",
            ""body"": ""New flight axis features and improvements."",
            ""published_at"": ""2026-10-31T12:00:00Z"",
            ""assets"": [
                {
                    ""name"": ""StickLock-v2.5.0-Setup.exe"",
                    ""browser_download_url"": ""https://github.com/zombiehausAI/StickLock/releases/download/v2.5.0/StickLock-v2.5.0-Setup.exe"",
                    ""size"": 42000000
                }
            ]
        }";

        var mockHandler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(mockHandler);
        var logService = new ActivityLogService();
        var updateService = new UpdateService(logService, httpClient);

        var info = await updateService.CheckForUpdateAsync(force: true);

        Assert.Equal("2.5.0", info.LatestVersion);
        Assert.True(info.IsUpdateAvailable);
        Assert.Equal("StickLock-v2.5.0-Setup.exe", info.InstallerFileName);
        Assert.Contains("New flight axis features", info.ReleaseNotes);
        Assert.Equal("https://github.com/zombiehausAI/StickLock/releases/download/v2.5.0/StickLock-v2.5.0-Setup.exe", info.DownloadUrl);
    }

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsNotAvailable_WhenRemoteIsSame() {
        var currentVer = typeof(UpdateService).Assembly
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "1.1.0";
        var plusIdx = currentVer.IndexOf('+');
        if (plusIdx > 0) {
            currentVer = currentVer.Substring(0, plusIdx);
        }
        currentVer = currentVer.TrimStart('v', 'V');

        var json = $@"{{
            ""tag_name"": ""v{currentVer}"",
            ""body"": ""Current release"",
            ""assets"": []
        }}";

        var mockHandler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(mockHandler);
        var logService = new ActivityLogService();
        var updateService = new UpdateService(logService, httpClient);

        var info = await updateService.CheckForUpdateAsync(force: true);

        Assert.Equal(currentVer, info.LatestVersion);
        Assert.False(info.IsUpdateAvailable);
    }

    [Fact]
    public async Task DownloadUpdateAsync_DownloadsBytes_AndReportsProgress() {
        var testBytes = Encoding.UTF8.GetBytes("MOCK_INSTALLER_BINARY_DATA_TEST_1234567890");
        var mockHandler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new ByteArrayContent(testBytes)
        });

        var httpClient = new HttpClient(mockHandler);
        var logService = new ActivityLogService();
        var updateService = new UpdateService(logService, httpClient);

        var updateInfo = new AppUpdateInfo {
            LatestVersion = "9.9.9",
            InstallerFileName = "StickLock-Test-Setup.exe",
            DownloadUrl = "https://example.com/StickLock-Test-Setup.exe",
            FileSizeBytes = testBytes.Length
        };

        var progressCalls = new List<int>();
        var progress = new Progress<int>(p => progressCalls.Add(p));

        var downloadedPath = await updateService.DownloadUpdateAsync(updateInfo, progress);

        Assert.True(File.Exists(downloadedPath));
        Assert.Equal(testBytes, await File.ReadAllBytesAsync(downloadedPath));

        try {
            File.Delete(downloadedPath);
        } catch { }
    }

    [Fact]
    public void IsNewerVersion_DevBuildsAndReleases_ComparesCorrectly() {
        // Base version increment
        Assert.True(UpdateService.IsNewerVersion("1.1.0", "1.0.1"));
        Assert.False(UpdateService.IsNewerVersion("1.0.1", "1.1.0"));

        // Three-tier progression: alpha -> beta -> release
        Assert.True(UpdateService.IsNewerVersion("1.1.0-beta.1", "1.1.0-alpha.5"));
        Assert.False(UpdateService.IsNewerVersion("1.1.0-alpha.5", "1.1.0-beta.1"));
        Assert.True(UpdateService.IsNewerVersion("1.1.0", "1.1.0-beta.10"));
        Assert.False(UpdateService.IsNewerVersion("1.1.0-beta.10", "1.1.0"));

        // Increments within same tier
        Assert.True(UpdateService.IsNewerVersion("1.1.0-alpha.2", "1.1.0-alpha.1"));
        Assert.False(UpdateService.IsNewerVersion("1.1.0-alpha.1", "1.1.0-alpha.2"));
        Assert.True(UpdateService.IsNewerVersion("1.1.0-beta.5", "1.1.0-beta.2"));
        Assert.False(UpdateService.IsNewerVersion("1.1.0-beta.2", "1.1.0-beta.5"));

        // Legacy -dev tag backward compatibility
        Assert.True(UpdateService.IsNewerVersion("1.1.0-dev.21", "1.1.0-dev.20"));
        Assert.True(UpdateService.IsNewerVersion("1.1.0", "1.1.0-dev.20"));
    }

    [Fact]
    public async Task CheckForUpdateAsync_FiltersByChannel_StableVsDevelopment() {
        var json = @"[
            {
                ""tag_name"": ""v1.1.0-alpha.10"",
                ""prerelease"": true,
                ""target_commitish"": ""feature/plugin-system"",
                ""assets"": [ { ""name"": ""StickLock-v1.1.0-alpha.10-Setup.exe"", ""browser_download_url"": ""https://example.com/alpha.exe"" } ]
            },
            {
                ""tag_name"": ""v1.1.0-beta.5"",
                ""prerelease"": true,
                ""target_commitish"": ""dev"",
                ""assets"": [ { ""name"": ""StickLock-v1.1.0-beta.5-Setup.exe"", ""browser_download_url"": ""https://example.com/beta.exe"" } ]
            },
            {
                ""tag_name"": ""v1.0.1"",
                ""prerelease"": false,
                ""target_commitish"": ""main"",
                ""assets"": [ { ""name"": ""StickLock-v1.0.1-Setup.exe"", ""browser_download_url"": ""https://example.com/stable.exe"" } ]
            }
        ]";

        var mockHandler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(mockHandler);
        var logService = new ActivityLogService();
        var updateService = new UpdateService(logService, httpClient);

        // When checking Release channel, picks 1.0.1
        var releaseInfo = await updateService.CheckForUpdateAsync(force: true, channel: "Release");
        Assert.Equal("1.0.1", releaseInfo.LatestVersion);
        Assert.False(releaseInfo.IsPrerelease);

        // When checking Beta channel, picks 1.1.0-beta.5
        var betaInfo = await updateService.CheckForUpdateAsync(force: true, channel: "Beta");
        Assert.Equal("1.1.0-beta.5", betaInfo.LatestVersion);
        Assert.True(betaInfo.IsPrerelease);

        // When checking Alpha channel, picks newest preview (1.1.0-alpha.10)
        var alphaInfo = await updateService.CheckForUpdateAsync(force: true, channel: "Alpha");
        Assert.Equal("1.1.0-alpha.10", alphaInfo.LatestVersion);
        Assert.True(alphaInfo.IsPrerelease);
    }
}

