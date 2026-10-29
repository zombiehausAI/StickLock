using System.Text.RegularExpressions;
using System.Xml.Linq;
using StickLock.Models;

namespace StickLock.Services;

public class ActionMapsXmlService : IActionMapsXmlService {
    private readonly IAppConfigService _configService;
    private readonly IDirectInputService _directInputService;
    private readonly IStarCitizenDetector _starCitizenDetector;
    private readonly IActivityLogService _logService;
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    private static readonly Regex GuidRegex = new(
        @"\{([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\}",
        RegexOptions.Compiled);

    public ActionMapsXmlService(
        IAppConfigService configService,
        IDirectInputService directInputService,
        IStarCitizenDetector starCitizenDetector,
        IActivityLogService logService) {
        _configService = configService;
        _directInputService = directInputService;
        _starCitizenDetector = starCitizenDetector;
        _logService = logService;
    }

    public async Task<SyncStatus> CheckSyncStatusAsync() {
        if (_starCitizenDetector.IsGameRunning()) {
            return SyncStatus.StarCitizenActive;
        }

        var xmlPath = _configService.Config.StarCitizenPath;
        if (string.IsNullOrWhiteSpace(xmlPath) || !File.Exists(xmlPath)) {
            return SyncStatus.FileNotFound;
        }

        var canonical = _configService.Config.CanonicalSticks;
        var liveDevices = _directInputService.CurrentDevices;
        if (canonical.Count == 0) {
            var xml = await GetXmlMappingsAsync();
            if (xml.Count >= 2 && liveDevices.Count >= 2) {
                var alignment = await CheckAlignmentAsync();
                return alignment.IsMisaligned ? SyncStatus.NeedsSync : SyncStatus.Aligned;
            }
            return SyncStatus.Unknown;
        }

        foreach (var stick in canonical) {
            var found = liveDevices.Any(d => IsStickMatch(stick, d));
            if (!found) {
                return SyncStatus.SticksDisconnected;
            }
        }

        var xmlMappings = await GetXmlMappingsAsync();
        if (xmlMappings.Count == 0) {
            return SyncStatus.Unknown;
        }

        var needsSync = CheckIfNeedsSync(canonical, liveDevices, xmlMappings);
        return needsSync ? SyncStatus.NeedsSync : SyncStatus.Aligned;
    }

    public async Task<MappingAlignmentCheck> CheckAlignmentAsync() {
        var check = new MappingAlignmentCheck();
        check.IsGameRunning = _starCitizenDetector.IsGameRunning();

        var xmlPath = _configService.Config.StarCitizenPath;
        if (string.IsNullOrWhiteSpace(xmlPath) || !File.Exists(xmlPath)) {
            check.Status = SyncStatus.FileNotFound;
            check.StatusMessage = "actionmaps.xml not found. Please verify Star Citizen path.";
            return check;
        }

        var xmlMappings = await GetXmlMappingsAsync();
        check.ExistingXmlDevices = xmlMappings.ToList();

        var canonical = _configService.Config.CanonicalSticks;
        check.HasCanonicalConfigured = canonical.Count >= 2;

        var liveDevices = _directInputService.CurrentDevices;

        if (canonical.Count >= 2) {
            var missing = canonical.Where(c => !liveDevices.Any(d => IsStickMatch(c, d))).ToList();
            if (missing.Count > 0) {
                check.Status = SyncStatus.SticksDisconnected;
                check.StatusMessage = $"Disconnected flight stick(s): {string.Join(", ", missing.Select(m => m.FriendlyRoleName))}";
                return check;
            }

            foreach (var stick in canonical) {
                var live = liveDevices.FirstOrDefault(d => IsStickMatch(stick, d));
                var xmlEntry = xmlMappings.FirstOrDefault(x =>
                    (stick.InstanceGuid != Guid.Empty && x.ExtractedGuid == stick.InstanceGuid) ||
                    (stick.ProductGuid != Guid.Empty && x.ExtractedGuid == stick.ProductGuid))
                    ?? xmlMappings.FirstOrDefault(x => x.Instance == stick.RoleSlot);

                var liveSlot = live?.SlotIndex ?? 0;
                var xmlInst = xmlEntry?.Instance ?? stick.RoleSlot;

                check.Mismatches.Add(new StickMismatchDetail {
                    DeviceName = stick.FriendlyRoleName,
                    XmlInstance = xmlInst,
                    LiveWindowsSlot = liveSlot
                });

                if (xmlInst != liveSlot) {
                    check.IsMisaligned = true;
                }
            }
        } else if (xmlMappings.Count >= 2) {
            foreach (var xmlEntry in xmlMappings) {
                var live = liveDevices.FirstOrDefault(d =>
                    (xmlEntry.ExtractedGuid != Guid.Empty && d.InstanceGuid == xmlEntry.ExtractedGuid) ||
                    (xmlEntry.ExtractedGuid != Guid.Empty && d.ProductGuid == xmlEntry.ExtractedGuid) ||
                    string.Equals(d.ProductName, xmlEntry.ProductName, StringComparison.OrdinalIgnoreCase));

                if (live != null) {
                    var isFlipped = xmlEntry.Instance != live.SlotIndex;
                    check.Mismatches.Add(new StickMismatchDetail {
                        DeviceName = live.ProductName,
                        XmlInstance = xmlEntry.Instance,
                        LiveWindowsSlot = live.SlotIndex
                    });

                    if (isFlipped) {
                        check.IsMisaligned = true;
                    }
                }
            }
        }

        check.InGameResortCommand = await GetConsoleResortCommandAsync();

        if (check.IsMisaligned) {
            check.Status = SyncStatus.NeedsSync;
            check.CanAutoFix = !check.IsGameRunning || _configService.Config.AllowSyncWhileGameRunning;
            check.StatusMessage = check.IsGameRunning
                ? "Star Citizen is running and joystick slot order is inverted! Use in-game console command or Force Sync."
                : "Star Citizen joystick slot assignments do not match current Windows DirectInput enumeration order!";
        } else if (canonical.Count >= 2 || (xmlMappings.Count >= 2 && check.Mismatches.Count >= 2)) {
            check.Status = check.IsGameRunning ? SyncStatus.StarCitizenActive : SyncStatus.Aligned;
            check.StatusMessage = check.IsGameRunning
                ? "Star Citizen is running. Sticks are currently aligned."
                : "All flight sticks are correctly aligned with Star Citizen mappings.";
        } else {
            check.Status = SyncStatus.Unknown;
            check.StatusMessage = "Awaiting flight stick configuration or device connection.";
        }

        var backups = GetAvailableBackups();
        if (backups.Count > 0) {
            check.LatestBackupPath = backups[0];
        }

        return check;
    }

