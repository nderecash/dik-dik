# Package the WebGL build for itch.io.
#
# Two things itch is fussy about, both of which this gets right:
#
#   1. index.html has to be at the ROOT of the zip, not inside a folder. Upload a zip
#      with everything one level down and itch serves a directory listing.
#
#   2. Entry names have to use forward slashes. The zip spec requires them, and both
#      Compress-Archive and .NET's ZipFile.CreateFromDirectory write the Windows
#      separator instead. Most extractors cope. Relying on "most" for the one artefact
#      that is meant to be public is not worth the fifteen lines below.

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$source = Join-Path $PSScriptRoot '..\Builds\Web' | Resolve-Path
$zip = Join-Path $PSScriptRoot '..\Builds\dikdik-web.zip'

if (Test-Path $zip) { Remove-Item $zip -Force }

$stream = [System.IO.File]::Open($zip, [System.IO.FileMode]::CreateNew)
$archive = New-Object System.IO.Compression.ZipArchive(
    $stream, [System.IO.Compression.ZipArchiveMode]::Create)

$prefix = $source.Path.Length + 1
$level = [System.IO.Compression.CompressionLevel]::Optimal
$separator = [char]92
$forward = [char]47

foreach ($file in Get-ChildItem $source -Recurse -File) {
    $name = $file.FullName.Substring($prefix).Replace($separator, $forward)
    [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $archive, $file.FullName, $name, $level)
}

$archive.Dispose()
$stream.Dispose()

# Verify, rather than assume. Both properties above are invisible until upload fails.
$check = [System.IO.Compression.ZipFile]::OpenRead($zip)
$bad = @($check.Entries | Where-Object { $_.FullName.Contains($separator) }).Count
$root = [bool]($check.Entries | Where-Object { $_.FullName -eq 'index.html' })
$count = $check.Entries.Count
$check.Dispose()

"{0:N1} MB, {1} entries" -f ((Get-Item $zip).Length / 1MB), $count
"index.html at root : $root"
"backslash entries  : $bad"

if (-not $root -or $bad -gt 0) {
    Write-Error "Zip is not in the shape itch.io needs."
    exit 1
}

"OK: $zip"
