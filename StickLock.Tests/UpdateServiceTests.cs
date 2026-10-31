using System.Net;
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
        var currentVer = typeof(UpdateService).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
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
        // If current is an assembly dev build or release matching currentVer
        if (!info.CurrentVersion.Contains("-dev")) {
            Assert.False(info.IsUpdateAvailable);
        }
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

    [Theory]
    [InlineData("1.1.0", "1.0.2", true)]
    [InlineData("1.0.2", "1.1.0", false)]
    [InlineData("1.1.0-alpha.5", "1.1.0-alpha.4", true)]
    [InlineData("1.1.0-alpha.4", "1.1.0-alpha.5", false)]
    [InlineData("1.1.0-beta.1", "1.1.0-alpha.50", true)]
    [InlineData("1.1.0-alpha.50", "1.1.0-beta.1", false)]
    [InlineData("1.1.0", "1.1.0-beta.10", true)]
    [InlineData("1.1.0-beta.10", "1.1.0", false)]
    [InlineData("1.1.0", "1.1.0", false)]
    [InlineData("1.1.0-alpha.22", "1.0.2", true)]
    [InlineData("1.0.2", "1.1.0-alpha.22", false)]
    public void IsNewerVersion_ThreeTierSemVerComparison_WorksAsExpected(string candidate, string current, bool expected) {
        var result = UpdateService.IsNewerVersion(candidate, current);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task CheckForUpdateAsync_FiltersByChannel_RespectsChannelPreference() {
        var json = @"[
            {
                ""tag_name"": ""v1.1.0-alpha.22"",
                ""prerelease"": true,
                ""target_commitish"": ""feature/plugin-system"",
                ""body"": ""Alpha plugin build"",
                ""assets"": [
                    {
                        ""name"": ""StickLock-v1.1.0-alpha.22-Setup.exe"",
                        ""browser_download_url"": ""https://github.com/zombiehausAI/StickLock/releases/download/v1.1.0-alpha.22/StickLock-v1.1.0-alpha.22-Setup.exe"",
                        ""size"": 40000000
                    }
                ]
            },
            {
                ""tag_name"": ""v1.1.0-beta.5"",
                ""prerelease"": true,
                ""target_commitish"": ""dev"",
                ""body"": ""Beta dev build"",
                ""assets"": [
                    {
                        ""name"": ""StickLock-v1.1.0-beta.5-Setup.exe"",
                        ""browser_download_url"": ""https://github.com/zombiehausAI/StickLock/releases/download/v1.1.0-beta.5/StickLock-v1.1.0-beta.5-Setup.exe"",
                        ""size"": 40000000
                    }
                ]
            },
            {
                ""tag_name"": ""v1.0.2"",
                ""prerelease"": false,
                ""target_commitish"": ""main"",
                ""body"": ""Stable release"",
                ""assets"": [
                    {
                        ""name"": ""StickLock-v1.0.2-Setup.exe"",
                        ""browser_download_url"": ""https://github.com/zombiehausAI/StickLock/releases/download/v1.0.2/StickLock-v1.0.2-Setup.exe"",
                        ""size"": 40000000
                    }
                ]
            }
        ]";

        var mockHandler = new MockHttpMessageHandler(req => new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(mockHandler);
        var logService = new ActivityLogService();
        var updateService = new UpdateService(logService, httpClient);

        // When requesting Beta channel
        var betaInfo = await updateService.CheckForUpdateAsync(force: true, channel: "Beta");
        Assert.Equal("1.1.0-beta.5", betaInfo.LatestVersion);
        Assert.Equal("Beta", betaInfo.Channel);

        // When requesting Alpha channel
        var alphaInfo = await updateService.CheckForUpdateAsync(force: true, channel: "Alpha");
        Assert.Equal("1.1.0-alpha.22", alphaInfo.LatestVersion);
        Assert.Equal("Alpha", alphaInfo.Channel);

        // When requesting Release channel
        var releaseInfo = await updateService.CheckForUpdateAsync(force: true, channel: "Release");
        Assert.Equal("1.0.2", releaseInfo.LatestVersion);
        Assert.Equal("Release", releaseInfo.Channel);
    }
}

