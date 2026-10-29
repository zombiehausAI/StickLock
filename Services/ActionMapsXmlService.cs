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
        if (canonical.Count == 0) {
            return SyncStatus.Unknown;
        }

        var liveDevices = _directInputService.RefreshDevices();
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

    public async Task<SyncResult> SyncAsync() {
        await _syncLock.WaitAsync();
        try {
            if (_starCitizenDetector.IsGameRunning()) {
                var resortCmd = await GetConsoleResortCommandAsync();
                _logService.LogWarning(
                    "Star Citizen is running. Auto-sync skipped to avoid file lock conflict.",
                    $"Use game console (~) command: {resortCmd}");
                return SyncResult.Fail("Star Citizen is currently running. Close game or use in-game console resort command.", SyncStatus.StarCitizenActive);
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

            var liveDevices = _directInputService.RefreshDevices();
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

            // Create timestamped backup
            var backupPath = CreateBackup(xmlPath);

            // Perform XML two-pass transformation
            var doc = XDocument.Load(xmlPath);
            ApplyTransform(doc, swapMap);
            doc.Save(xmlPath);

            var swapDescriptions = swapMap.Select(kv => $"js{kv.Key} -> js{kv.Value}");
            var summary = $"Synced actionmaps.xml: {string.Join(", ", swapDescriptions)}";
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

    public async Task<string> GetConsoleResortCommandAsync() {
        var canonical = _configService.Config.CanonicalSticks;
        var liveDevices = _directInputService.CurrentDevices;
        var xmlMappings = await GetXmlMappingsAsync();
        var swapMap = ComputeSlotSwapMap(canonical, liveDevices, xmlMappings);

        if (swapMap.Count >= 2) {
            var keys = swapMap.Keys.OrderBy(k => k).ToList();
            return $"pp_resortdevices joystick {string.Join(" ", keys)}";
        }

        return "pp_resortdevices joystick 1 2";
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
            File.Copy(latest, xmlPath, overwrite: true);
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
        var xmlPath = _configService.Config.StarCitizenPath;
        if (string.IsNullOrWhiteSpace(xmlPath)) {
            return Array.Empty<string>();
        }

        var dir = Path.GetDirectoryName(xmlPath);
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) {
            return Array.Empty<string>();
        }

        var backupDir = Path.Combine(dir, "StickLock_Backups");
        if (!Directory.Exists(backupDir)) {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(backupDir, "actionmaps_backup_*.xml")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTime)
            .Select(f => f.FullName)
            .ToList();
    }

    private string CreateBackup(string xmlPath) {
        var dir = Path.GetDirectoryName(xmlPath)!;
        var backupDir = Path.Combine(dir, "StickLock_Backups");
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
                } catch {
                    // Suppress deletion errors
                }
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
        if (canonical.Count < 2) {
            return swapMap;
        }

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
