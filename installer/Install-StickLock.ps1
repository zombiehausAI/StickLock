<#
.SYNOPSIS
    StickLock Windows Desktop Installer & Updater Script
.DESCRIPTION
    Installs or updates the self-enclosed StickLock utility for Star Citizen flight controller sync.
    Autodetects existing installations, supports in-place updating, allows custom installation paths
    on any drive, provisions shortcuts, and registers with Windows Installed Apps.
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$InstallPath = "",
    [string]$SourceDir = "",
    [switch]$SkipPrereqCheck,
    [switch]$CreateDesktopShortcut = $true,
    [switch]$CreateStartMenuShortcut = $true,
    [switch]$Unattended,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ExtraArgs
)

# Normalize POSIX / double-dash flags (--Unattended, --InstallPath=..., etc.)
$allPositional = @($InstallPath) + @($ExtraArgs)
if ($InstallPath -match '^--') {
    $InstallPath = ""
}

foreach ($arg in $allPositional) {
    if ($arg -match '^--(unattended|u)$') {
        $Unattended = [switch]::Present
    } elseif ($arg -match '^--(skipprereqcheck|skip-prereq-check|skip-prereq)$') {
        $SkipPrereqCheck = [switch]::Present
    } elseif ($arg -match '^--(nodesktopshortcut|no-desktop-shortcut)$') {
        $CreateDesktopShortcut = [switch]::False
    } elseif ($arg -match '^--(nostartmenushortcut|no-start-menu-shortcut)$') {
        $CreateStartMenuShortcut = [switch]::False
    } elseif ($arg -match '^--(installpath|install-path)=(.*)$') {
        $InstallPath = $Matches[2]
    } elseif ($arg -match '^--(sourcedir|source-dir)=(.*)$') {
        $SourceDir = $Matches[2]
    } elseif (-not [string]::IsNullOrWhiteSpace($arg) -and -not ($arg -match '^--')) {
        $InstallPath = $arg
    }
}

$ErrorActionPreference = "Stop"

Write-Host "===========================================================" -ForegroundColor Cyan
Write-Host "       StickLock Desktop Installer & Updater Pipeline      " -ForegroundColor Cyan
Write-Host "===========================================================" -ForegroundColor Cyan

# -------------------------------------------------------------
# STEP 1: RUNNING PROCESS CHECK
# -------------------------------------------------------------
Write-Host "`n[1/5] Checking for running instances..." -ForegroundColor Cyan
$runningProcesses = Get-Process StickLock -ErrorAction SilentlyContinue
if ($runningProcesses) {
    Write-Host "  > Active StickLock process detected." -ForegroundColor Yellow
    if ($Unattended -or [Console]::IsInputRedirected) {
        Write-Host "  > Closing StickLock to apply updates (Unattended mode)..." -ForegroundColor Gray
        $runningProcesses | Stop-Process -Force
        Start-Sleep -Seconds 1
    } else {
        Write-Host "StickLock is currently running and must be closed to update binaries." -ForegroundColor Yellow
        $closeChoice = Read-Host "Close StickLock now? (Y/n) [Default: Y]"
        if ([string]::IsNullOrWhiteSpace($closeChoice) -or $closeChoice.Trim() -match '^[Yy]') {
            $runningProcesses | Stop-Process -Force
            Start-Sleep -Seconds 1
            Write-Host "  > StickLock closed." -ForegroundColor Green
        } else {
            Write-Error "Installation cancelled: StickLock must be closed to proceed."
            exit 1
        }
    }
} else {
    Write-Host "  > No conflicting processes running." -ForegroundColor Green
}

# -------------------------------------------------------------
# STEP 2: DETECT EXISTING INSTALLATION & TARGET FOLDER
# -------------------------------------------------------------
Write-Host "`n[2/5] Detecting existing installation & selecting directory..." -ForegroundColor Cyan

