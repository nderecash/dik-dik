<#
.SYNOPSIS
    Turns clean voice recordings into mission control transmissions.

.DESCRIPTION
    Band-limits to roughly 300 Hz to 3 kHz, compresses hard, and tops and tails each
    clip with authentic Quindar tones.

    The repeated lowpass stages are deliberate. ffmpeg's lowpass is 2-pole, about
    12 dB per octave, and 3 kHz to 4 kHz is barely half an octave, so one stage does
    almost nothing. Measured: a single stage gave 4.9 dB of attenuation above 4 kHz.
    Three stages gave 10.9 dB above 4 kHz and 17.2 dB above 6 kHz. Do not "simplify"
    this back to one lowpass; it will stop sounding like a radio.

    Quindar tones are the real ones: 2,525 Hz on key-down, 2,475 Hz on key-up, 250 ms.
    They keyed remote transmitters over a single audio line.

.EXAMPLE
    .\tools\radio-filter.ps1 -InputFolder .\audio\raw -OutputFolder .\audio\processed

.EXAMPLE
    .\tools\radio-filter.ps1 -InputFolder .\raw -OutputFolder .\out -NoQuindar
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InputFolder,
    [Parameter(Mandatory = $true)][string]$OutputFolder,

    [Parameter()][int]$HighPassHz = 300,
    [Parameter()][int]$LowPassHz = 3000,
    [Parameter()][int]$LowPassStages = 3,
    [Parameter()][int]$CompressRatio = 8,
    [Parameter()][int]$MakeupDb = 6,

    [switch]$NoQuindar
)

$ErrorActionPreference = 'Stop'

$ffmpeg = (Get-Command ffmpeg -ErrorAction SilentlyContinue).Source
if (-not $ffmpeg) {
    throw "ffmpeg not found on PATH. It installs to the user PATH, so open a NEW terminal after installing it."
}

if (-not (Test-Path $InputFolder)) { throw "No such folder: $InputFolder" }
New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null

$work = Join-Path $OutputFolder '_tones'
New-Item -ItemType Directory -Path $work -Force | Out-Null

# ---------------------------------------------------------------------------
# Quindar tones. The short fades stop the tone clicking at its edges.
# ---------------------------------------------------------------------------
$quindarIn = Join-Path $work 'quindar_in.wav'
$quindarOut = Join-Path $work 'quindar_out.wav'

if (-not $NoQuindar) {
    $fade = 'volume=0.25,afade=t=in:d=0.01,afade=t=out:st=0.24:d=0.01'

    & $ffmpeg -y -v error -f lavfi -i "sine=frequency=2525:duration=0.25:sample_rate=44100" -ac 1 -af $fade $quindarIn
    & $ffmpeg -y -v error -f lavfi -i "sine=frequency=2475:duration=0.25:sample_rate=44100" -ac 1 -af $fade $quindarOut
    Write-Host "tones    generated (2525 Hz in, 2475 Hz out)"
}

# ---------------------------------------------------------------------------
# The radio chain
# ---------------------------------------------------------------------------
$stages = @()
$stages += "highpass=f=${HighPassHz}:poles=2"
$stages += "highpass=f=${HighPassHz}:poles=2"

for ($i = 0; $i -lt $LowPassStages; $i++) {
    $stages += "lowpass=f=${LowPassHz}:poles=2"
}

$stages += "acompressor=threshold=-20dB:ratio=${CompressRatio}:attack=5:release=60:makeup=${MakeupDb}"
$stages += "alimiter=limit=0.95"

$chain = $stages -join ','
Write-Host "chain    $chain"
Write-Host ""

$files = Get-ChildItem $InputFolder -File | Where-Object { $_.Extension -in '.wav', '.flac', '.mp3', '.m4a' }
if ($files.Count -eq 0) {
    Write-Warning "No audio files in $InputFolder"
    return
}

foreach ($file in $files) {
    $filtered = Join-Path $work "$($file.BaseName)_filtered.wav"
    $final = Join-Path $OutputFolder "$($file.BaseName).wav"

    try {
        & $ffmpeg -y -v error -i $file.FullName -ac 1 -ar 44100 -af $chain $filtered

        if ($NoQuindar) {
            Move-Item $filtered $final -Force
        }
        else {
            & $ffmpeg -y -v error -i $quindarIn -i $filtered -i $quindarOut `
                -filter_complex "[0:a][1:a][2:a]concat=n=3:v=0:a=1" $final
        }

        $seconds = [math]::Round((Get-Item $final).Length / (44100 * 2), 2)
        Write-Host ("ok       {0,-32} {1}s" -f $file.Name, $seconds)
    }
    catch {
        Write-Warning "failed   $($file.Name): $($_.Exception.Message)"
    }
}

Write-Host ""
Write-Host "Done. Listen to one before processing the rest."
Write-Host "If it sounds muffled rather than radio-like, try -LowPassHz 3400 or -LowPassStages 2."
