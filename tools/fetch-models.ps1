<#
.SYNOPSIS
    Downloads the Whisper model weights this project needs.

.DESCRIPTION
    Model weights are not in git. A 74 MB binary in version control is a repository
    everyone has to clone forever, and the weights are not ours to redistribute anyway.
    This fetches them into StreamingAssets, where whisper.unity expects to find them.

    Safe to run repeatedly. Existing files are verified and skipped.

.PARAMETER Model
    Which weights to fetch. Default is the two small English-capable ones.
      tiny     multilingual, ships with whisper.unity, the baseline
      tiny.en  English only, usually better than tiny on English
      base.en  larger and slower, noticeably better on under-represented accents
      all      all three

.EXAMPLE
    .\tools\fetch-models.ps1
    .\tools\fetch-models.ps1 -Model all
#>

[CmdletBinding()]
param(
    [ValidateSet('tiny', 'tiny.en', 'base.en', 'all', 'default')]
    [string]$Model = 'default'
)

$ErrorActionPreference = 'Stop'

# Repository root is the parent of the folder holding this script.
$root = Split-Path -Parent $PSScriptRoot
$dest = Join-Path $root 'Assets\StreamingAssets\Whisper'

# ggerganov/whisper.cpp is the canonical home of the GGML conversions.
# Note: the ggml-org mirror returns 401 on direct file requests, so do not "helpfully"
# switch this over without testing an actual download first.
$base = 'https://huggingface.co/ggerganov/whisper.cpp/resolve/main'

$catalogue = @{
    'tiny'    = @{ File = 'ggml-tiny.bin';    ApproxMB = 75  }
    'tiny.en' = @{ File = 'ggml-tiny.en.bin'; ApproxMB = 75  }
    'base.en' = @{ File = 'ggml-base.en.bin'; ApproxMB = 142 }
}

$wanted = switch ($Model) {
    'all'     { @('tiny', 'tiny.en', 'base.en') }
    'default' { @('tiny', 'tiny.en') }
    default   { @($Model) }
}

if (-not (Test-Path $dest)) {
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    Write-Host "Created $dest"
}

foreach ($key in $wanted) {
    $entry = $catalogue[$key]
    $target = Join-Path $dest $entry.File

    if (Test-Path $target) {
        $sizeMB = [math]::Round((Get-Item $target).Length / 1MB, 1)
        if ($sizeMB -gt ($entry.ApproxMB * 0.9)) {
            Write-Host "ok       $($entry.File) already present ($sizeMB MB)"
            continue
        }

        Write-Warning "$($entry.File) looks truncated ($sizeMB MB). Downloading again."
    }

    $url = "$base/$($entry.File)"
    Write-Host "download $($entry.File) (about $($entry.ApproxMB) MB) ..."

    try {
        Invoke-WebRequest -Uri $url -OutFile $target -UseBasicParsing -TimeoutSec 1800
    }
    catch {
        Write-Error "Failed to download $($entry.File): $($_.Exception.Message)"
        continue
    }

    # A truncated download or an HTML error page saved as .bin fails at runtime with a
    # useless message, so check the magic bytes now rather than three steps later.
    $head = [System.IO.File]::ReadAllBytes($target)[0..3]
    $magic = ($head | ForEach-Object { [char]$_ }) -join ''

    if ($magic -ne 'lmgg') {
        Remove-Item $target -Force
        Write-Error "$($entry.File) is not a GGML file (magic '$magic'). Deleted it."
        continue
    }

    $sizeMB = [math]::Round((Get-Item $target).Length / 1MB, 1)
    Write-Host "ok       $($entry.File) verified ($sizeMB MB)"
}

Write-Host ""
Write-Host "Models are in $dest"
Write-Host "These are ignored by git on purpose. Run this again after a fresh clone."