$defaultInstallPath = Join-Path $env:LOCALAPPDATA "StickLock\app"
$existingInstall = $null
$isUpdate = $false

function Find-StickLockExe($folder) {
    if ([string]::IsNullOrWhiteSpace($folder)) {
        return $null
    }
    $candidates = @(
        (Join-Path $folder "StickLock.exe"),
        (Join-Path $folder "bin\StickLock.exe")
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) {
            return $c
        }
    }
    return $null
}

# Check Registry: HKCU\Software\StickLock -> InstallPath
try {
    $regPath = (Get-ItemProperty -Path "HKCU:\Software\StickLock" -Name "InstallPath" -ErrorAction SilentlyContinue).InstallPath
    if ($regPath -and (Find-StickLockExe $regPath)) {
        $existingInstall = $regPath.TrimEnd('\', '/')
    }
} catch { }

# Check Uninstall Registry
if (-not $existingInstall) {
    try {
        $uninstallReg = Get-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\StickLock",
                                               "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\StickLock" -ErrorAction SilentlyContinue |
                        Where-Object { $_.InstallLocation -and (Find-StickLockExe $_.InstallLocation) } |
                        Select-Object -First 1
        if ($uninstallReg) {
            $existingInstall = $uninstallReg.InstallLocation.TrimEnd('\', '/')
        }
    } catch { }
}

# Check Shortcuts
if (-not $existingInstall) {
    try {
        $shortcutDirs = @(
            [Environment]::GetFolderPath([Environment+SpecialFolder]::Desktop),
            [Environment]::GetFolderPath([Environment+SpecialFolder]::CommonDesktopDirectory),
            [Environment]::GetFolderPath([Environment+SpecialFolder]::Programs),
            [Environment]::GetFolderPath([Environment+SpecialFolder]::CommonPrograms)
        )
        $wshShell = New-Object -ComObject WScript.Shell
        foreach ($sDir in $shortcutDirs) {
            if (-not (Test-Path $sDir)) { continue }
            $matchingLnk = Get-ChildItem -Path $sDir -Filter "*StickLock*.lnk" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($matchingLnk) {
                $sc = $wshShell.CreateShortcut($matchingLnk.FullName)
                if ($sc.TargetPath -and (Test-Path $sc.TargetPath)) {
                    $targetDir = Split-Path $sc.TargetPath -Parent
                    if (Test-Path (Join-Path $targetDir "StickLock.exe")) {
                        $existingInstall = $targetDir
                        break
                    }
                }
            }
        }
    } catch { }
}

# Check Default Path
if (-not $existingInstall) {
    if (Find-StickLockExe $defaultInstallPath) {
        $existingInstall = $defaultInstallPath
    }
}

# Check Common root paths
if (-not $existingInstall) {
    $commonRoots = @("C:\StickLock", "D:\StickLock", "E:\StickLock", "C:\Games\StickLock", "D:\Games\StickLock")
    foreach ($cand in $commonRoots) {
        if (Find-StickLockExe $cand) {
            $existingInstall = $cand
            break
        }
    }
}

# If user passed explicit path
if (-not [string]::IsNullOrWhiteSpace($InstallPath)) {
    if ($existingInstall -and ($InstallPath -eq $existingInstall)) {
        $isUpdate = $true
    }
} elseif ($existingInstall) {
    $existingVer = "Unknown"
    $foundExe = Find-StickLockExe $existingInstall
    if ($foundExe) {
        try {
            $existingVer = (Get-Item $foundExe).VersionInfo.ProductVersion
            if (-not $existingVer) {
                $existingVer = (Get-Item $foundExe).VersionInfo.FileVersion
            }
        } catch { }
    }

    Write-Host "  > Existing installation detected at: $existingInstall (v$existingVer)" -ForegroundColor Green

    if ($Unattended -or [Console]::IsInputRedirected) {
        $InstallPath = $existingInstall
        $isUpdate = $true
    } else {
        Write-Host "`nWould you like to update this installation?" -ForegroundColor Yellow
        Write-Host "  [Enter]  Update existing install at $existingInstall" -ForegroundColor White
        Write-Host "  [C]      Choose a different folder or drive" -ForegroundColor Gray
        $choice = Read-Host "`nChoice [Default: Update]"

        if ([string]::IsNullOrWhiteSpace($choice) -or ($choice.Trim() -ne "C" -and $choice.Trim() -ne "c")) {
            $InstallPath = $existingInstall
            $isUpdate = $true
            Write-Host "  > Selected: Update existing installation" -ForegroundColor Green
        }
    }
}

if ([string]::IsNullOrWhiteSpace($InstallPath)) {
    if ($Unattended -or [Console]::IsInputRedirected) {
        $InstallPath = $defaultInstallPath
    } else {
        Write-Host "`nChoose where to install StickLock (can be on any drive, e.g. D:\StickLock):" -ForegroundColor Yellow
        Write-Host "  [Enter]  Use default: $defaultInstallPath" -ForegroundColor Gray
        Write-Host "  [B]      Browse for a folder with Windows dialog" -ForegroundColor Gray
        Write-Host "  Or type a custom path directly (e.g. D:\StickLock or C:\Games\StickLock)" -ForegroundColor Gray

        $userInput = Read-Host "`nInstall path [Default: $defaultInstallPath]"
        if ([string]::IsNullOrWhiteSpace($userInput)) {
            $InstallPath = $defaultInstallPath
        } elseif ($userInput.Trim() -eq "B" -or $userInput.Trim() -eq "b") {
            try {
                Add-Type -AssemblyName System.Windows.Forms
                $browser = New-Object System.Windows.Forms.FolderBrowserDialog
                $browser.Description = "Select StickLock Installation Folder (Any Drive)"
                $browser.UseDescriptionForTitle = $true
                $browser.ShowNewFolderButton = $true
                if ($browser.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK -and -not [string]::IsNullOrWhiteSpace($browser.SelectedPath)) {
                    $InstallPath = $browser.SelectedPath
                } else {
                    Write-Host "  > No folder selected in dialog. Using default." -ForegroundColor Yellow
                    $InstallPath = $defaultInstallPath
                }
            } catch {
                Write-Warning "GUI folder picker unavailable. Using default: $defaultInstallPath"
                $InstallPath = $defaultInstallPath
            }
        } else {
            $InstallPath = $userInput.Trim().Trim('"', "'")
        }
    }
}

try {
    $InstallPath = [System.IO.Path]::GetFullPath($InstallPath)
} catch {
    Write-Warning "Invalid path provided. Falling back to default: $defaultInstallPath"
    $InstallPath = $defaultInstallPath
}

if ($isUpdate) {
    Write-Host "  > Target path (Updating in-place): $InstallPath" -ForegroundColor Green
} else {
    Write-Host "  > Target path (New Installation):  $InstallPath" -ForegroundColor Green
}

# Display drive space info
try {
    $driveRoot = [System.IO.Path]::GetPathRoot($InstallPath)
    if ($driveRoot) {
        $driveLetter = $driveRoot.Substring(0, 1)
        $diskInfo = Get-CimInstance -ClassName Win32_LogicalDisk -Filter "DeviceID='$($driveLetter):'" -ErrorAction SilentlyContinue
        if ($diskInfo -and $diskInfo.FreeSpace) {
            $freeGb = [math]::Round($diskInfo.FreeSpace / 1GB, 1)
            Write-Host "  > Target Drive ($($driveLetter):): $freeGb GB free space available" -ForegroundColor Gray
        }
    }
} catch { }

New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null

# -------------------------------------------------------------
# STEP 3: PROVISION BINARIES
# -------------------------------------------------------------
Write-Host "`n[3/5] Deploying self-enclosed StickLock binaries..." -ForegroundColor Cyan

$scriptRoot = $PSScriptRoot
$repoRoot = Split-Path $scriptRoot -Parent
$resolvedSource = $null

# Priority 1: Explicit -SourceDir parameter
if ($SourceDir -and (Test-Path (Join-Path $SourceDir "StickLock.exe"))) {
    $resolvedSource = $SourceDir
}

# Priority 2: Current script directory (if bundled into a package/archive)
if (-not $resolvedSource -and (Test-Path (Join-Path $scriptRoot "StickLock.exe"))) {
    $resolvedSource = $scriptRoot
}

# Priority 3: Staging directory in repo
$stagingCandidate = Join-Path $repoRoot "artifacts\staging\StickLock"
if (-not $resolvedSource -and (Test-Path (Join-Path $stagingCandidate "StickLock.exe"))) {
    $resolvedSource = $stagingCandidate
}

# Priority 4: Build from repo source using dotnet publish if source is available
$appCsproj = Join-Path $repoRoot "StickLock.csproj"
if (-not $resolvedSource -and (Test-Path $appCsproj)) {
    Write-Host "  > Compiling self-contained Release binaries via dotnet publish..." -ForegroundColor Gray
    & dotnet publish $appCsproj -c Release -r win-x64 --self-contained true -o $InstallPath
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet publish failed with code $LASTEXITCODE."
        exit $LASTEXITCODE
    }
    Write-Host "  > Compiled and deployed directly to $InstallPath." -ForegroundColor Green
} elseif ($resolvedSource) {
    Write-Host "  > Copying files from $resolvedSource..." -ForegroundColor Gray
    Copy-Item -Path "$resolvedSource\*" -Destination $InstallPath -Recurse -Force
    Write-Host "  > Binaries deployed." -ForegroundColor Green
} else {
    Write-Error "Could not locate StickLock binaries or project source to install."
    exit 1
}

# -------------------------------------------------------------
# STEP 4: SHORTCUTS & WINDOWS REGISTRATION
# -------------------------------------------------------------
Write-Host "`n[4/5] Configuring shortcuts and Windows registration..." -ForegroundColor Cyan

$targetExe = Join-Path $InstallPath "StickLock.exe"
if (-not (Test-Path $targetExe)) {
    Write-Error "Installation verification failed: StickLock.exe not found at $targetExe."
    exit 1
}

# Desktop Shortcut
if ($CreateDesktopShortcut) {
    try {
        $desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::Desktop)
        $shortcutPath = Join-Path $desktop "StickLock.lnk"
        $wshShell = New-Object -ComObject WScript.Shell
        $sc = $wshShell.CreateShortcut($shortcutPath)
        $sc.TargetPath = $targetExe
        $sc.WorkingDirectory = $InstallPath
        $sc.IconLocation = "$targetExe,0"
        $sc.Description = "StickLock - Star Citizen Flight Controller Sync"
        $sc.Save()
        Write-Host "  > Desktop shortcut created: $shortcutPath" -ForegroundColor Green
    } catch {
        Write-Warning "Could not create desktop shortcut: $_"
    }
}