    public async Task<SyncResult> FixAndSyncAsync(bool force = false) {
        var canonical = _configService.Config.CanonicalSticks;
        if (canonical.Count < 2) {
            return await AutoLockAndAlignFromXmlAsync(force);
        }

        return await SyncAsync(force);
    }

    public async Task<SyncResult> AutoLockAndAlignFromXmlAsync(bool force = false) {
        var xmlMappings = await GetXmlMappingsAsync();
        if (xmlMappings.Count == 0) {
            return SyncResult.Fail("No joystick mappings found in Star Citizen actionmaps.xml.");
        }

        var liveDevices = await _directInputService.RefreshDevicesAsync();
        var added = 0;

        foreach (var xml in xmlMappings) {
            var live = liveDevices.FirstOrDefault(d =>
                (xml.ExtractedGuid != Guid.Empty && d.InstanceGuid == xml.ExtractedGuid) ||
                (xml.ExtractedGuid != Guid.Empty && d.ProductGuid == xml.ExtractedGuid) ||
                string.Equals(d.ProductName, xml.ProductName, StringComparison.OrdinalIgnoreCase));

            var stickName = live != null ? live.ProductName : (!string.IsNullOrWhiteSpace(xml.ProductName) ? xml.ProductName : $"Joystick {xml.Instance}");
            var friendlyRole = xml.Instance == 1 ? $"Stick 1 ({stickName})" : (xml.Instance == 2 ? $"Stick 2 ({stickName})" : $"Stick {xml.Instance} ({stickName})");

            var deviceToAssign = live ?? new DirectInputDevice {
                SlotIndex = xml.Instance,
                ProductName = stickName,
                InstanceGuid = xml.ExtractedGuid,
                ProductGuid = xml.ExtractedGuid
            };

            await _configService.SetCanonicalStickAsync(xml.Instance, deviceToAssign, friendlyRole);
            added++;
        }

        _logService.LogSuccess($"Auto-locked {added} canonical joystick assignment(s) from Star Citizen XML.");

        var status = await CheckSyncStatusAsync();
        if (status == SyncStatus.NeedsSync) {
            return await SyncAsync(force);
        }

        return SyncResult.Ok($"Locked {added} joystick(s) from Star Citizen. Devices are aligned with current Windows order.", changesMade: false, status: SyncStatus.Aligned);
    }

