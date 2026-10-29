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

        var configService = new AppConfigService(Path.Combine(_tempDir, ".sticklock", "config.json"));
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

        var configService = new AppConfigService(Path.Combine(_tempDir, ".sticklock", "config.json"));
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

        var configService = new AppConfigService(Path.Combine(_tempDir, ".sticklock", "config.json"));
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
    public async Task SyncAsync_WithForce_SucceedsAndDeploysToControlsMappings_WhenStarCitizenRunning() {
        var guidStick1 = Guid.NewGuid();
        var guidStick2 = Guid.NewGuid();

        // Star Citizen directory layout
        var scRoot = Path.Combine(_tempDir, "LIVE");
        var clientZero = Path.Combine(scRoot, "USER", "Client", "0");
        var profileDefault = Path.Combine(clientZero, "Profiles", "default");
        Directory.CreateDirectory(profileDefault);

        var scXmlPath = Path.Combine(profileDefault, "actionmaps.xml");
        var originalXml = $@"<ActionMaps version=""1"">
            <options type=""joystick"" instance=""1"" Product=""VKB Right {{{guidStick1}}}""/>
            <options type=""joystick"" instance=""2"" Product=""VKB Left {{{guidStick2}}}""/>
            <actionmap name=""spaceship_general"">
                <action name=""fire""><rebind input=""js1_button1""/></action>
            </actionmap>
        </ActionMaps>";
        await File.WriteAllTextAsync(scXmlPath, originalXml);

        var customConfigPath = Path.Combine(_tempDir, ".sticklock", "config.json");
        var configService = new AppConfigService(customConfigPath);
        await configService.SetStarCitizenPathAsync(scXmlPath);
        await configService.SetAutoDeployToMappingsFolderAsync(true);

        // Windows has them reversed: Left is slot 1, Right is slot 2
        var diService = new TestDirectInputService();
        diService.SetDevices(new List<DirectInputDevice> {
            new() { SlotIndex = 1, ProductName = "VKB Left", InstanceGuid = guidStick2, ProductGuid = guidStick2 },
            new() { SlotIndex = 2, ProductName = "VKB Right", InstanceGuid = guidStick1, ProductGuid = guidStick1 }
        });

        // Set canonical sticks
        await configService.SetCanonicalStickAsync(1, new DirectInputDevice { SlotIndex = 2, ProductName = "VKB Right", InstanceGuid = guidStick1, ProductGuid = guidStick1 }, "Right Stick");
        await configService.SetCanonicalStickAsync(2, new DirectInputDevice { SlotIndex = 1, ProductName = "VKB Left", InstanceGuid = guidStick2, ProductGuid = guidStick2 }, "Left Stick");

        var detector = new TestStarCitizenDetector { Running = true };
        var logService = new ActivityLogService();
        var xmlService = new ActionMapsXmlService(configService, diService, detector, logService);

        // 1. Without force and without config setting, sync is blocked
        var blockedResult = await xmlService.SyncAsync(force: false);
        Assert.False(blockedResult.Success);
        Assert.Equal(SyncStatus.StarCitizenActive, blockedResult.Status);

        // 2. With force = true, sync succeeds even while game is running
        var forceResult = await xmlService.SyncAsync(force: true);
        Assert.True(forceResult.Success);
        Assert.True(forceResult.ChangesMade);

        // 3. Verify actionmaps.xml on disk was updated
        var updatedXml = await File.ReadAllTextAsync(scXmlPath);
        Assert.Contains(@"instance=""1"" Product=""VKB Left", updatedXml);
        Assert.Contains(@"instance=""2"" Product=""VKB Right", updatedXml);

        // 4. Verify layout_StickLock_Active.xml was deployed to Controls\Mappings
        var deployedProfile = Path.Combine(clientZero, "Controls", "Mappings", "layout_StickLock_Active.xml");
        Assert.True(File.Exists(deployedProfile));
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

        var configService = new AppConfigService(Path.Combine(_tempDir, ".sticklock", "config.json"));
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

    [Fact]
    public async Task CheckAlignment_DetectsMisalignment_And_FixAndSync_UpdatesGameAndBacksUp() {
        var guidStick1 = Guid.NewGuid();
        var guidStick2 = Guid.NewGuid();

        // Star Citizen actionmaps.xml has Stick1 as js1 and Stick2 as js2
        var xmlContent = $@"<ActionMaps version=""1"">
            <options type=""joystick"" instance=""1"" Product=""VKB Gladiator Right {{{guidStick1}}}""/>
            <options type=""joystick"" instance=""2"" Product=""VKB Gladiator Left {{{guidStick2}}}""/>
            <actionmap name=""spaceship_general"">
                <action name=""v_pitch""><rebind input=""js1_pitch""/></action>
                <action name=""v_yaw""><rebind input=""js2_yaw""/></action>
            </actionmap>
        </ActionMaps>";
        await File.WriteAllTextAsync(_xmlPath, xmlContent);

        var customConfigPath = Path.Combine(_tempDir, ".sticklock", "config.json");
        var configService = new AppConfigService(customConfigPath);
        await configService.SetStarCitizenPathAsync(_xmlPath);

        // Windows currently has them reversed: Stick2 is slot 1, Stick1 is slot 2
        var diService = new TestDirectInputService();
        diService.SetDevices(new List<DirectInputDevice> {
            new() { SlotIndex = 1, ProductName = "VKB Gladiator Left", InstanceGuid = guidStick2, ProductGuid = guidStick2 },
            new() { SlotIndex = 2, ProductName = "VKB Gladiator Right", InstanceGuid = guidStick1, ProductGuid = guidStick1 }
        });

        var detector = new TestStarCitizenDetector();
        var logService = new ActivityLogService();
        var xmlService = new ActionMapsXmlService(configService, diService, detector, logService);

        // 1. Check Alignment without canonical sticks: should detect existing SC mappings are flipped
        var alignment = await xmlService.CheckAlignmentAsync();
        Assert.True(alignment.IsMisaligned);
        Assert.Equal(SyncStatus.NeedsSync, alignment.Status);
        Assert.True(alignment.CanAutoFix);
        Assert.Equal(2, alignment.Mismatches.Count);
        Assert.True(alignment.Mismatches[0].IsFlipped);
        Assert.True(alignment.Mismatches[1].IsFlipped);

        // 2. Fix and Sync: should automatically back up, swap slots, and update actionmaps.xml
        var fixResult = await xmlService.FixAndSyncAsync();
        Assert.True(fixResult.Success);
        Assert.True(fixResult.ChangesMade);
        Assert.NotNull(fixResult.BackupPath);
        Assert.True(File.Exists(fixResult.BackupPath));

        // 3. Verify backup is saved in .sticklock/backups
        Assert.Contains(".sticklock", fixResult.BackupPath);
        Assert.Contains("backups", fixResult.BackupPath);

        // 4. Verify Star Citizen XML was updated so Stick2 (slot 1) is now js1, Stick1 (slot 2) is now js2
        var updatedXml = await File.ReadAllTextAsync(_xmlPath);
        Assert.Contains(@"instance=""1"" Product=""VKB Gladiator Left", updatedXml);
        Assert.Contains(@"instance=""2"" Product=""VKB Gladiator Right", updatedXml);

        // 5. Re-check alignment: should now be Aligned!
        alignment = await xmlService.CheckAlignmentAsync();
        Assert.False(alignment.IsMisaligned);
        Assert.Equal(SyncStatus.Aligned, alignment.Status);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(1, 4)]
    [InlineData(2, 3)]
    public async Task GetConsoleResortCommand_ComputesDynamicSlots_ForArbitraryIndices(int slotA, int slotB) {
        var guidA = Guid.NewGuid();
        var guidB = Guid.NewGuid();

        // XML has Device A on slotA, Device B on slotB
        var xmlContent = $@"<ActionMaps version=""1"">
            <options type=""joystick"" instance=""{slotA}"" Product=""Controller Alpha {{{guidA}}}""/>
            <options type=""joystick"" instance=""{slotB}"" Product=""Controller Beta {{{guidB}}}""/>
            <actionmap name=""spaceship_general"">
                <action name=""v_pitch""><rebind input=""js{slotA}_pitch""/></action>
            </actionmap>
        </ActionMaps>";
        await File.WriteAllTextAsync(_xmlPath, xmlContent);

        var customConfigPath = Path.Combine(_tempDir, ".sticklock", "config.json");
        var configService = new AppConfigService(customConfigPath);
        await configService.SetStarCitizenPathAsync(_xmlPath);

        // Windows DirectInput has them flipped: Device A is slotB, Device B is slotA
        var diService = new TestDirectInputService();
        diService.SetDevices(new List<DirectInputDevice> {
            new() { SlotIndex = slotB, ProductName = "Controller Alpha", InstanceGuid = guidA, ProductGuid = guidA },
            new() { SlotIndex = slotA, ProductName = "Controller Beta", InstanceGuid = guidB, ProductGuid = guidB }
        });

        var detector = new TestStarCitizenDetector();
        var logService = new ActivityLogService();
        var xmlService = new ActionMapsXmlService(configService, diService, detector, logService);

        var command = await xmlService.GetConsoleResortCommandAsync();
        var min = Math.Min(slotA, slotB);
        var max = Math.Max(slotA, slotB);

        Assert.Equal($"pp_resortdevices joystick {min} {max}", command);
    }

    [Fact]
    public async Task SyncAsync_And_Propagate_UpdatesMultipleDetectedInstalls() {
        var guidRight = Guid.NewGuid();
        var guidLeft = Guid.NewGuid();

        var scRoot = Path.Combine(_tempDir, "StarCitizen");
        var liveProfileDir = Path.Combine(scRoot, "LIVE", "USER", "Client", "0", "Profiles", "default");
        var ptuProfileDir = Path.Combine(scRoot, "PTU", "USER", "Client", "0", "Profiles", "default");
        var techDir = Path.Combine(scRoot, "TECH-PREVIEW");

        Directory.CreateDirectory(liveProfileDir);
        Directory.CreateDirectory(ptuProfileDir);
        Directory.CreateDirectory(techDir);

        var xmlTemplate = $@"<ActionMaps version=""1"">
            <options type=""joystick"" instance=""1"" Product=""VKB Right {{{guidRight}}}""/>
            <options type=""joystick"" instance=""2"" Product=""VKB Left {{{guidLeft}}}""/>
            <actionmap name=""flight"">
                <action name=""fire""><rebind input=""js1_button1""/></action>
            </actionmap>
        </ActionMaps>";

        var liveXml = Path.Combine(liveProfileDir, "actionmaps.xml");
        var ptuXml = Path.Combine(ptuProfileDir, "actionmaps.xml");
        await File.WriteAllTextAsync(liveXml, xmlTemplate);
        await File.WriteAllTextAsync(ptuXml, xmlTemplate);

        var customConfigPath = Path.Combine(_tempDir, ".sticklock", "config.json");
        var configService = new AppConfigService(customConfigPath);
        await configService.SetStarCitizenRootFolderAsync(scRoot);
        await configService.SetSyncAllDetectedInstallsAsync(true);

        await configService.SetCanonicalStickAsync(1, new DirectInputDevice { SlotIndex = 2, ProductName = "VKB Right", InstanceGuid = guidRight, ProductGuid = guidRight }, "Right Stick");
        await configService.SetCanonicalStickAsync(2, new DirectInputDevice { SlotIndex = 1, ProductName = "VKB Left", InstanceGuid = guidLeft, ProductGuid = guidLeft }, "Left Stick");

        // Windows has Left on slot 1, Right on slot 2
        var diService = new TestDirectInputService();
        diService.SetDevices(new List<DirectInputDevice> {
            new() { SlotIndex = 1, ProductName = "VKB Left", InstanceGuid = guidLeft, ProductGuid = guidLeft },
            new() { SlotIndex = 2, ProductName = "VKB Right", InstanceGuid = guidRight, ProductGuid = guidRight }
        });

        var detector = new TestStarCitizenDetector();
        var logService = new ActivityLogService();
        var xmlService = new ActionMapsXmlService(configService, diService, detector, logService);

        // 1. SyncAsync should align both LIVE and PTU simultaneously
        var syncResult = await xmlService.SyncAsync();
        Assert.True(syncResult.Success);
        Assert.True(syncResult.ChangesMade);

        var liveUpdated = await File.ReadAllTextAsync(liveXml);
        var ptuUpdated = await File.ReadAllTextAsync(ptuXml);
        Assert.Contains(@"instance=""1"" Product=""VKB Left", liveUpdated);
        Assert.Contains(@"instance=""1"" Product=""VKB Left", ptuUpdated);

        // 2. PropagateActiveToDetectedInstallsAsync should also populate TECH-PREVIEW's actionmaps.xml
        var propResult = await xmlService.PropagateActiveToDetectedInstallsAsync();
        Assert.True(propResult.Success);

        var techXml = Path.Combine(techDir, "USER", "Client", "0", "Profiles", "default", "actionmaps.xml");
        Assert.True(File.Exists(techXml));
        var techContent = await File.ReadAllTextAsync(techXml);
        Assert.Contains(@"instance=""1"" Product=""VKB Left", techContent);
    }

    [Fact]
    public async Task AutoLockAndSync_IgnoresEmptyOptions_And_SwapsJs1AndJs3_WhenSticksAreJs1AndJs4() {
        var guidRight = Guid.Parse("0224231d-0000-0000-0000-504944564944");
        var guidLink = Guid.Parse("d0303434-0000-0000-0000-504944564944");
        var guidK8 = Guid.Parse("0e803434-0000-0000-0000-504944564944");
        var guidLeft = Guid.Parse("3201231d-0000-0000-0000-504944564944");

        var xmlContent = $@"<ActionMaps version=""1"">
            <options type=""joystick"" instance=""1"" Product="" VKBsim Gladiator EVO R SEM FSM.GA {{{guidRight}}}""/>
            <options type=""joystick"" instance=""2"" Product=""Keychron Link  {{{guidLink}}}""/>
            <options type=""joystick"" instance=""3"" Product=""Keychron K8 HE {{{guidK8}}}""/>
            <options type=""joystick"" instance=""4"" Product="" VKBsim Gladiator EVO OT L  {{{guidLeft}}}""/>
            <options type=""joystick"" instance=""5""/>
            <options type=""joystick"" instance=""6""/>
            <options type=""joystick"" instance=""7""/>
            <options type=""joystick"" instance=""8""/>
            <actionmap name=""spaceship_movement"">
                <action name=""v_pitch""><rebind input=""js1_y""/></action>
                <action name=""v_strafe_longitudinal""><rebind input=""js4_y""/></action>
            </actionmap>
        </ActionMaps>";
        await File.WriteAllTextAsync(_xmlPath, xmlContent);

        var customConfigPath = Path.Combine(_tempDir, ".sticklock", "config.json");
        var configService = new AppConfigService(customConfigPath);
        await configService.SetStarCitizenPathAsync(_xmlPath);

        // Windows DirectInput has K8 HE at js1, Link at js2, VKB Right at js3, VKB Left at js4
        var diService = new TestDirectInputService();
        diService.SetDevices(new List<DirectInputDevice> {
            new() { SlotIndex = 1, ProductName = "Keychron K8 HE", InstanceGuid = guidK8, ProductGuid = guidK8 },
            new() { SlotIndex = 2, ProductName = "Keychron Link", InstanceGuid = guidLink, ProductGuid = guidLink },
            new() { SlotIndex = 3, ProductName = "VKBsim Gladiator EVO R SEM FSM.GA", InstanceGuid = guidRight, ProductGuid = guidRight },
            new() { SlotIndex = 4, ProductName = "VKBsim Gladiator EVO OT L", InstanceGuid = guidLeft, ProductGuid = guidLeft }
        });

        var detector = new TestStarCitizenDetector();
        var logService = new ActivityLogService();
        var xmlService = new ActionMapsXmlService(configService, diService, detector, logService);

        // 1. Parse mappings should ignore empty instance 5..8
        var mappings = await xmlService.GetXmlMappingsAsync();
        Assert.Equal(4, mappings.Count);

        // 2. AutoLockAndAlignFromXmlAsync should lock ONLY js1 (VKB Right) and js4 (VKB Left) that have rebinds, and sync js1 <-> js3
        var result = await xmlService.AutoLockAndAlignFromXmlAsync();
        Assert.True(result.Success);
        Assert.Equal(2, configService.Config.CanonicalSticks.Count);
        Assert.Contains(configService.Config.CanonicalSticks, s => s.RoleSlot == 1 && s.ProductName.Contains("EVO R"));
        Assert.Contains(configService.Config.CanonicalSticks, s => s.RoleSlot == 4 && s.ProductName.Contains("EVO OT L"));

        // 3. Verify XML was updated: VKB Right is now instance 3 (js3_y), Keychron K8 HE is instance 1, VKB Left stays instance 4 (js4_y)
        var doc = XDocument.Load(_xmlPath);
        var pitchRebind = doc.Descendants("action").First(a => (string?)a.Attribute("name") == "v_pitch").Element("rebind");
        var strafeRebind = doc.Descendants("action").First(a => (string?)a.Attribute("name") == "v_strafe_longitudinal").Element("rebind");
        Assert.Equal("js3_y", (string?)pitchRebind?.Attribute("input"));
        Assert.Equal("js4_y", (string?)strafeRebind?.Attribute("input"));

        var alignmentAfter = await xmlService.CheckAlignmentAsync();
        Assert.False(alignmentAfter.IsMisaligned);
        Assert.Equal(SyncStatus.Aligned, alignmentAfter.Status);
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

