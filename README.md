# StickLock

[![Windows](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-blue?logo=windows)](https://microsoft.com)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple?logo=dotnet)](https://dotnet.microsoft.com)
[![License](https://img.shields.io/badge/License-MIT-green)](LICENSE)
[![Ko-fi](https://img.shields.io/badge/Ko--fi-Support-FF5E5B?logo=ko-fi&logoColor=white)](https://ko-fi.com/zombiehaus)

**Star Citizen Flight Controller Slot Synchronizer & Anti-Inversion Daemon**

StickLock is a zero-XAML, high-performance Windows desktop utility built with **.NET 10**, **WinForms Blazor Hybrid** (`Microsoft.AspNetCore.Components.WebView.WindowsForms`), and **MudBlazor**.

Star Citizen relies on Windows DirectInput enumeration order (`js1`, `js2`, etc.) to map physical flight sticks. When joysticks are unplugged, USB hubs reset, or Windows reboots, DirectInput frequently flips their enumeration order. StickLock runs quietly in the Windows System Tray, monitors USB hardware changes in real time, compares current DirectInput device indices against user-locked canonical slots, and automatically updates Star Citizen's `actionmaps.xml` before game launch.

> [!NOTE]
> **Project Context & Disclaimer**:
> - **Solo Developer Side Project**: StickLock is an independent side project created and maintained by a single developer. Due to full-time work and outside commitments, I'm unable to dedicate full-time hours to it—updates, bug fixes, and maintenance happen as personal time permits. Your patience and understanding are greatly appreciated!
> - **Proof of Concept**: This application, setup scripts, utilities, and technical documentation represent an early **proof of concept** created to overcome Windows DirectInput enumeration order quirks and Star Citizen device mapping hurdles. While these setups have been tested and verified across local Windows environments (such as Windows 10 and 11) and dual-stick / HOSAS / HOTAS hardware configurations, hardware setups, Windows driver versions, and USB topologies can differ significantly—what works seamlessly in one test environment may require adjustments or further testing on yours. Community feedback, issue reports, and real-world testing are warmly welcomed!
> - **AI-Assisted Development**: This application and documentation were created with AI assistance. In the spirit of complete transparency: if you prefer not to use AI-assisted code, please feel free to pass on this project.

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
- **Centralized `.sticklock` User Directory & Immune Mappings Library**:
  - All configurations, profile backups, and persistent mappings are stored in `~/.sticklock` (`%USERPROFILE%\.sticklock\`), completely outside the Star Citizen install folder so they are immune to game reinstalls and USER folder wipes.
  - `~/.sticklock/mappings/`: Primary repository for all saved flight controller mapping profiles.
  - `~/.sticklock/config.json`: Active StickLock settings and canonical joystick assignments.
  - `~/.sticklock/settings_backups/`: Timestamped snapshots created via the one-click **"Backup Current Settings"** button.
  - `~/.sticklock/backups/`: ActionMaps XML backups created prior to sync operations or profile imports.
  - `~/.sticklock/exports/`: Default repository for exported custom control profiles.
- **Two-Way Star Citizen USER Sync**:
  - **Save Active as Profile**: Snapshot current `actionmaps.xml` directly into `~/.sticklock/mappings/<name>.xml`.
  - **Deploy / Sync to Star Citizen**: Automatically copies profiles from `~/.sticklock/mappings/` to Star Citizen's `USER\Client\0\Controls\Mappings\` folder so you can select and modify them in-game under Control Profiles.
  - **Pull from Star Citizen**: Imports custom XML mappings from Star Citizen's `USER\Client\0\Controls\Mappings\` folder into `~/.sticklock/mappings/` so any in-game edits are safely preserved.
  - **Apply to Active XML**: Applies any saved profile directly to the active `actionmaps.xml` and immediately aligns DirectInput stick slots.
  - **Open Mappings Folder**: Instant one-click Explorer button to browse your `.sticklock/mappings` folder.
- **Export & Import Profiles**:
  - Export active control profiles as user-named XML files directly to any custom folder.
  - Import any Star Citizen mapping XML, inspect device and bind counts, and apply it directly to the active `actionmaps.xml` with automated backup and instant physical DirectInput slot alignment.
  - One-click **"Backup Current Settings"** to snapshot canonical stick assignments, paths, and preferences at any time.
  - Export and import StickLock configuration JSON to easily migrate settings across machines.
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

---

## Development Transparency

This project and its accompanying utilities were developed with AI assistance (*Assisted by AI*). We believe in open transparency: if you prefer not to use or engage with AI-assisted software, please be aware before installing, evaluating, or running this application.

## Support & Contributions

If you find StickLock helpful for your Star Citizen setup or flight sim configuration, consider supporting ongoing development:

[![Support me on Ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/zombiehaus)

## License

This project is licensed under the [MIT License](LICENSE).