# Start Menu Shortcut
if ($CreateStartMenuShortcut) {
    try {
        $startMenuPrograms = [Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)
        $stickLockMenuDir = Join-Path $startMenuPrograms "StickLock"
        New-Item -ItemType Directory -Path $stickLockMenuDir -Force | Out-Null

        $menuShortcutPath = Join-Path $stickLockMenuDir "StickLock.lnk"
        $wshShell = New-Object -ComObject WScript.Shell
        $sc = $wshShell.CreateShortcut($menuShortcutPath)
        $sc.TargetPath = $targetExe
        $sc.WorkingDirectory = $InstallPath
        $sc.IconLocation = "$targetExe,0"
        $sc.Description = "StickLock - Star Citizen Flight Controller Sync"
        $sc.Save()
        Write-Host "  > Start Menu shortcut created: $menuShortcutPath" -ForegroundColor Green
    } catch {
        Write-Warning "Could not create Start Menu shortcut: $_"
    }
}

# Save InstallPath to Registry for future update detection
try {
    New-Item -Path "HKCU:\Software\StickLock" -Force -ErrorAction SilentlyContinue | Out-Null
    Set-ItemProperty -Path "HKCU:\Software\StickLock" -Name "InstallPath" -Value $InstallPath -Force -ErrorAction SilentlyContinue
} catch { }

