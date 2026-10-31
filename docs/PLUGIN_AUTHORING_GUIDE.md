# StickLock Plugin Authoring Guide

Comprehensive guide for developing, compiling, and distributing out-of-tree game integration plugins for **StickLock Avionics**.

---

## 1. Overview & Architecture

StickLock is designed as an extensible flight controller slot management engine. While its initial native focus was Star Citizen's `actionmaps.xml` DirectInput enumeration flips, modern flight and space simulators (Elite Dangerous, IL-2 Sturmovik, DCS World, Microsoft Flight Simulator) also suffer from DirectInput GUID and device instance reordering whenever Windows updates, USB ports change, or hubs reset.

The **StickLock Plugin System** allows anyone to write and distribute custom game integrations **out-of-tree**, without requiring the main StickLock repository or source code.

```
┌─────────────────────────────────────────────────────────────┐
│                       StickLock GUI                         │
│  (Top-Bar Game Switcher & "Manage Plugins..." Dialog)       │
└──────────────────────────────┬──────────────────────────────┘
                               │
               ┌───────────────┴───────────────┐
               │     IGamePluginManager        │
               └───────┬───────────────┬───────┘
                       │               │
      ┌────────────────┴────────┐      └────────────────────────┐
      ▼                         ▼                               ▼
[Built-In Plugins]     [GamePluginLoader]              [StickLock.SDK]
  - Star Citizen         - Scans ~/.sticklock/plugins/   - IGamePlugin
  - Elite Dangerous      - AssemblyLoadContext (isolated)- GamePluginBase
                         - Hot-reloads .dll assemblies   - Models & Enums
                                │
                                ▼
                     [Community Plugins]
                       - IL-2 Sturmovik
                       - DCS World
                       - Custom Sims
```

### Key Architectural Tenets
- **Out-of-Tree Independence**: Third-party developers build standard .NET class libraries targeting `net10.0` (or compatible .NET standard/modern runtimes) referencing only the lightweight `StickLock.SDK` contract assembly.
- **Isolated Assembly Loading**: Plugins are loaded via collectible `AssemblyLoadContext` instances. Dependencies do not conflict with the host application or each other.
- **Hot-Reloadable**: Plugins can be dropped into the plugins directory or recompiled while StickLock is running. Clicking **Reload Plugins** inside the app updates available integrations immediately without an app restart.
- **Process Protection**: Plugins enforce safety checks to ensure game configuration files are never modified while the game process is running.
- **Automated Backup & Restore**: Inheriting from `GamePluginBase` automatically provides timestamped backups before any write operation and enables one-click rollback in the UI.

---

## 2. Getting the SDK

Third-party plugin developers do not need the StickLock source code. All contracts, base classes, and data structures are distributed via `StickLock.SDK`.

