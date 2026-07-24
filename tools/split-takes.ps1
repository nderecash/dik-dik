<#
.SYNOPSIS
    Splits one long recording into individual clips, cutting at the silences.

.DESCRIPTION
    Recording forty lines as forty separate files means forty start/stop cycles and
    forty filenames typed by hand. Reading straight through with a clear pause between
    lines is far less work, and the pauses are exactly what a splitter needs.

    Cuts at silence, names the pieces from a list, and reports the count so a wrong
    split is obvious immediately rather than three steps later.

.PARAMETER Take
    The long recording.

.PARAMETER OutputFolder
    Where the clips go.

.PARAMETER NamesFile
    Text file, one clip name per line, in the order they were read. Optional; without
    it clips are numbered.

.PARAMETER SilenceDb
    Anything quieter than this counts as silence. Raise toward 0 in a noisy room
    (-30), lower it in a quiet one (-40).

.PARAMETER SilenceSeconds
    How long a pause has to be before it counts as a break between lines. Must be
    shorter than your actual pauses and longer than any pause inside a line.

.EXAMPLE
    .\tools\split-takes.ps1 -Take .\audio\take1.wav -OutputFolder .\audio\raw -NamesFile .\audio\names.txt
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Take,
    [Parameter(Mandatory = $true)][string]$OutputFolder,
    [Parameter()][string]$NamesFile,
    [Parameter()][double]$SilenceDb = -35,
    [Parameter()][double]$SilenceSeconds = 1.2,
    [Parameter()][double]$MinimumClipSeconds = 0.4
)

$ErrorActionPreference = 'Stop'

$ffmpeg = (Get-Command ffmpeg -ErrorAction SilentlyContinue).Source
if (-not $ffmpeg) { throw "ffmpeg not on PATH. Open a NEW terminal after installing it." }
$ffprobe = (Get-Command ffprobe -ErrorAction SilentlyContinue).Source

# Run ffmpeg through Start-Process, capturing stderr to a file.
#
# ffmpeg writes everything, including the silencedetect report, to stderr. PowerShell 5.1
# wraps a native command's stderr as terminating errors, so calling ffmpeg directly with
# 2>&1 or 2>file both blow up the moment it prints even a harmless "Guessed Channel Layout"
# line. Start-Process hands stderr straight to a file without PowerShell touching it, which
# is the only reliable way to run a chatty native tool from a 5.1 script.
function Invoke-FFmpeg {
    param([string[]]$FFArgs)

    $errFile = [System.IO.Path]::GetTempFileName()
    $outFile = [System.IO.Path]::GetTempFileName()
    try {
        $proc = Start-Process -FilePath $ffmpeg -ArgumentList $FFArgs -NoNewWindow -Wait -PassThru `
            -RedirectStandardError $errFile -RedirectStandardOutput $outFile
        return [pscustomobject]@{
            ExitCode = $proc.ExitCode
            StdErr   = (Get-Content $errFile -ErrorAction SilentlyContinue)
        }
    }
    finally {
        Remove-Item $errFile, $outFile -Force -ErrorAction SilentlyContinue
    }
}

if (-not (Test-Path $Take)) { throw "No such file: $Take" }
New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null

$duration = [double](& $ffprobe -v error -show_entries format=duration -of default=nw=1:nk=1 $Take)
Write-Host ("take is {0:0.0} seconds" -f $duration)

# ---------------------------------------------------------------------------
# Find the silences
# ---------------------------------------------------------------------------
$detect = (Invoke-FFmpeg @(
    '-hide_banner', '-v', 'info', '-i', $Take,
    '-af', "silencedetect=noise=${SilenceDb}dB:d=$SilenceSeconds",
    '-f', 'null', 'NUL'
)).StdErr

$starts = @()
$ends = @()

foreach ($line in $detect) {
    if ($line -match 'silence_start:\s*([-\d.]+)') { $starts += [double]$Matches[1] }
    if ($line -match 'silence_end:\s*([-\d.]+)')   { $ends   += [double]$Matches[1] }
}

Write-Host "found $($starts.Count) silences"

# ---------------------------------------------------------------------------
# Speech is whatever is left over
# ---------------------------------------------------------------------------
$segments = @()
$cursor = 0.0

for ($i = 0; $i -lt $starts.Count; $i++) {
    $segmentEnd = $starts[$i]
    if (($segmentEnd - $cursor) -ge $MinimumClipSeconds) {
        $segments += [pscustomobject]@{ Start = $cursor; End = $segmentEnd }
    }
    if ($i -lt $ends.Count) { $cursor = $ends[$i] }
}

if (($duration - $cursor) -ge $MinimumClipSeconds) {
    $segments += [pscustomobject]@{ Start = $cursor; End = $duration }
}

Write-Host "that gives $($segments.Count) clips"
Write-Host ""

# ---------------------------------------------------------------------------
# Names
# ---------------------------------------------------------------------------
$names = @()
if ($NamesFile -and (Test-Path $NamesFile)) {
    $names = Get-Content $NamesFile | Where-Object { $_.Trim() -ne '' -and -not $_.StartsWith('#') }
    Write-Host "names file has $($names.Count) entries"

    if ($names.Count -ne $segments.Count) {
        Write-Warning "MISMATCH: $($segments.Count) clips but $($names.Count) names."
        Write-Warning "Extra clips usually mean a pause inside a line. Try -SilenceSeconds 1.6"
        Write-Warning "Too few usually means pauses were too short. Try -SilenceSeconds 0.8"
        Write-Warning "Writing numbered files so you can look at what happened."
        $names = @()
    }
}

# ---------------------------------------------------------------------------
# Cut
# ---------------------------------------------------------------------------
for ($i = 0; $i -lt $segments.Count; $i++) {
    $segment = $segments[$i]

    # A little air either side so nothing is clipped off the start of a word.
    $from = [math]::Max(0, $segment.Start - 0.15)
    $length = ($segment.End - $segment.Start) + 0.30

    $name = if ($names.Count -gt $i) { $names[$i].Trim() } else { "clip_{0:D3}" -f ($i + 1) }
    $out = Join-Path $OutputFolder "$name.wav"

    $cut = Invoke-FFmpeg @(
        '-y', '-v', 'error', '-ss', "$from", '-t', "$length",
        '-i', $Take, '-ac', '1', '-ar', '44100', $out
    )

    if ($cut.ExitCode -ne 0) {
        Write-Warning "failed to cut $name.wav: $($cut.StdErr -join ' ')"
        continue
    }

    Write-Host ("{0,-22} {1,6:0.0}s" -f "$name.wav", ($segment.End - $segment.Start))
}

Write-Host ""
Write-Host "$($segments.Count) clips in $OutputFolder"
Write-Host "Listen to two or three before processing the lot."
Write-Host "Then: .\tools\radio-filter.ps1 -InputFolder $OutputFolder -OutputFolder .\audio\processed"