    public async Task<SyncResult> SyncAsync(bool force = false) {
        await _syncLock.WaitAsync();
        try {
            var isGameRunning = _starCitizenDetector.IsGameRunning();
            if (isGameRunning && !force && !_configService.Config.AllowSyncWhileGameRunning) {
                var resortCmd = await GetConsoleResortCommandAsync();
                _logService.LogWarning(
                    "Star Citizen is running. Auto-sync skipped to avoid file lock conflict.",
                    $"Use game console (~) command: {resortCmd}");
                return SyncResult.Fail("Star Citizen is currently running. Use in-game console resort command or enable Force Sync.", SyncStatus.StarCitizenActive);
            }

            var xmlPath = _configService.Config.StarCitizenPath;
            if (string.IsNullOrWhiteSpace(xmlPath) || !File.Exists(xmlPath)) {
                _logService.LogError("actionmaps.xml not found at specified path.", xmlPath ?? "Empty path");
                return SyncResult.Fail($"File not found: {xmlPath}", SyncStatus.FileNotFound);
            }

            var canonical = _configService.Config.CanonicalSticks;
            if (canonical.Count < 2) {
                _logService.LogInfo("Need at least 2 canonical sticks configured to evaluate sync.", $"Configured: {canonical.Count}");
                return SyncResult.Ok("Less than 2 sticks configured. Nothing to sync.", changesMade: false, status: SyncStatus.Aligned);
            }

            var liveDevices = await _directInputService.RefreshDevicesAsync();
            var missingSticks = canonical.Where(c => !liveDevices.Any(d => IsStickMatch(c, d))).ToList();
            if (missingSticks.Count > 0) {
                var missingNames = string.Join(", ", missingSticks.Select(m => m.FriendlyRoleName));
                _logService.LogWarning($"Cannot sync: Sticks disconnected ({missingNames})");
                return SyncResult.Fail($"Disconnected sticks: {missingNames}", SyncStatus.SticksDisconnected);
            }

            var xmlMappings = await GetXmlMappingsAsync();
            var swapMap = ComputeSlotSwapMap(canonical, liveDevices, xmlMappings);
            if (swapMap.Count == 0) {
                _logService.LogSuccess("Sticks are already aligned with actionmaps.xml.");
                return SyncResult.Ok("Sticks are already aligned with actionmaps.xml.", changesMade: false, status: SyncStatus.Aligned);
            }

            // Perform backup and XML two-pass transformation strictly on background thread
            var backupPath = await Task.Run(() => {
                var bPath = CreateBackup(xmlPath);
                var doc = XDocument.Load(xmlPath);
                ApplyTransform(doc, swapMap);
                doc.Save(xmlPath);

                // Mirror current active profile in .sticklock/mappings/active_actionmaps.xml
                try {
                    Directory.CreateDirectory(_configService.MappingsDirectory);
                    var persistentActivePath = Path.Combine(_configService.MappingsDirectory, "active_actionmaps.xml");
                    doc.Save(persistentActivePath);
                } catch { }

                // Auto-deploy to Star Citizen Controls\Mappings folder for in-game import
                if (_configService.Config.AutoDeployToMappingsFolder) {
                    try {
                        var scMappingsDir = GetStarCitizenMappingsDirectory();
                        if (!string.IsNullOrWhiteSpace(scMappingsDir)) {
                            Directory.CreateDirectory(scMappingsDir);
                            var activeDeployPath = Path.Combine(scMappingsDir, "layout_StickLock_Active.xml");
                            doc.Save(activeDeployPath);
                        }
                    } catch { }
                }

                return bPath;
            });

            var swapDescriptions = swapMap.Select(kv => $"js{kv.Key} -> js{kv.Value}");
            var deployNote = _configService.Config.AutoDeployToMappingsFolder
                ? " (Deployed to Controls\\Mappings\\layout_StickLock_Active.xml)"
                : "";
            var summary = $"Synced actionmaps.xml: {string.Join(", ", swapDescriptions)}{deployNote}";
            _logService.LogSuccess(summary, $"Backup created: {Path.GetFileName(backupPath)}");

            return SyncResult.Ok(summary, changesMade: true, backupPath: backupPath, status: SyncStatus.Aligned);
        } catch (Exception ex) {
            _logService.LogError($"Sync error: {ex.Message}", ex.ToString());
            return SyncResult.Fail($"Error during sync: {ex.Message}");
        } finally {
            _syncLock.Release();
        }
    }

