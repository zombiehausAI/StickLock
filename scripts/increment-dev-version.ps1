$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path "$PSScriptRoot/..").Path
$versionFile = Join-Path $repoRoot "version.json"

if (-not (Test-Path $versionFile)) {
    Write-Host "version.json not found at $versionFile"
    exit 0
}

$branch = (git -C $repoRoot rev-parse --abbrev-ref HEAD 2>$null).Trim()
if ($branch -eq "dev" -or $branch.StartsWith("feature/")) {
    $content = Get-Content $versionFile -Raw | ConvertFrom-Json
    $oldBuild = [int]$content.devBuild
    $newBuild = $oldBuild + 1
    $content.devBuild = $newBuild
    $content.version = "$($content.major).$($content.minor).$($content.patch)"

    $jsonStr = $content | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText($versionFile, $jsonStr + [System.Environment]::NewLine, [System.Text.Encoding]::UTF8)

    $tier = if ($branch -eq "dev") { "beta" } else { "alpha" }
    Write-Host "Incremented build from $oldBuild to $newBuild ($($content.major).$($content.minor).$($content.patch)-$tier.$newBuild) on branch '$branch'."
    git -C $repoRoot add "$versionFile"
} else {
    Write-Host "Branch is '$branch' (not dev/feature). Skipping version auto-increment."
}