### Option A: Direct Assembly Reference (`StickLock.SDK.dll`)
Download `StickLock.SDK.dll` from the latest [StickLock GitHub Releases](https://github.com/dondon101/StickLock/releases) and place it in your local development folder.

### Option B: Local or NuGet Package Reference
When `StickLock.SDK` is distributed via NuGet or a local package feed:

```xml
<ItemGroup>
  <PackageReference Include="StickLock.SDK" Version="1.0.0" />
</ItemGroup>
```

---

## 3. Creating a Plugin from Scratch

### Step 1: Create a Class Library
Open a terminal (PowerShell, Bash, or Command Prompt) and create a new class library:

```powershell
dotnet new classlib -n StickLock.Plugin.Il2Sturmovik -f net10.0
cd StickLock.Plugin.Il2Sturmovik
```

### Step 2: Configure the `.csproj`
Edit `StickLock.Plugin.Il2Sturmovik.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>StickLock.Plugins.Il2Sturmovik</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <!-- Reference the StickLock.SDK assembly -->
    <Reference Include="StickLock.SDK">
      <HintPath>..\lib\StickLock.SDK.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

</Project>
```

> [!NOTE]
> Set `<Private>false</Private>` on `StickLock.SDK` reference so that `StickLock.SDK.dll` is not duplicated into your build output; StickLock's host engine already provides it at runtime.

---

## 4. Implementing the Plugin

To implement your plugin, create a class that inherits from `GamePluginBase` (or directly implements `IGamePlugin`).

### Core Interface Contract: `IGamePlugin`

| Member | Type | Purpose |
|---|---|---|
| `Id` | `string` | Unique alphanumeric identifier (e.g., `"il2sturmovik"`). |
| `Name` | `string` | User-friendly display name (e.g., `"IL-2 Sturmovik: Great Battles"`). |
| `Description` | `string` | Brief explanation of the integration. |
| `ProcessName` | `string` | Game executable name without extension (e.g., `"Il-2"`). |
| `SupportsFlightAxisMode` | `bool` | Whether the game supports space-sim vs traditional flight axis swapping. |
| `IsInstalledAsync()` | `Task<bool>` | Checks if the game or configuration folders exist on this machine. |
| `DetectActiveBindingsPathAsync()` | `Task<string?>` | Resolves the primary active binding file path. |
| `CheckAlignmentAsync()` | `Task<MappingAlignmentCheck>` | Inspects configuration and compares device slots to current DirectInput order. |
| `SyncAsync(bool force)` | `Task<SyncResult>` | Backs up and reorders device mappings to match current DirectInput order. |
| `FixAndSyncAsync(bool force)` | `Task<SyncResult>` | Automatically corrects mismatched slots and writes the configuration. |
| `GetAvailableBackupsAsync()` | `Task<IReadOnlyList<string>>` | Returns available timestamped backup file paths. |
| `RestoreLatestBackupAsync()` | `Task<SyncResult>` | Reverts configuration to the most recent backup. |

---

## 5. Walkthrough: Real-World Case Studies

### Case Study 1: IL-2 Sturmovik: Great Battles

IL-2 Sturmovik tracks connected controllers in `<IL-2 Install>\data\input\devices.txt`.
The file maps DirectInput GUIDs to device indices (`joy0`, `joy1`, `joy2`):

```txt
configId 1
0 "%device{6F1D2B61-D5A0-11CF-BFC7-525400123456}DevName%"
1 "%device{12345678-ABCD-EF01-2345-6789ABCDEF01}DevName%"
```

When Windows changes GUIDs or USB ports, the IDs in `devices.txt` change, scrambling every control binding in `current.actions`.

#### Implementation:
```csharp
using StickLock.Models;
using StickLock.Plugins;
using System.Text.RegularExpressions;

namespace StickLock.Plugins.Il2Sturmovik;

public class Il2SturmovikPlugin : GamePluginBase {
    public override string Id => "il2sturmovik";
    public override string Name => "IL-2 Sturmovik: Great Battles";
    public override string Description => "Maintains DirectInput GUID indices in data/input/devices.txt.";
    public override string ProcessName => "Il-2";
    public override bool SupportsFlightAxisMode => false;

    private static readonly Regex DeviceRegex = new(
        @"^(\d+)\s+""%device\{([A-Fa-f0-9\-]+)\}([^%]*)%""",
        RegexOptions.Compiled);

    public override Task<bool> IsInstalledAsync() {
        var path = GetDevicesTxtPath();
        return Task.FromResult(File.Exists(path));
    }

    public override Task<string?> DetectActiveBindingsPathAsync() {
        var path = GetDevicesTxtPath();
        return Task.FromResult(File.Exists(path) ? path : null);
    }

    public override async Task<MappingAlignmentCheck> CheckAlignmentAsync() {
        var path = await DetectActiveBindingsPathAsync();
        if (path == null) {
            return new MappingAlignmentCheck {
                IsMisaligned = false,
                Status = SyncStatus.FileNotFound,
                StatusMessage = "devices.txt not found."
            };
        }

        if (IsGameRunning()) {
            return new MappingAlignmentCheck {
                IsMisaligned = false,
                IsGameRunning = true,
                Status = SyncStatus.StarCitizenActive,
                StatusMessage = "IL-2 Sturmovik is currently running."
            };
        }

        // Parse existing entries and verify GUID alignment
        return new MappingAlignmentCheck {
            IsMisaligned = false,
            Status = SyncStatus.Aligned,
            StatusMessage = "IL-2 controller indices are aligned."
        };
    }

    public override async Task<SyncResult> SyncAsync(bool force = false) {
        var path = await DetectActiveBindingsPathAsync();
        if (path == null) {
            return SyncResult.Fail("devices.txt not found.");
        }

        if (IsGameRunning() && !force) {
            return SyncResult.Fail("Cannot sync while IL-2 Sturmovik is running.");
        }

        // 1. Create automated timestamped backup
        var backupPath = CreateBackup(path);

        // 2. Perform file alignment and write updated devices.txt safely
        // File.WriteAllText(path, newContent);

        return SyncResult.Ok("Aligned IL-2 devices.txt.", changesMade: true, backupPath: backupPath, status: SyncStatus.Aligned);
    }

    private string GetDevicesTxtPath() {
        // Typically located under IL-2 directory or Steam library
        return Path.Combine(@"C:\Program Files (x86)\1C Game Studios\IL-2 Sturmovik Great Battles\data\input\devices.txt");
    }
}
```

---

### Case Study 2: Digital Combat Simulator (DCS World)

DCS World stores input bindings under `%USERPROFILE%\Saved Games\DCS\Config\Input\<Aircraft>\joystick\`.
Each joystick gets its own diff file named after the device name and DirectInput GUID:
`VKB-Sim Gladiator NXT R {E2B801D0-8910-11EE-8001-444553540000}.diff.lua`.

When Windows enumeration changes the GUID, DCS fails to match the device and creates an empty configuration.
A DCS plugin:
1. Detects connected DirectInput devices and their GUIDs.
2. Locates matching aircraft `.diff.lua` files.
3. Automatically renames or updates the GUID portion of the filename to restore all keybinds without manual in-game remapping.

---

## 6. Distributing and Installing Plugins

### Where Plugins Live
StickLock scans the following directories on startup:
1. **User Plugins (Recommended)**:
   `%USERPROFILE%\.sticklock\plugins\<PluginName>\`
2. **Application Directory**:
   `<StickLock-Folder>\plugins\<PluginName>\`

### Installation Steps for Players
1. Build or download the plugin `.dll` (e.g., `StickLock.Plugin.Il2Sturmovik.dll`).
2. Open StickLock.
3. In the top bar game dropdown, select **Manage Plugins...**.
4. Click **Open Plugins Folder**.
5. Create a folder for the plugin (e.g. `Il2Sturmovik`) and copy the `.dll` (and any third-party dependencies) into it.
6. Return to StickLock and click **Reload Plugins**.
7. The new game integration appears in the list and can be activated immediately!

---

## 7. Best Practices & Guidelines

1. **Never Write While Game Is Running**: Always check `IsGameRunning()` before writing changes. Modifying files while a flight simulator is running can corrupt configs or be overwritten by the game on exit.
2. **Always Create Backups**: Call `CreateBackup(filePath)` provided by `GamePluginBase` prior to altering any user configuration. Backups are placed in `%USERPROFILE%\.sticklock\backups\<PluginId>\`.
3. **Atomic File Writes**: Write new content to a temporary file (`.tmp`) first, then use `File.Move(tempFile, targetFile, overwrite: true)` to ensure atomic updates.
4. **Use K&R Style**: Keep plugin code clean, structured, and compliant with K&R (One True Brace Style).
5. **Report Detailed Status**: In `MappingAlignmentCheck`, populate `StatusMessage` and `Mismatches` with actionable descriptions so users understand what changed.

