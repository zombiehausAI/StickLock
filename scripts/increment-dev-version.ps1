$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path "$PSScriptRoot/..").Path
$versionFile = Join-Path $repoRoot "version.json"

if (-not (Test-Path $versionFile)) {
    Write-Host "version.json not found at $versionFile"
    exit 0
}

$branch = (git -C $repoRoot rev-parse --abbrev-ref HEAD 2>$null).Trim()
if ($branch -eq "dev") {
    $content = Get-Content $versionFile -Raw | ConvertFrom-Json
    $oldBuild = [int]$content.devBuild
    $newBuild = $oldBuild + 1
    $content.devBuild = $newBuild

    $jsonStr = $content | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText($versionFile, $jsonStr + [System.Environment]::NewLine, [System.Text.Encoding]::UTF8)

    Write-Host "Incremented devBuild from $oldBuild to $newBuild on branch '$branch'."
    git -C $repoRoot add "$versionFile"
} else {
    Write-Host "Branch is '$branch' (not 'dev'). Skipping version auto-increment."
}