    public async Task<IReadOnlyList<XmlStickMapping>> GetXmlMappingsAsync() {
        var xmlPath = _configService.Config.StarCitizenPath;
        if (string.IsNullOrWhiteSpace(xmlPath) || !File.Exists(xmlPath)) {
            return Array.Empty<XmlStickMapping>();
        }

        try {
            await using var stream = File.OpenRead(xmlPath);
            var doc = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);
            var results = new List<XmlStickMapping>();

            var optionNodes = doc.Descendants("options")
                .Where(e => string.Equals((string?)e.Attribute("type"), "joystick", StringComparison.OrdinalIgnoreCase));

            foreach (var opt in optionNodes) {
                var instanceAttr = (string?)opt.Attribute("instance");
                if (!int.TryParse(instanceAttr, out var instance)) {
                    continue;
                }

                var productAttr = (string?)opt.Attribute("Product") ?? string.Empty;
                var guid = Guid.Empty;
                var guidMatch = GuidRegex.Match(productAttr);
                if (guidMatch.Success) {
                    _ = Guid.TryParse(guidMatch.Groups[1].Value, out guid);
                }

                var productName = productAttr;
                if (guidMatch.Success) {
                    productName = productAttr.Replace(guidMatch.Value, "").Trim();
                }

                results.Add(new XmlStickMapping {
                    Instance = instance,
                    ProductAttribute = productAttr,
                    ProductName = productName,
                    ExtractedGuid = guid
                });
            }

            return results.OrderBy(r => r.Instance).ToList();
        } catch {
            return Array.Empty<XmlStickMapping>();
        }
    }

    public async Task<IReadOnlyList<string>> GetConsoleResortCommandsAsync() {
        var canonical = _configService.Config.CanonicalSticks;
        var liveDevices = _directInputService.CurrentDevices;

        var xmlMappings = await GetXmlMappingsAsync();
        var swapMap = ComputeSlotSwapMap(canonical, liveDevices, xmlMappings);

        var commands = new List<string>();
        if (swapMap.Count == 0) {
            if (xmlMappings.Count >= 2) {
                return new List<string> { $"pp_resortdevices joystick {xmlMappings[0].Instance} {xmlMappings[1].Instance}" };
            }
            return new List<string> { "pp_resortdevices joystick 1 2" };
        }

        var handledPairs = new HashSet<string>();
        foreach (var (fromInst, toInst) in swapMap) {
            var min = Math.Min(fromInst, toInst);
            var max = Math.Max(fromInst, toInst);
            var key = $"{min}_{max}";
            if (handledPairs.Add(key)) {
                commands.Add($"pp_resortdevices joystick {min} {max}");
            }
        }

        return commands.Count > 0 ? commands : new List<string> { "pp_resortdevices joystick 1 2" };
    }

    public async Task<string> GetConsoleResortCommandAsync() {
        var commands = await GetConsoleResortCommandsAsync();
        return string.Join(" ; ", commands);
    }

    public async Task<SyncResult> RestoreLatestBackupAsync() {
        await _syncLock.WaitAsync();
        try {
            var xmlPath = _configService.Config.StarCitizenPath;
            if (string.IsNullOrWhiteSpace(xmlPath)) {
                return SyncResult.Fail("Star Citizen path is not configured.");
            }

            var backups = GetAvailableBackups();
            if (backups.Count == 0) {
                return SyncResult.Fail("No backups found to restore.");
            }

            var latest = backups[0];
            await Task.Run(() => File.Copy(latest, xmlPath, overwrite: true));
            var fileName = Path.GetFileName(latest);
            _logService.LogSuccess($"Restored backup: {fileName}");
            return SyncResult.Ok($"Successfully restored {fileName}", changesMade: true, backupPath: latest);
        } catch (Exception ex) {
            _logService.LogError($"Failed to restore backup: {ex.Message}", ex.ToString());
            return SyncResult.Fail($"Failed to restore backup: {ex.Message}");
        } finally {
            _syncLock.Release();
        }
    }

    public IReadOnlyList<string> GetAvailableBackups() {
        var backupDir = Path.Combine(_configService.BaseDirectory, "backups");
        var results = new List<FileInfo>();

        if (Directory.Exists(backupDir)) {
            results.AddRange(Directory.GetFiles(backupDir, "actionmaps_backup_*.xml").Select(f => new FileInfo(f)));
        }

        var xmlPath = _configService.Config.StarCitizenPath;
        if (!string.IsNullOrWhiteSpace(xmlPath)) {
            var dir = Path.GetDirectoryName(xmlPath);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir)) {
                var legacyDir = Path.Combine(dir, "StickLock_Backups");
                if (Directory.Exists(legacyDir)) {
                    results.AddRange(Directory.GetFiles(legacyDir, "actionmaps_backup_*.xml").Select(f => new FileInfo(f)));
                }
            }
        }

        return results.OrderByDescending(f => f.CreationTime)
            .Select(f => f.FullName)
            .Distinct()
            .ToList();
    }

    public async Task<SyncResult> ExportActiveXmlAsync(string destinationPath) {
        var xmlPath = _configService.Config.StarCitizenPath;
        if (string.IsNullOrWhiteSpace(xmlPath) || !File.Exists(xmlPath)) {
            _logService.LogError("Export failed: Active actionmaps.xml file does not exist.");
            return SyncResult.Fail("Active actionmaps.xml not found. Nothing to export.", SyncStatus.FileNotFound);
        }

        try {
            await Task.Run(() => {
                var destDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(destDir)) {
                    Directory.CreateDirectory(destDir);
                }
                File.Copy(xmlPath, destinationPath, overwrite: true);
            });

            var fileName = Path.GetFileName(destinationPath);
            _logService.LogSuccess($"Exported profile XML to {fileName}", destinationPath);
            return SyncResult.Ok($"Successfully exported profile to {fileName}", changesMade: false);
        } catch (Exception ex) {
            _logService.LogError($"Export failed: {ex.Message}", ex.ToString());
            return SyncResult.Fail($"Failed to export XML: {ex.Message}");
        }
    }

    public async Task<SyncResult> ImportAndApplyXmlAsync(string sourcePath) {
        if (!File.Exists(sourcePath)) {
            return SyncResult.Fail($"Import file not found: {sourcePath}");
        }

        var xmlPath = _configService.Config.StarCitizenPath;
        if (string.IsNullOrWhiteSpace(xmlPath)) {
            return SyncResult.Fail("Active actionmaps.xml path is not configured.");
        }

        if (_starCitizenDetector.IsGameRunning()) {
            return SyncResult.Fail("Star Citizen is currently running. Please close the game before applying a new profile XML.", SyncStatus.StarCitizenActive);
        }

        await _syncLock.WaitAsync();
        string? backupPath = null;
        try {
            await Task.Run(() => {
                var doc = XDocument.Load(sourcePath);
                if (doc.Root == null) {
                    throw new InvalidOperationException("Imported XML file is empty or missing root element.");
                }

                var targetDir = Path.GetDirectoryName(xmlPath);
                if (!string.IsNullOrWhiteSpace(targetDir)) {
                    Directory.CreateDirectory(targetDir);
                }

                if (File.Exists(xmlPath)) {
                    backupPath = CreateBackup(xmlPath);
                }

                File.Copy(sourcePath, xmlPath, overwrite: true);
            });

            var importName = Path.GetFileName(sourcePath);
            _logService.LogSuccess($"Imported and applied '{importName}' to active game XML.", backupPath != null ? $"Backup saved: {Path.GetFileName(backupPath)}" : "");
        } catch (Exception ex) {
            _logService.LogError($"Import error: {ex.Message}", ex.ToString());
            return SyncResult.Fail($"Import error: {ex.Message}");
        } finally {
            _syncLock.Release();
        }

        // Run sync to immediately align the newly imported profile with physical sticks
        var syncResult = await SyncAsync();
        if (syncResult.ChangesMade) {
            _logService.LogSuccess("Imported profile automatically aligned with current physical joysticks.");
            return SyncResult.Ok($"Applied '{Path.GetFileName(sourcePath)}' and aligned joystick slots to Windows order.", changesMade: true, status: SyncStatus.Aligned);
        }

        return SyncResult.Ok($"Applied '{Path.GetFileName(sourcePath)}' to active game XML.", changesMade: true, status: SyncStatus.Aligned);
    }

    public async Task<XmlProfileSummary> InspectXmlProfileAsync(string filePath) {
        var summary = new XmlProfileSummary {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath)
        };

        if (!File.Exists(filePath)) {
            summary.IsValid = false;
            summary.ErrorMessage = "File does not exist.";
            return summary;
        }

        try {
            return await Task.Run(async () => {
                await using var stream = File.OpenRead(filePath);
                var doc = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);
                var options = doc.Descendants("options")
                    .Where(e => string.Equals((string?)e.Attribute("type"), "joystick", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                summary.JoystickOptionsCount = options.Count;
                foreach (var opt in options) {
                    var product = (string?)opt.Attribute("Product") ?? $"Joystick {(string?)opt.Attribute("instance")}";
                    summary.DeviceNames.Add(product.Trim());
                }

                summary.RebindCount = doc.Descendants("rebind").Count();
                summary.IsValid = true;
                return summary;
            });
        } catch (Exception ex) {
            summary.IsValid = false;
            summary.ErrorMessage = ex.Message;
            return summary;
        }
    }

    public string? GetStarCitizenMappingsDirectory() {
        var xmlPath = _configService.Config.StarCitizenPath;
        if (string.IsNullOrWhiteSpace(xmlPath)) {
            return null;
        }

        try {
            var defaultDir = Path.GetDirectoryName(xmlPath);
            if (string.IsNullOrWhiteSpace(defaultDir)) {
                return null;
            }

            var profilesDir = Path.GetDirectoryName(defaultDir);
            if (string.IsNullOrWhiteSpace(profilesDir)) {
                return null;
            }

            var clientZeroDir = Path.GetDirectoryName(profilesDir);
            if (string.IsNullOrWhiteSpace(clientZeroDir)) {
                return null;
            }

            return Path.Combine(clientZeroDir, "Controls", "Mappings");
        } catch {
            return null;
        }
    }

    public IReadOnlyList<string> GetStarCitizenCustomMappings() {
        var scMappingsDir = GetStarCitizenMappingsDirectory();
        if (string.IsNullOrWhiteSpace(scMappingsDir) || !Directory.Exists(scMappingsDir)) {
            return Array.Empty<string>();
        }

        try {
            return Directory.GetFiles(scMappingsDir, "*.xml");
        } catch {
            return Array.Empty<string>();
        }
    }

    public async Task<IReadOnlyList<SavedMappingProfile>> GetSavedMappingsAsync() {
        var mappingsDir = _configService.MappingsDirectory;
        if (!Directory.Exists(mappingsDir)) {
            return Array.Empty<SavedMappingProfile>();
        }

        return await Task.Run(async () => {
            var files = Directory.GetFiles(mappingsDir, "*.xml")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .ToList();

            var scMappingsDir = GetStarCitizenMappingsDirectory();
            var scFiles = (!string.IsNullOrWhiteSpace(scMappingsDir) && Directory.Exists(scMappingsDir))
                ? Directory.GetFiles(scMappingsDir, "*.xml").Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string?>();

            var results = new List<SavedMappingProfile>();
            foreach (var fi in files) {
                var profile = new SavedMappingProfile {
                    FileName = fi.Name,
                    FilePath = fi.FullName,
                    LastModified = fi.LastWriteTime,
                    FileSizeBytes = fi.Length,
                    ExistsInStarCitizen = scFiles.Contains(fi.Name)
                };

                try {
                    var summary = await InspectXmlProfileAsync(fi.FullName);
                    if (summary.IsValid) {
                        profile.JoystickOptionsCount = summary.JoystickOptionsCount;
                        profile.RebindCount = summary.RebindCount;
                        profile.DeviceNames = summary.DeviceNames;
                    }
                } catch { }

                results.Add(profile);
            }

            return (IReadOnlyList<SavedMappingProfile>)results;
        });
    }

    public async Task<SyncResult> SaveActiveAsProfileAsync(string profileName) {
        if (string.IsNullOrWhiteSpace(profileName)) {
            return SyncResult.Fail("Profile name cannot be empty.");
        }

        var xmlPath = _configService.Config.StarCitizenPath;
        if (string.IsNullOrWhiteSpace(xmlPath) || !File.Exists(xmlPath)) {
            return SyncResult.Fail("Active actionmaps.xml not found. Nothing to save.", SyncStatus.FileNotFound);
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var cleanName = new string(profileName.Where(c => !invalidChars.Contains(c)).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(cleanName)) {
            return SyncResult.Fail("Profile name contains invalid characters.");
        }

        if (!cleanName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) {
            cleanName += ".xml";
        }

        var destinationPath = Path.Combine(_configService.MappingsDirectory, cleanName);

        try {
            await Task.Run(() => {
                Directory.CreateDirectory(_configService.MappingsDirectory);
                File.Copy(xmlPath, destinationPath, overwrite: true);
            });

            _logService.LogSuccess($"Saved active profile as '{cleanName}' in .sticklock library.", destinationPath);
            return SyncResult.Ok($"Successfully saved '{cleanName}' to your .sticklock mappings library!");
        } catch (Exception ex) {
            _logService.LogError($"Failed to save profile: {ex.Message}", ex.ToString());
            return SyncResult.Fail($"Failed to save profile: {ex.Message}");
        }
    }

    public async Task<SyncResult> CopyProfileToStarCitizenAsync(string profileFileName) {
        var sourcePath = Path.Combine(_configService.MappingsDirectory, profileFileName);
        if (!File.Exists(sourcePath)) {
            return SyncResult.Fail($"Profile not found in .sticklock library: {profileFileName}");
        }

        var scMappingsDir = GetStarCitizenMappingsDirectory();
        if (string.IsNullOrWhiteSpace(scMappingsDir)) {
            return SyncResult.Fail("Star Citizen directory could not be resolved. Please verify your game path.");
        }

        try {
            await Task.Run(() => {
                Directory.CreateDirectory(scMappingsDir);
                var destPath = Path.Combine(scMappingsDir, profileFileName);
                File.Copy(sourcePath, destPath, overwrite: true);
            });

            _logService.LogSuccess($"Copied '{profileFileName}' to Star Citizen Controls\\Mappings.", scMappingsDir);
            return SyncResult.Ok($"Copied '{profileFileName}' to Star Citizen Controls\\Mappings folder. You can now load it in-game!");
        } catch (Exception ex) {
            _logService.LogError($"Failed to copy profile to Star Citizen: {ex.Message}", ex.ToString());
            return SyncResult.Fail($"Failed to copy profile to Star Citizen: {ex.Message}");
        }
    }

    public async Task<SyncResult> CopyAllProfilesToStarCitizenAsync() {
        var scMappingsDir = GetStarCitizenMappingsDirectory();
        if (string.IsNullOrWhiteSpace(scMappingsDir)) {
            return SyncResult.Fail("Star Citizen directory could not be resolved. Please verify your game path.");
        }

        try {
            var files = Directory.GetFiles(_configService.MappingsDirectory, "*.xml");
            if (files.Length == 0) {
                return SyncResult.Fail("No saved profiles found in .sticklock library to copy.");
            }

            await Task.Run(() => {
                Directory.CreateDirectory(scMappingsDir);
                foreach (var file in files) {
                    var fileName = Path.GetFileName(file);
                    File.Copy(file, Path.Combine(scMappingsDir, fileName), overwrite: true);
                }
            });

            _logService.LogSuccess($"Copied {files.Length} profile(s) to Star Citizen Controls\\Mappings.", scMappingsDir);
            return SyncResult.Ok($"Successfully copied {files.Length} profile(s) to Star Citizen USER folder!");
        } catch (Exception ex) {
            _logService.LogError($"Failed to copy profiles to Star Citizen: {ex.Message}", ex.ToString());
            return SyncResult.Fail($"Failed to copy profiles: {ex.Message}");
        }
    }

    public async Task<SyncResult> PullProfilesFromStarCitizenAsync() {
        var scMappingsDir = GetStarCitizenMappingsDirectory();
        if (string.IsNullOrWhiteSpace(scMappingsDir) || !Directory.Exists(scMappingsDir)) {
            return SyncResult.Fail("Star Citizen Controls\\Mappings directory not found or empty.");
        }

        try {
            var files = Directory.GetFiles(scMappingsDir, "*.xml");
            if (files.Length == 0) {
                return SyncResult.Fail("No XML profiles found in Star Citizen Controls\\Mappings folder.");
            }

            var copied = 0;
            await Task.Run(() => {
                Directory.CreateDirectory(_configService.MappingsDirectory);
                foreach (var file in files) {
                    var fileName = Path.GetFileName(file);
                    var destPath = Path.Combine(_configService.MappingsDirectory, fileName);
                    File.Copy(file, destPath, overwrite: true);
                    copied++;
                }
            });

            _logService.LogSuccess($"Imported {copied} profile(s) from Star Citizen to .sticklock library.", _configService.MappingsDirectory);
            return SyncResult.Ok($"Successfully backed up {copied} profile(s) from Star Citizen into your .sticklock library!");
        } catch (Exception ex) {
            _logService.LogError($"Failed to pull profiles from Star Citizen: {ex.Message}", ex.ToString());
            return SyncResult.Fail($"Failed to pull profiles: {ex.Message}");
        }
    }

    public async Task<SyncResult> ApplySavedProfileToActiveAsync(string profileFileName) {
        var sourcePath = Path.Combine(_configService.MappingsDirectory, profileFileName);
        if (!File.Exists(sourcePath)) {
            return SyncResult.Fail($"Profile '{profileFileName}' not found in .sticklock library.");
        }

        return await ImportAndApplyXmlAsync(sourcePath);
    }

    public async Task<bool> DeleteSavedProfileAsync(string profileFileName) {
        var path = Path.Combine(_configService.MappingsDirectory, profileFileName);
        if (!File.Exists(path)) {
            return false;
        }

        try {
            await Task.Run(() => File.Delete(path));
            _logService.LogInfo($"Deleted profile '{profileFileName}' from .sticklock library.");
            return true;
        } catch (Exception ex) {
            _logService.LogError($"Failed to delete profile '{profileFileName}': {ex.Message}");
            return false;
        }
    }

    private string CreateBackup(string xmlPath) {
        var backupDir = Path.Combine(_configService.BaseDirectory, "backups");
        Directory.CreateDirectory(backupDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupFile = Path.Combine(backupDir, $"actionmaps_backup_{timestamp}.xml");
        File.Copy(xmlPath, backupFile, overwrite: true);

        // Prune old backups
        var maxBackups = Math.Max(1, _configService.Config.MaxBackupsToKeep);
        var existing = Directory.GetFiles(backupDir, "actionmaps_backup_*.xml")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .ToList();

        if (existing.Count > maxBackups) {
            foreach (var oldFile in existing.Skip(maxBackups)) {
                try {
                    oldFile.Delete();
                } catch { }
            }
        }

        return backupFile;
    }

    private bool CheckIfNeedsSync(
        List<CanonicalStick> canonical,
        IReadOnlyList<DirectInputDevice> liveDevices,
        IReadOnlyList<XmlStickMapping> xmlMappings) {
        var swapMap = ComputeSlotSwapMap(canonical, liveDevices, xmlMappings);
        return swapMap.Count > 0;
    }

    private Dictionary<int, int> ComputeSlotSwapMap(
        List<CanonicalStick> canonical,
        IReadOnlyList<DirectInputDevice> liveDevices,
        IReadOnlyList<XmlStickMapping> xmlMappings) {
        var swapMap = new Dictionary<int, int>();

        if (canonical.Count >= 2) {
            // Determine which live slot each canonical stick currently occupies in Windows
            var stickLiveSlots = new Dictionary<CanonicalStick, int>();
            foreach (var stick in canonical) {
                var live = liveDevices.FirstOrDefault(d => IsStickMatch(stick, d));
                if (live == null) {
                    return swapMap; // Stick missing, cannot reliably compute swap
                }
                stickLiveSlots[stick] = live.SlotIndex;
            }

            // Determine which XML instance currently belongs to each canonical stick
            var stickXmlInstances = new Dictionary<CanonicalStick, int>();
            foreach (var stick in canonical) {
                var xmlEntry = xmlMappings.FirstOrDefault(x =>
                    (stick.InstanceGuid != Guid.Empty && x.ExtractedGuid == stick.InstanceGuid) ||
                    (stick.ProductGuid != Guid.Empty && x.ExtractedGuid == stick.ProductGuid));

                if (xmlEntry != null) {
                    stickXmlInstances[stick] = xmlEntry.Instance;
                } else {
                    // Fallback: match by RoleSlot if instance exists
                    var fallbackEntry = xmlMappings.FirstOrDefault(x => x.Instance == stick.RoleSlot);
                    if (fallbackEntry != null) {
                        stickXmlInstances[stick] = fallbackEntry.Instance;
                    }
                }
            }

            // Compare: for each canonical stick, does its XML instance match its current live Windows slot?
            foreach (var stick in canonical) {
                if (!stickLiveSlots.TryGetValue(stick, out var liveSlot)) {
                    continue;
                }
                if (!stickXmlInstances.TryGetValue(stick, out var xmlInstance)) {
                    continue;
                }

                if (xmlInstance != liveSlot) {
                    swapMap[xmlInstance] = liveSlot;
                }
            }
        } else if (xmlMappings.Count >= 2) {
            // Match XML mappings directly against live devices
            foreach (var xmlEntry in xmlMappings) {
                var live = liveDevices.FirstOrDefault(d =>
                    (xmlEntry.ExtractedGuid != Guid.Empty && d.InstanceGuid == xmlEntry.ExtractedGuid) ||
                    (xmlEntry.ExtractedGuid != Guid.Empty && d.ProductGuid == xmlEntry.ExtractedGuid) ||
                    string.Equals(d.ProductName, xmlEntry.ProductName, StringComparison.OrdinalIgnoreCase));

                if (live != null && xmlEntry.Instance != live.SlotIndex) {
                    swapMap[xmlEntry.Instance] = live.SlotIndex;
                }
            }
        }

        return swapMap;
    }

    private void ApplyTransform(XDocument doc, Dictionary<int, int> swapMap) {
        if (swapMap.Count == 0) {
            return;
        }

        // Pass 1: Replace targets with unique placeholders
        var optionsNodes = doc.Descendants("options")
            .Where(e => string.Equals((string?)e.Attribute("type"), "joystick", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var opt in optionsNodes) {
            var instAttr = opt.Attribute("instance");
            if (instAttr != null && int.TryParse(instAttr.Value, out var inst) && swapMap.ContainsKey(inst)) {
                instAttr.Value = $"__TEMP_JS_INSTANCE_{inst}__";
            }
        }

        var rebindNodes = doc.Descendants("rebind").ToList();
        foreach (var rebind in rebindNodes) {
            TransformRebindAttributes(rebind, (val) => {
                foreach (var kv in swapMap) {
                    val = Regex.Replace(val, $@"\bjs{kv.Key}_", $"__TEMP_JS_{kv.Key}__");
                }
                return val;
            });
        }

        // Pass 2: Replace placeholders with final swapped slots
        foreach (var opt in optionsNodes) {
            var instAttr = opt.Attribute("instance");
            if (instAttr == null) {
                continue;
            }

            foreach (var kv in swapMap) {
                var placeholder = $"__TEMP_JS_INSTANCE_{kv.Key}__";
                if (instAttr.Value == placeholder) {
                    instAttr.Value = kv.Value.ToString();
                    break;
                }
            }
        }

        foreach (var rebind in rebindNodes) {
            TransformRebindAttributes(rebind, (val) => {
                foreach (var kv in swapMap) {
                    val = val.Replace($"__TEMP_JS_{kv.Key}__", $"js{kv.Value}_");
                }
                return val;
            });
        }
    }

    private void TransformRebindAttributes(XElement element, Func<string, string> transformer) {
        var inputAttr = element.Attribute("input");
        if (inputAttr != null && !string.IsNullOrEmpty(inputAttr.Value)) {
            inputAttr.Value = transformer(inputAttr.Value);
        }

        var defaultInputAttr = element.Attribute("defaultInput");
        if (defaultInputAttr != null && !string.IsNullOrEmpty(defaultInputAttr.Value)) {
            defaultInputAttr.Value = transformer(defaultInputAttr.Value);
        }
    }

    private bool IsStickMatch(CanonicalStick stick, DirectInputDevice device) {
        if (stick.InstanceGuid != Guid.Empty && device.InstanceGuid != Guid.Empty) {
            if (stick.InstanceGuid == device.InstanceGuid) {
                return true;
            }
        }

        if (stick.ProductGuid != Guid.Empty && device.ProductGuid != Guid.Empty) {
            if (stick.ProductGuid == device.ProductGuid) {
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(stick.ProductName) && !string.IsNullOrWhiteSpace(device.ProductName)) {
            if (string.Equals(stick.ProductName, device.ProductName, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }

        return false;
    }
}