# Create Uninstaller Script inside install directory
$uninstallerScript = Join-Path $InstallPath "Uninstall-StickLock.ps1"
$uninstallerContent = @"
<#
.SYNOPSIS
    StickLock Uninstaller Script
#>
Write-Host "Uninstalling StickLock..." -ForegroundColor Cyan

# Stop running process
Get-Process StickLock -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# Remove shortcuts
`$desktopLnk = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::Desktop)) "StickLock.lnk"
if (Test-Path `$desktopLnk) { Remove-Item `$desktopLnk -Force -ErrorAction SilentlyContinue }

`$startMenuDir = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::Programs)) "StickLock"
if (Test-Path `$startMenuDir) { Remove-Item `$startMenuDir -Recurse -Force -ErrorAction SilentlyContinue }

# Remove registry entries
Remove-Item -Path "HKCU:\Software\StickLock" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\StickLock" -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "StickLock shortcuts and registration removed." -ForegroundColor Green
Write-Host "You may now safely delete: `"$InstallPath`"" -ForegroundColor Yellow
"@
Set-Content -Path $uninstallerScript -Value $uninstallerContent -Encoding utf8

# Register in Windows Add/Remove Programs (HKCU uninstall)
try {
    $uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\StickLock"
    New-Item -Path $uninstallKey -Force -ErrorAction SilentlyContinue | Out-Null
    Set-ItemProperty -Path $uninstallKey -Name "DisplayName" -Value "StickLock" -Force
    Set-ItemProperty -Path $uninstallKey -Name "DisplayVersion" -Value "1.0.0" -Force
    Set-ItemProperty -Path $uninstallKey -Name "Publisher" -Value "StickLock Avionics" -Force
    Set-ItemProperty -Path $uninstallKey -Name "InstallLocation" -Value $InstallPath -Force
    Set-ItemProperty -Path $uninstallKey -Name "DisplayIcon" -Value "$targetExe,0" -Force
    Set-ItemProperty -Path $uninstallKey -Name "UninstallString" -Value "powershell.exe -ExecutionPolicy Bypass -NoProfile -File `"$uninstallerScript`"" -Force
    Set-ItemProperty -Path $uninstallKey -Name "NoModify" -Value 1 -Type DWord -Force
    Set-ItemProperty -Path $uninstallKey -Name "NoRepair" -Value 1 -Type DWord -Force
} catch { }

# -------------------------------------------------------------
# STEP 5: COMPLETION
# -------------------------------------------------------------
Write-Host "`n[5/5] Finalizing installation..." -ForegroundColor Cyan
Write-Host "`n===========================================================" -ForegroundColor Green
if ($isUpdate) {
    Write-Host "           StickLock successfully updated!                 " -ForegroundColor Green
} else {
    Write-Host "           StickLock successfully installed!               " -ForegroundColor Green
}
Write-Host "  Location: $InstallPath                                   " -ForegroundColor Green
Write-Host "  Binary:   $targetExe                                     " -ForegroundColor Green
Write-Host "===========================================================" -ForegroundColor Green

if (-not $Unattended -and -not [Console]::IsInputRedirected) {
    $launchChoice = Read-Host "`nLaunch StickLock now? (Y/n) [Default: Y]"
    if ([string]::IsNullOrWhiteSpace($launchChoice) -or $launchChoice.Trim() -match '^[Yy]') {
        Start-Process -FilePath $targetExe -WorkingDirectory $InstallPath
    }
}

