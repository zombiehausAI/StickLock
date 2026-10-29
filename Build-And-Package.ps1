<#
.SYNOPSIS
    StickLock Automated Pipeline: Clean, Test, Build, Package, and Install.
.DESCRIPTION
    Automates the full release lifecycle for StickLock:
    1. Clean: Removes previous build artifacts, bin/obj caches, and staging directories.
    2. Test: Executes all unit test suites, verifying zero failures before packaging.
    3. Build: Compiles and publishes self-enclosed (self-contained) Release binaries for Windows x64.
    4. Package: Bundles the application, installer script, and creates a ZIP distribution package (and Inno Setup EXE if ISCC is installed).
    5. Install: Optional flag (-Install) to immediately deploy and test the installation locally.
.PARAMETER Configuration
    Build configuration (default: Release).
.PARAMETER SkipTests
    Skip running automated unit tests.
.PARAMETER Install
    Immediately install the built package locally for testing.
.PARAMETER InstallPath
    Target installation directory when -Install is specified.
.PARAMETER OutputDir
    Output directory for packaged release artifacts (default: artifacts).
.PARAMETER Version
    Override version string (defaults to version.json).
.EXAMPLE
    .\Build-And-Package.ps1
.EXAMPLE
    .\Build-And-Package.ps1 -Install
.EXAMPLE
    .\Build-And-Package.ps1 -SkipTests -Install -InstallPath "D:\StickLock"
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Configuration = "Release",
    [switch]$SkipTests,
    [switch]$Install,
    [string]$InstallPath = "",
    [string]$OutputDir = "artifacts",
    [string]$Version = "",
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ExtraArgs
)

# Normalize POSIX / double-dash flags (--Install, --SkipTests, --skip-tests, etc.)
$allPositional = @($Configuration) + @($ExtraArgs)
if ($Configuration -match '^--') {
    $Configuration = "Release"
}

foreach ($arg in $allPositional) {
    if ($arg -match '^--(install|i)$') {
        $Install = [switch]::Present
    } elseif ($arg -match '^--(skiptests|skip-tests|skip)$') {
        $SkipTests = [switch]::Present
    } elseif ($arg -match '^--(config|configuration)=(.*)$') {
        $Configuration = $Matches[2]
    } elseif ($arg -match '^--(installpath|install-path)=(.*)$') {
        $InstallPath = $Matches[2]
    } elseif ($arg -match '^--(outputdir|output-dir)=(.*)$') {
        $OutputDir = $Matches[2]
    } elseif ($arg -match '^--(version|v)=(.*)$') {
        $Version = $Matches[2]
    } elseif ($arg -match '^(Release|Debug)$') {
        $Configuration = $arg
    }
}

$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$artifactsPath = Join-Path $repoRoot $OutputDir
$stagingPath = Join-Path $artifactsPath "staging"
$packageAppDir = Join-Path $stagingPath "StickLock"
$appCsproj = Join-Path $repoRoot "StickLock.csproj"
$testsCsproj = Join-Path $repoRoot "StickLock.Tests\StickLock.Tests.csproj"
$solutionFile = Join-Path $repoRoot "StickLock.slnx"
if (-not (Test-Path $solutionFile)) {
    $solutionFile = Join-Path $repoRoot "StickLock.sln"
}

# Extract Version from -Version param, root version.json, installer\version.json, or default
$appVersion = $Version
if (-not $appVersion) {
    $candidatePaths = @(
        (Join-Path $repoRoot "version.json"),
        (Join-Path $repoRoot "installer\version.json")
    )
    foreach ($vPath in $candidatePaths) {
        if (Test-Path $vPath) {
            try {
                $vJson = Get-Content $vPath -Raw | ConvertFrom-Json
                if ($vJson.version) {
                    $appVersion = $vJson.version
                    break
                }
            } catch { }
        }
    }
}
if (-not $appVersion) {
    $appVersion = "1.0.0"
}

Write-Host "===========================================================" -ForegroundColor Cyan
Write-Host "       StickLock Build, Test & Packaging Pipeline          " -ForegroundColor Cyan
Write-Host "       Version: $appVersion | Target: win-x64 ($Configuration)  " -ForegroundColor Cyan
Write-Host "===========================================================" -ForegroundColor Cyan

# -------------------------------------------------------------
# STEP 1: CLEAN
# -------------------------------------------------------------
Write-Host "`n[1/4] Cleaning previous builds and caches..." -ForegroundColor Cyan

# Clean via dotnet
if (Test-Path $solutionFile) {
    Write-Host "  > Running dotnet clean..." -ForegroundColor Gray
    & dotnet clean $solutionFile -c $Configuration --verbosity quiet
}

