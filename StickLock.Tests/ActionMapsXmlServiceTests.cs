using System.Xml.Linq;
using StickLock.Models;
using StickLock.Services;

namespace StickLock.Tests;

public class ActionMapsXmlServiceTests : IDisposable {
    private readonly string _tempDir;
    private readonly string _xmlPath;

    public ActionMapsXmlServiceTests() {
        _tempDir = Path.Combine(Path.GetTempPath(), "StickLockTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _xmlPath = Path.Combine(_tempDir, "actionmaps.xml");
    }

    [Fact]
    public async Task ParseMappings_ExtractsJoystickNodesAndGuids() {
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        var xmlContent = $@"<ActionMaps version=""1"">
            <options type=""joystick"" instance=""1"" Product=""VKB Gladiator EVO R {{{guid1}}}""/>
            <options type=""joystick"" instance=""2"" Product=""VKB Gladiator EVO L {{{guid2}}}""/>
            <actionmap name=""spaceship_general"">
                <action name=""v_attack1_group1"">
                    <rebind input=""js1_button1""/>
                </action>
                <action name=""v_throttle"">
                    <rebind input=""js2_throttle""/>
                </action>
            </actionmap>
        </ActionMaps>";
        await File.WriteAllTextAsync(_xmlPath, xmlContent);

        var configService = new AppConfigService();
        await configService.SetStarCitizenPathAsync(_xmlPath);

        var diService = new TestDirectInputService();
        var detector = new TestStarCitizenDetector();
        var logService = new ActivityLogService();

        var xmlService = new ActionMapsXmlService(configService, diService, detector, logService);
        var mappings = await xmlService.GetXmlMappingsAsync();

        Assert.Equal(2, mappings.Count);
        Assert.Equal(1, mappings[0].Instance);
        Assert.Equal(guid1, mappings[0].ExtractedGuid);
        Assert.Equal(2, mappings[1].Instance);
        Assert.Equal(guid2, mappings[1].ExtractedGuid);
    }

    [Fact]
    public async Task SyncAsync_SwapsInstancesAndRebinds_WhenOrderFlipped() {
        var guidStick1 = Guid.NewGuid(); // Right stick
        var guidStick2 = Guid.NewGuid(); // Left stick

        // XML originally had stick 1 as js1, stick 2 as js2
        var originalXml = $@"<ActionMaps version=""1"">
            <options type=""joystick"" instance=""1"" Product=""VKB Right {{{guidStick1}}}""/>
            <options type=""joystick"" instance=""2"" Product=""VKB Left {{{guidStick2}}}""/>
            <actionmap name=""flight"">
                <action name=""fire_primary"">
                    <rebind input=""js1_button1""/>
                </action>
                <action name=""strafe_vertical"">
                    <rebind input=""js2_throttle""/>
                </action>
            </actionmap>
        </ActionMaps>";
        await File.WriteAllTextAsync(_xmlPath, originalXml);

        var configService = new AppConfigService();
        await configService.SetStarCitizenPathAsync(_xmlPath);
        // Canonical configuration: Stick 1 = slot 1 (guidStick1), Stick 2 = slot 2 (guidStick2)
        await configService.SetCanonicalStickAsync(1, new DirectInputDevice {
            SlotIndex = 1,
            InstanceGuid = guidStick1,
            ProductGuid = Guid.NewGuid(),
            ProductName = "VKB Right"
        }, "Right Stick");

        await configService.SetCanonicalStickAsync(2, new DirectInputDevice {
            SlotIndex = 2,
            InstanceGuid = guidStick2,
            ProductGuid = Guid.NewGuid(),
            ProductName = "VKB Left"
        }, "Left Stick");

        // Windows DirectInput is NOW FLIPPED:
        // Slot 1 has Left Stick (guidStick2)
        // Slot 2 has Right Stick (guidStick1)
        var diService = new TestDirectInputService();
        diService.SetDevices(new List<DirectInputDevice> {
            new DirectInputDevice { SlotIndex = 1, InstanceGuid = guidStick2, ProductName = "VKB Left" },
            new DirectInputDevice { SlotIndex = 2, InstanceGuid = guidStick1, ProductName = "VKB Right" }
        });

        var detector = new TestStarCitizenDetector();
        var logService = new ActivityLogService();
        var xmlService = new ActionMapsXmlService(configService, diService, detector, logService);

        var statusBefore = await xmlService.CheckSyncStatusAsync();
        Assert.Equal(SyncStatus.NeedsSync, statusBefore);

        var syncResult = await xmlService.SyncAsync();
        Assert.True(syncResult.Success);
        Assert.True(syncResult.ChangesMade);
        Assert.NotNull(syncResult.BackupPath);
        Assert.True(File.Exists(syncResult.BackupPath));

        // Verify the updated XML
        var doc = XDocument.Load(_xmlPath);
        var options = doc.Descendants("options").ToList();
        var optRight = options.First(o => ((string?)o.Attribute("Product") ?? "").Contains("VKB Right"));
        var optLeft = options.First(o => ((string?)o.Attribute("Product") ?? "").Contains("VKB Left"));

        // Right stick was instance 1, but is now in Windows slot 2, so XML instance must be 2
        Assert.Equal("2", (string?)optRight.Attribute("instance"));
        // Left stick was instance 2, but is now in Windows slot 1, so XML instance must be 1
        Assert.Equal("1", (string?)optLeft.Attribute("instance"));

        // Rebind for fire_primary (originally js1_button1) should now be js2_button1
        var fireAction = doc.Descendants("action").First(a => (string?)a.Attribute("name") == "fire_primary");
        var fireRebind = fireAction.Element("rebind");
        Assert.Equal("js2_button1", (string?)fireRebind?.Attribute("input"));

        // Rebind for strafe_vertical (originally js2_throttle) should now be js1_throttle
        var strafeAction = doc.Descendants("action").First(a => (string?)a.Attribute("name") == "strafe_vertical");
        var strafeRebind = strafeAction.Element("rebind");
        Assert.Equal("js1_throttle", (string?)strafeRebind?.Attribute("input"));

        // Now status should be Aligned
        var statusAfter = await xmlService.CheckSyncStatusAsync();
        Assert.Equal(SyncStatus.Aligned, statusAfter);

        // Test restore
        var restoreResult = await xmlService.RestoreLatestBackupAsync();
        Assert.True(restoreResult.Success);

        var restoredDoc = XDocument.Load(_xmlPath);
        var restoredOptions = restoredDoc.Descendants("options").ToList();
        var restoredRight = restoredOptions.First(o => ((string?)o.Attribute("Product") ?? "").Contains("VKB Right"));
        Assert.Equal("1", (string?)restoredRight.Attribute("instance"));
    }

    [Fact]
    public async Task SyncAsync_BlocksSync_WhenStarCitizenRunning() {
        var xmlContent = @"<ActionMaps version=""1""></ActionMaps>";
        await File.WriteAllTextAsync(_xmlPath, xmlContent);

        var configService = new AppConfigService();
        await configService.SetStarCitizenPathAsync(_xmlPath);

        var diService = new TestDirectInputService();
        var detector = new TestStarCitizenDetector { Running = true };
        var logService = new ActivityLogService();

        var xmlService = new ActionMapsXmlService(configService, diService, detector, logService);
        var result = await xmlService.SyncAsync();

        Assert.False(result.Success);
        Assert.Equal(SyncStatus.StarCitizenActive, result.Status);
    }

    [Fact]
    public async Task ExportAndImport_PreservesAndAppliesProfile() {
        var guid1 = Guid.NewGuid();
        var originalXml = $@"<ActionMaps version=""1"">
            <options type=""joystick"" instance=""1"" Product=""Gladiator EVO {{{guid1}}}""/>
            <actionmap name=""spaceship_general"">
                <action name=""fire""><rebind input=""js1_button1""/></action>
            </actionmap>
        </ActionMaps>";
        await File.WriteAllTextAsync(_xmlPath, originalXml);

        var configService = new AppConfigService();
        await configService.SetStarCitizenPathAsync(_xmlPath);

        var diService = new TestDirectInputService();
        var detector = new TestStarCitizenDetector();
        var logService = new ActivityLogService();
        var xmlService = new ActionMapsXmlService(configService, diService, detector, logService);

        // 1. Export
        var exportPath = Path.Combine(_tempDir, "custom_export_layout.xml");
        var exportResult = await xmlService.ExportActiveXmlAsync(exportPath);
        Assert.True(exportResult.Success);
        Assert.True(File.Exists(exportPath));

        // 2. Inspect
        var summary = await xmlService.InspectXmlProfileAsync(exportPath);
        Assert.True(summary.IsValid);
        Assert.Equal(1, summary.JoystickOptionsCount);
        Assert.Equal(1, summary.RebindCount);

        // 3. Import new profile over active XML
        var newProfilePath = Path.Combine(_tempDir, "imported_profile.xml");
        var newXml = @"<ActionMaps version=""1"">
            <options type=""joystick"" instance=""1"" Product=""Thrustmaster Warthog""/>
            <actionmap name=""flight"">
                <action name=""flare""><rebind input=""js1_button2""/></action>
            </actionmap>
        </ActionMaps>";
        await File.WriteAllTextAsync(newProfilePath, newXml);

        var importResult = await xmlService.ImportAndApplyXmlAsync(newProfilePath);
        Assert.True(importResult.Success);

        // Verify active XML content changed to the imported one
        var activeText = await File.ReadAllTextAsync(_xmlPath);
        Assert.Contains("Thrustmaster Warthog", activeText);
        Assert.Contains("flare", activeText);

        // Verify backup of original was created during import
        var backups = xmlService.GetAvailableBackups();
        Assert.NotEmpty(backups);
    }

    [Fact]
    public async Task SaveActiveAsProfile_And_CopySyncToStarCitizen_WorksCleanly() {
        // Construct realistic Star Citizen directory hierarchy
        var scRoot = Path.Combine(_tempDir, "LIVE");
        var clientZero = Path.Combine(scRoot, "USER", "Client", "0");
        var profileDefault = Path.Combine(clientZero, "Profiles", "default");
        Directory.CreateDirectory(profileDefault);

        var scXmlPath = Path.Combine(profileDefault, "actionmaps.xml");
        var xmlContent = @"<ActionMaps version=""1"">
            <options type=""joystick"" instance=""1"" Product=""VKB Gladiator EVO R""/>
            <actionmap name=""spaceship_general"">
                <action name=""v_attack1_group1""><rebind input=""js1_button1""/></action>
            </actionmap>
        </ActionMaps>";
        await File.WriteAllTextAsync(scXmlPath, xmlContent);

        // Dedicated custom config path to isolate test's .sticklock folder
        var customConfigPath = Path.Combine(_tempDir, ".sticklock", "config.json");
        var configService = new AppConfigService(customConfigPath);
        await configService.SetStarCitizenPathAsync(scXmlPath);

        var diService = new TestDirectInputService();
        var detector = new TestStarCitizenDetector();
        var logService = new ActivityLogService();
        var xmlService = new ActionMapsXmlService(configService, diService, detector, logService);

        // 1. Verify Star Citizen Mappings directory is correctly resolved
        var expectedScMappingsDir = Path.Combine(clientZero, "Controls", "Mappings");
        var resolvedScMappingsDir = xmlService.GetStarCitizenMappingsDirectory();
        Assert.Equal(expectedScMappingsDir, resolvedScMappingsDir);

        // 2. Save active mapping as profile in .sticklock library
        var saveResult = await xmlService.SaveActiveAsProfileAsync("Dual_VKB_Combat_v1");
        Assert.True(saveResult.Success);

        var savedFile = Path.Combine(configService.MappingsDirectory, "Dual_VKB_Combat_v1.xml");
        Assert.True(File.Exists(savedFile));

        var savedProfiles = await xmlService.GetSavedMappingsAsync();
        Assert.Single(savedProfiles);
        Assert.Equal("Dual_VKB_Combat_v1.xml", savedProfiles[0].FileName);
        Assert.Equal(1, savedProfiles[0].JoystickOptionsCount);
        Assert.False(savedProfiles[0].ExistsInStarCitizen);

        // 3. Deploy / Copy to Star Citizen Controls\Mappings folder
        var copyResult = await xmlService.CopyProfileToStarCitizenAsync("Dual_VKB_Combat_v1.xml");
        Assert.True(copyResult.Success);

        var scCopiedFile = Path.Combine(expectedScMappingsDir, "Dual_VKB_Combat_v1.xml");
        Assert.True(File.Exists(scCopiedFile));

        // Re-check saved profiles: now ExistsInStarCitizen should be true!
        savedProfiles = await xmlService.GetSavedMappingsAsync();
        Assert.True(savedProfiles[0].ExistsInStarCitizen);

        // 4. Simulate user adding/editing an in-game profile in Star Citizen, then pull into .sticklock
        var inGameProfile = Path.Combine(expectedScMappingsDir, "InGame_Custom_HOTAS.xml");
        await File.WriteAllTextAsync(inGameProfile, xmlContent);

        var pullResult = await xmlService.PullProfilesFromStarCitizenAsync();
        Assert.True(pullResult.Success);

        var pulledFileInStickLock = Path.Combine(configService.MappingsDirectory, "InGame_Custom_HOTAS.xml");
        Assert.True(File.Exists(pulledFileInStickLock));

        var allProfiles = await xmlService.GetSavedMappingsAsync();
        Assert.Equal(2, allProfiles.Count);

        // 5. Delete profile from .sticklock library
        var deleteResult = await xmlService.DeleteSavedProfileAsync("InGame_Custom_HOTAS.xml");
        Assert.True(deleteResult);
        Assert.False(File.Exists(pulledFileInStickLock));
    }

    public void Dispose() {
        try {
            if (Directory.Exists(_tempDir)) {
                Directory.Delete(_tempDir, true);
            }
        } catch {
            // Ignore temp cleanup errors
        }
    }
}

