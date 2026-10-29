# StickLock

**Star Citizen Flight Controller Slot Synchronizer & Anti-Inversion Daemon**

StickLock is a zero-XAML, high-performance Windows desktop utility built with **.NET 10**, **WinForms Blazor Hybrid** (`Microsoft.AspNetCore.Components.WebView.WindowsForms`), and **MudBlazor**.

Star Citizen relies on Windows DirectInput enumeration order (`js1`, `js2`, etc.) to map physical flight sticks. When joysticks are unplugged, USB hubs reset, or Windows reboots, DirectInput frequently flips their enumeration order. StickLock runs quietly in the Windows System Tray, monitors USB hardware changes in real time, compares current DirectInput device indices against user-locked canonical slots, and automatically updates Star Citizen's `actionmaps.xml` before game launch.

---

## Key Features

- **Self-Enclosed Deployment**: Compiled self-contained (`win-x64`), bundling the .NET 10 CLR and Windows Desktop runtimes so end users do not need to install any external runtimes or SDKs.
- **Smart Installer & In-Place Updater**:
  - Automatically detects existing installations across the Registry, Windows Uninstall database, shortcuts, and default locations.
  - Automatically defaults to updating existing installations in-place while preserving user configurations.
  - Allows custom folder selection on any drive via command-line or native Windows folder browser GUI.
  - User-level install (`%LOCALAPPDATA%\StickLock\app`) requiring no Administrator UAC prompts.
  - Integrates with Windows Add/Remove Programs (Installed Apps) and creates Desktop / Start Menu shortcuts.
- **Background USB Monitoring**: WMI hardware watcher (`Win32_DeviceChangeEvent`) with a 1.5-second debounce timer to absorb USB hub power-cycles.
- **Star Citizen Process Guard**: Detects active `StarCitizen.exe` processes and safely defers auto-sync to avoid file lock conflicts, providing an in-game console command (`pp_resortdevices joystick 1 2`) for live resort.
- **Idempotent Two-Pass Transformation**: Replaces `<options type="joystick" instance="X">` and `<rebind input="jsX_...">` attributes using unique intermediary placeholders to eliminate collision errors.
- **Automated Backup & Pruning**: Creates timestamped backups in `StickLock_Backups` with customizable pruning limits and one-click restoration.
- **Star Citizen MFD Dark UI**: Custom obsidian (`#0B0F19`), cyan (`#00E5FF`), and amber (`#FFAB00`) glass theme reminiscent of spaceship avionics.

---

## Build, Test & Package Pipeline

StickLock includes an automated pipeline script modelled after modern release orchestrators:

```powershell
# Full release cycle: Clean -> Unit Tests -> Self-Contained Publish -> ZIP & Checksum (and Inno Setup EXE if ISCC is installed)
.\Build-And-Package.ps1

# Build, package, and immediately install locally for testing
.\Build-And-Package.ps1 -Install

# Build, package, and install to a specific custom folder or drive
.\Build-And-Package.ps1 -Install -InstallPath "D:\StickLock"

# Skip automated tests during development
.\Build-And-Package.ps1 -SkipTests
```

### Build Artifacts Output
Artifacts are generated in the `artifacts/` folder:
- `StickLock-v<version>-win-x64.zip` — Standalone portable archive.
- `StickLock-v<version>-Setup.exe` — Inno Setup Windows installer (when Inno Setup 6 `ISCC.exe` is present).
- `checksums.sha256` — SHA256 integrity manifest.

---

## Installer Usage (`installer\Install-StickLock.ps1`)

The installer script can be run standalone from the repository or from inside an extracted distribution package:

```powershell
# Interactive installation (auto-detects updates or prompts for folder)
.\installer\Install-StickLock.ps1

# Non-interactive / unattended update or install
.\installer\Install-StickLock.ps1 -Unattended

# Install to a custom directory or drive
.\installer\Install-StickLock.ps1 -InstallPath "E:\Games\StickLock"
```
