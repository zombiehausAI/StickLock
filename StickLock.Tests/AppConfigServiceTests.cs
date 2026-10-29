using StickLock.Models;
using StickLock.Services;

namespace StickLock.Tests;

public class AppConfigServiceTests {
    [Fact]
    public void ResolveActionMapsPath_SwitchesLiveToPtuAndBack() {
        var isolatedConfig = Path.Combine(Path.GetTempPath(), $"cfg_resolve_{Guid.NewGuid():N}", "config.json");
        try {
            var service = new AppConfigService(isolatedConfig);
            var livePath = @"C:\Program Files\Roberts Space Industries\StarCitizen\LIVE\USER\Client\0\Profiles\default\actionmaps.xml";

            var ptuPath = service.ResolveActionMapsPath(livePath, ScEnvironment.PTU);
            Assert.Contains(@"\PTU\", ptuPath);
            Assert.DoesNotContain(@"\LIVE\", ptuPath);

            var eptuPath = service.ResolveActionMapsPath(ptuPath, ScEnvironment.EPTU);
            Assert.Contains(@"\EPTU\", eptuPath);
            Assert.DoesNotContain(@"\PTU\", eptuPath);

            var backToLive = service.ResolveActionMapsPath(eptuPath, ScEnvironment.LIVE);
            Assert.Contains(@"\LIVE\", backToLive);
        } finally {
            var dir = Path.GetDirectoryName(isolatedConfig);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) {
                Directory.Delete(dir, true);
            }
        }
    }

    [Fact]
    public async Task CanonicalStick_AssignmentAndRemoval_WorksCorrectly() {
        var isolatedConfig = Path.Combine(Path.GetTempPath(), $"cfg_test_{Guid.NewGuid():N}.json");
        try {
            var service = new AppConfigService(isolatedConfig);
            var stickGuid = Guid.NewGuid();

            await service.SetCanonicalStickAsync(1, new DirectInputDevice {
                SlotIndex = 1,
                InstanceGuid = stickGuid,
                ProductGuid = Guid.NewGuid(),
                ProductName = "Test Gladiator"
            }, "Right Stick");

            Assert.Single(service.Config.CanonicalSticks);
            Assert.Equal(1, service.Config.CanonicalSticks[0].RoleSlot);
            Assert.Equal("Right Stick", service.Config.CanonicalSticks[0].FriendlyRoleName);
            Assert.Equal(stickGuid, service.Config.CanonicalSticks[0].InstanceGuid);

            await service.RemoveCanonicalStickAsync(1);
            Assert.Empty(service.Config.CanonicalSticks);
        } finally {
            if (File.Exists(isolatedConfig)) {
                File.Delete(isolatedConfig);
            }
        }
    }

    [Fact]
    public async Task ExportAndImportConfig_RoundtripsSuccessfully() {
        var tempFile = Path.Combine(Path.GetTempPath(), $"sticklock_cfg_{Guid.NewGuid():N}.json");
        var isolated1 = Path.Combine(Path.GetTempPath(), $"sticklock_inst1_{Guid.NewGuid():N}.json");
        var isolated2 = Path.Combine(Path.GetTempPath(), $"sticklock_inst2_{Guid.NewGuid():N}.json");
        try {
            var service1 = new AppConfigService(isolated1);
            await service1.SetStarCitizenPathAsync(@"D:\Games\StarCitizen\LIVE\actionmaps.xml");
            await service1.SetAutoSyncAsync(false);
            await service1.SetStartMinimizedAsync(true);

            await service1.ExportConfigAsync(tempFile);
            Assert.True(File.Exists(tempFile));

            var service2 = new AppConfigService(isolated2);
            await service2.ImportConfigAsync(tempFile);

            Assert.Equal(@"D:\Games\StarCitizen\LIVE\actionmaps.xml", service2.Config.StarCitizenPath);
            Assert.False(service2.Config.AutoSyncOnUsbChange);
            Assert.True(service2.Config.StartMinimizedToTray);
        } finally {
            if (File.Exists(tempFile)) { File.Delete(tempFile); }
            if (File.Exists(isolated1)) { File.Delete(isolated1); }
            if (File.Exists(isolated2)) { File.Delete(isolated2); }
        }
    }

    [Fact]
    public async Task BackupCurrentSettings_CreatesTimestampedBackup() {
        var isolatedDir = Path.Combine(Path.GetTempPath(), $"sticklock_dir_{Guid.NewGuid():N}");
        var isolatedConfig = Path.Combine(isolatedDir, "config.json");
        try {
            var service = new AppConfigService(isolatedConfig);
            await service.SetStarCitizenPathAsync(@"C:\StarCitizen\LIVE\actionmaps.xml");
            var backupPath = await service.BackupCurrentSettingsAsync();

            Assert.True(File.Exists(backupPath));
            Assert.Contains("settings_backups", backupPath);

            var backups = service.GetSettingsBackups();
            Assert.NotEmpty(backups);
            Assert.Contains(backupPath, backups);
        } finally {
            if (Directory.Exists(isolatedDir)) {
                Directory.Delete(isolatedDir, true);
            }
        }
    }

    [Fact]
    public async Task SetStarCitizenRootFolder_AutoDetectsMultipleInstallsAndXmls() {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"sc_root_{Guid.NewGuid():N}");
        var scDir = Path.Combine(tempRoot, "StarCitizen");
        var isolatedConfig = Path.Combine(tempRoot, ".sticklock", "config.json");
        try {
            // Create LIVE, PTU, and TECH-PREVIEW folders; put actionmaps.xml in LIVE and TECH-PREVIEW
            var liveProfileDir = Path.Combine(scDir, "LIVE", "USER", "Client", "0", "Profiles", "default");
            var ptuDir = Path.Combine(scDir, "PTU");
            var techProfileDir = Path.Combine(scDir, "TECH-PREVIEW", "USER", "Client", "0", "Profiles", "default");

            Directory.CreateDirectory(liveProfileDir);
            Directory.CreateDirectory(ptuDir);
            Directory.CreateDirectory(techProfileDir);

            var liveXml = Path.Combine(liveProfileDir, "actionmaps.xml");
            var techXml = Path.Combine(techProfileDir, "actionmaps.xml");
            await File.WriteAllTextAsync(liveXml, "<ActionMaps/>");
            await File.WriteAllTextAsync(techXml, "<ActionMaps/>");

            var service = new AppConfigService(isolatedConfig);
            await service.SetStarCitizenRootFolderAsync(scDir);

            Assert.Equal(scDir, service.Config.StarCitizenRootPath);
            Assert.Equal(liveXml, service.Config.StarCitizenPath);

            var installs = service.GetDetectedInstalls();
            Assert.Equal(4, installs.Count);

            var live = installs.Single(i => i.Environment == ScEnvironment.LIVE);
            Assert.True(live.IsDirectoryPresent);
            Assert.True(live.HasActionMapsXml);

            var ptu = installs.Single(i => i.Environment == ScEnvironment.PTU);
            Assert.True(ptu.IsDirectoryPresent);
            Assert.False(ptu.HasActionMapsXml);

            var eptu = installs.Single(i => i.Environment == ScEnvironment.EPTU);
            Assert.False(eptu.IsDirectoryPresent);

            var tech = installs.Single(i => i.Environment == ScEnvironment.TechPreview);
            Assert.True(tech.IsDirectoryPresent);
            Assert.True(tech.HasActionMapsXml);
            Assert.Equal(techXml, tech.ActionMapsPath);
        } finally {
            if (Directory.Exists(tempRoot)) {
                Directory.Delete(tempRoot, true);
            }
        }
    }
}