# Clean artifacts and staging directories
if (Test-Path $artifactsPath) {
    Write-Host "  > Purging $artifactsPath..." -ForegroundColor Gray
    Remove-Item $artifactsPath -Recurse -Force -ErrorAction SilentlyContinue
}

# Clean bin and obj folders
Get-ChildItem -Path $repoRoot -Include "bin", "obj" -Directory -Recurse | ForEach-Object {
    try {
        Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
    } catch { }
}

New-Item -ItemType Directory -Path $stagingPath -Force | Out-Null
New-Item -ItemType Directory -Path $packageAppDir -Force | Out-Null
Write-Host "  > Clean completed." -ForegroundColor Green

# -------------------------------------------------------------
# STEP 2: TEST
# -------------------------------------------------------------
if (-not $SkipTests) {
    Write-Host "`n[2/4] Executing automated test suite..." -ForegroundColor Cyan
    if (Test-Path $testsCsproj) {
        & dotnet test $testsCsproj -c $Configuration --verbosity normal --nologo
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Test suite failed! Aborting packaging to protect build integrity."
            exit $LASTEXITCODE
        }
        Write-Host "  > All test suites passed successfully." -ForegroundColor Green
    } else {
        Write-Warning "Test project not found at $testsCsproj. Skipping tests."
    }
} else {
    Write-Host "`n[2/4] Skipping tests (-SkipTests specified)." -ForegroundColor Yellow
}

# -------------------------------------------------------------
# STEP 3: BUILD & PUBLISH (SELF-CONTAINED)
# -------------------------------------------------------------
Write-Host "`n[3/4] Publishing self-enclosed StickLock application ($Configuration)..." -ForegroundColor Cyan

$cleanNumericVersion = "1.0.0"
if ($appVersion -match '^(\d+\.\d+(\.\d+)?)') {
    $cleanNumericVersion = $Matches[1]
}

