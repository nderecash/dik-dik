<#
.SYNOPSIS
    Turns a freshly created Unity project into the Dik-dik project.

.DESCRIPTION
    Every version and setting here was arrived at by hitting the failure first. The
    comments say which failure, so nobody (including me, next week) "tidies" one of
    these back to the value that does not work.

    Run this once against a project made with:
        Unity.exe -batchmode -quit -createProject <path>

.PARAMETER ProjectPath
    The Unity project folder. Must not be inside OneDrive: Unity's Library folder
    generates thousands of small writes and sync will corrupt or throttle it.

.EXAMPLE
    .\tools\configure-project.ps1 -ProjectPath C:\dev\dik-dik
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectPath
)

$ErrorActionPreference = 'Stop'
$staging = Split-Path -Parent $PSScriptRoot

if (-not (Test-Path (Join-Path $ProjectPath 'ProjectSettings'))) {
    throw "$ProjectPath does not look like a Unity project."
}

if ($ProjectPath -like '*OneDrive*') {
    throw "Refusing to configure a project inside OneDrive. Unity's Library folder will be corrupted by sync. Use C:\dev\ instead."
}

# ---------------------------------------------------------------------------
# Packages
# ---------------------------------------------------------------------------
$manifestPath = Join-Path $ProjectPath 'Packages\manifest.json'
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json

$packages = @{
    # The bindings. Pinned to the repo because there is no registry release.
    'com.whisper.unity'     = 'https://github.com/Macoron/whisper.unity.git?path=/Packages/com.whisper.unity'

    # REQUIRED, and whisper.unity does not declare it. Its MicrophoneRecord.cs and
    # UiUtils.cs use UnityEngine.UI (Image, Dropdown, ScrollRect) but its package.json
    # has no dependencies block at all. Without this the package will not compile in a
    # minimal project. It only works for most people because the default 3D template
    # happens to include ugui.
    'com.unity.ugui'        = '2.0.0'

    # 1.20.0, NOT the 1.12.0 that Unity's own 3D template declares. Unity 6.5 made
    # TreeView/TreeViewItem/TreeViewState obsolete-as-error, and 1.12.0's editor windows
    # still use them, so it fails to compile. Do not "match the template" here.
    'com.unity.inputsystem' = '1.20.0'
}

foreach ($name in $packages.Keys) {
    $manifest.dependencies | Add-Member -NotePropertyName $name -NotePropertyValue $packages[$name] -Force
    Write-Host "package  $name = $($packages[$name])"
}

# WriteAllText with UTF8Encoding($false), never Set-Content -Encoding utf8.
# PowerShell 5.1 writes a byte order mark, and Unity's JSON parser rejects it outright
# with "Non-whitespace before {  Char: 65279". Same applies to any YAML asset below.
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 10), $utf8NoBom)
Write-Host "wrote    manifest.json (no BOM)"

# ---------------------------------------------------------------------------
# Project settings
# ---------------------------------------------------------------------------
$settingsPath = Join-Path $ProjectPath 'ProjectSettings\ProjectSettings.asset'
$settings = Get-Content $settingsPath -Raw

# 2 = both input backends. Keeps the new Input System available without breaking
# anything that still reads the old one.
if ($settings -match 'activeInputHandler:\s*\d') {
    $settings = $settings -replace 'activeInputHandler:\s*\d', 'activeInputHandler: 2'
    [System.IO.File]::WriteAllText($settingsPath, $settings, $utf8NoBom)
    Write-Host "set      activeInputHandler = 2 (both)"
}
else {
    Write-Warning "activeInputHandler not found in ProjectSettings.asset"
}

# ---------------------------------------------------------------------------
# Source
# ---------------------------------------------------------------------------
foreach ($folder in @('Assets\Scripts', 'Assets\Editor')) {
    $source = Join-Path $staging $folder
    if (-not (Test-Path $source)) { continue }

    $target = Join-Path $ProjectPath $folder
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Copy-Item "$source\*" $target -Recurse -Force
    Write-Host "copied   $folder"
}

foreach ($file in @('.gitignore', 'README.md')) {
    $source = Join-Path $staging $file
    if (Test-Path $source) {
        Copy-Item $source (Join-Path $ProjectPath $file) -Force
        Write-Host "copied   $file"
    }
}

$toolsTarget = Join-Path $ProjectPath 'tools'
New-Item -ItemType Directory -Path $toolsTarget -Force | Out-Null
Copy-Item (Join-Path $staging 'tools\*') $toolsTarget -Recurse -Force
Write-Host "copied   tools"

# ---------------------------------------------------------------------------
# Model weights
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "Fetching model weights..."
& (Join-Path $toolsTarget 'fetch-models.ps1')

Write-Host ""
Write-Host "Done. Next:"
Write-Host "  Unity.exe -batchmode -quit -projectPath $ProjectPath -executeMethod DikdikBuild.Windows -logFile build.log"
Write-Host ""
Write-Host "Verify the .exe exists afterwards. Unity batch mode on Windows detaches, so"
Write-Host "the shell's exit code tells you nothing useful about whether the build ran."