$publishArgs = @(
    "publish",
    $appCsproj,
    "-c", $Configuration,
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:Version=$cleanNumericVersion",
    "-p:AssemblyVersion=$cleanNumericVersion",
    "-p:FileVersion=$cleanNumericVersion",
    "-p:InformationalVersion=$appVersion",
    "-o", $packageAppDir
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

# Copy Installer Support Files & Documentation into package
Copy-Item (Join-Path $repoRoot "installer\Install-StickLock.ps1") -Destination $packageAppDir -Force
if (Test-Path (Join-Path $repoRoot "LICENSE")) {
    Copy-Item (Join-Path $repoRoot "LICENSE") -Destination $packageAppDir -Force
}
if (Test-Path (Join-Path $repoRoot "README.md")) {
    Copy-Item (Join-Path $repoRoot "README.md") -Destination $packageAppDir -Force
}
if (Test-Path (Join-Path $repoRoot "version.json")) {
    Copy-Item (Join-Path $repoRoot "version.json") -Destination $packageAppDir -Force
}

Write-Host "  > Self-enclosed application published to staging: $packageAppDir" -ForegroundColor Green

# -------------------------------------------------------------
# -------------------------------------------------------------
# STEP 4: PACKAGE
# -------------------------------------------------------------
Write-Host "`n[4/4] Creating distribution packages..." -ForegroundColor Cyan

$zipFilename = "StickLock-v$appVersion-win-x64.zip"
$zipPath = Join-Path $artifactsPath $zipFilename

Write-Host "  > Compressing ZIP archive: $zipFilename..." -ForegroundColor Gray
Compress-Archive -Path "$packageAppDir\*" -DestinationPath $zipPath -CompressionLevel Optimal -Force
Write-Host "  > ZIP distribution ready: $zipPath" -ForegroundColor Green

# Locate or auto-acquire Inno Setup compiler (ISCC.exe)
$isccRegistryPath = $null
try {
    $innoReg = Get-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*",
                                      "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*",
                                      "HKLM:\Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*" -ErrorAction SilentlyContinue |
               Where-Object { $_.DisplayName -match "Inno Setup" -and $_.InstallLocation } |
               Select-Object -First 1
    if ($innoReg -and $innoReg.InstallLocation) {
        $isccRegistryPath = Join-Path $innoReg.InstallLocation "ISCC.exe"
    }
} catch { }

$isccCandidates = @(
    (Get-Command iscc.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    $isccRegistryPath,
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)
$isccPath = $isccCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

# If Inno Setup is not installed, attempt automatic acquisition via winget
if (-not $isccPath) {
    $wingetCmd = Get-Command winget.exe -ErrorAction SilentlyContinue
    if ($wingetCmd) {
        Write-Host "  > Inno Setup compiler (ISCC.exe) not found. Attempting automatic installation via winget..." -ForegroundColor Yellow
        try {
            & winget install --id JRSoftware.InnoSetup --exact --accept-source-agreements --accept-package-agreements --silent
            $isccPath = $isccCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
            if ($isccPath) {
                Write-Host "  > Successfully installed Inno Setup: $isccPath" -ForegroundColor Green
            }
        } catch {
            Write-Warning "Could not automatically install Inno Setup via winget: $_"
        }
    }
}

$installerFilename = "StickLock-v$appVersion-Setup.exe"
$installerPath = Join-Path $artifactsPath $installerFilename
$installerBuilt = $false

if ($isccPath) {
    Write-Host "  > Found Inno Setup compiler: $isccPath" -ForegroundColor Gray
    Write-Host "  > Building Windows Setup Installer ($installerFilename)..." -ForegroundColor Gray
    $issScript = Join-Path $repoRoot "installer\StickLock.iss"
    if (Test-Path $issScript) {
        & $isccPath "/DMyAppVersion=$appVersion" "/DMySourceDir=$packageAppDir" "/O$artifactsPath" "/FStickLock-v$appVersion-Setup" $issScript
        if ($LASTEXITCODE -eq 0 -and (Test-Path $installerPath)) {
            Write-Host "  > Windows Setup Installer ready: $installerPath" -ForegroundColor Green
            $installerBuilt = $true
        } else {
            Write-Warning "Inno Setup compilation returned non-zero code ($LASTEXITCODE)."
        }
    } else {
        Write-Warning "Inno Setup script not found at $issScript."
    }
} else {
    Write-Warning "Inno Setup compiler (ISCC.exe) not found. To build the Windows Setup Installer EXE, install Inno Setup 6 or run 'winget install JRSoftware.InnoSetup'."
}

# Generate SHA256 Checksum Manifest
$checksumFile = Join-Path $artifactsPath "checksums.sha256"
$checksumLines = @()

if (Test-Path $zipPath) {
    $zHash = (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash
    $checksumLines += "$zHash  $zipFilename"
    Write-Host "  > Checksum (ZIP SHA256):       $zHash" -ForegroundColor Gray
}

if ($installerBuilt -and (Test-Path $installerPath)) {
    $eHash = (Get-FileHash -Path $installerPath -Algorithm SHA256).Hash
    $checksumLines += "$eHash  $installerFilename"
    Write-Host "  > Checksum (Installer SHA256): $eHash" -ForegroundColor Gray
}

$checksumLines | Out-File -FilePath $checksumFile -Encoding utf8

Write-Host "`n===========================================================" -ForegroundColor Green
Write-Host "             Packaging Pipeline Succeeded!                 " -ForegroundColor Green
Write-Host "  Output Directory: $artifactsPath                         " -ForegroundColor Green
Write-Host "  ZIP Package:      $zipFilename                           " -ForegroundColor Green
if ($installerBuilt) {
Write-Host "  Setup Installer:  $installerFilename                     " -ForegroundColor Green
}
Write-Host "===========================================================" -ForegroundColor Green

# -------------------------------------------------------------
# STEP 5: OPTIONAL LOCAL INSTALLATION & TESTING
# -------------------------------------------------------------
if ($Install) {
    Write-Host "`n[*] Starting immediate local installation for testing..." -ForegroundColor Magenta
    if ($installerBuilt -and (Test-Path $installerPath)) {
        Write-Host "  > Launching compiled Windows Setup Installer: $installerPath" -ForegroundColor Cyan
        $setupArgs = @()
        if ($InstallPath) {
            $setupArgs += "/DIR=`"$InstallPath`""
        }
        Start-Process -FilePath $installerPath -ArgumentList $setupArgs -Wait
    } else {
        $installerScript = Join-Path $repoRoot "installer\Install-StickLock.ps1"
        if (Test-Path $installerScript) {
            & $installerScript -SourceDir $packageAppDir -InstallPath $InstallPath
        } else {
            Write-Error "Installer script not found at $installerScript."
            exit 1
        }
    }
} else {
    Write-Host "`nTo install and test this build now, run:" -ForegroundColor Cyan
    if ($installerBuilt) {
        Write-Host "  $installerPath" -ForegroundColor White
    }
    Write-Host "  .\Build-And-Package.ps1 -Install" -ForegroundColor White
    Write-Host "or extract and execute:" -ForegroundColor Cyan
    Write-Host "  $zipPath" -ForegroundColor White
}

