# Generate the game's non-voice audio with ffmpeg.
#
# Every clip here is synthesised, not sampled, so the whole soundtrack is
# reproducible from this file and nothing in it needs a licence.
#
# The rule this project learned the hard way: verify audio numerically, never by
# ear. Two shipped audio bugs this project has had were both caught by checking
# the sample rate and the peak level, and both would have survived a listen.
#
#   - The station voice was reinterpreted at the wrong rate, playing 1.86x fast.
#   - The ambient hum peaked at -40 dBFS and was then multiplied by 0.35.
#
# So: 44100 Hz everywhere, and a printed peak for every file at the end.

$ErrorActionPreference = 'Stop'
$out = 'Assets/Audio'
New-Item -ItemType Directory -Force -Path $out | Out-Null

function Make($name, $filterArgs, $seconds) {
    $path = "$out/$name"
    Write-Host "  $name"
    $procArgs = @('-y', '-f', 'lavfi', '-i', $filterArgs, '-t', $seconds,
                  '-ar', '44100', '-ac', '1', '-c:a', 'pcm_s16le', $path)
    $p = Start-Process -FilePath ffmpeg -ArgumentList $procArgs -NoNewWindow -Wait -PassThru `
         -RedirectStandardError "$env:TEMP\ffmpeg-err.txt"
    if ($p.ExitCode -ne 0) {
        Get-Content "$env:TEMP\ffmpeg-err.txt" | Select-Object -Last 12
        throw "ffmpeg failed on $name"
    }
}

Write-Host "Generating..."

# Tire roll. A loop, so it must be seamless: any click at the wrap is audible on
# every revolution. Low filtered noise plus a slow grind, no transients.
Make 'tire_roll.wav' `
  "anoisesrc=color=brown:amplitude=0.5:r=44100,highpass=f=60,lowpass=f=900,volume=0.5" 3

# Wind. Much wider band than the tires and far quieter, so the two never compete
# for the same part of the spectrum. This sits under everything all the time.
Make 'wind_loop.wav' `
  "anoisesrc=color=pink:amplitude=0.35:r=44100,highpass=f=180,lowpass=f=2600,tremolo=f=0.11:d=0.5,volume=0.32" 8

# ---------------------------------------------------------------------------
# Tones.
#
# ffmpeg's `sine` source does NOT peak at full scale. Measured here: a plain
# `sine=frequency=660` writes a file peaking at -18.1 dBFS. So a `volume=0.35`
# on top of it lands at -27.2 dB, not the -9 dB the number suggests, and the cue
# is inaudible under the radio-filtered voice.
#
# The gains below are therefore above 1.0 on purpose. This is exactly the class
# of mistake that shipped twice on this project already, and the only reason it
# was caught this time is the verification block at the bottom.
# ---------------------------------------------------------------------------

# Scan sweep. Rises while the bar travels, so the sound and the picture are the
# same gesture rather than two cues that happen to coincide.
Make 'scan_sweep.wav' `
  "sine=frequency=420:duration=1.4,volume=2.0,aeval='val(0)*(1+0.9*t)':c=same" 1.4

# Scan done, clean. Short and bright.
Make 'scan_clear.wav' `
  "sine=frequency=660:duration=0.14,volume=2.0" 0.14

# Scan done, fault. Lower and longer, deliberately unlike the clean one, because
# this plays exactly once in the whole game and has to land.
Make 'scan_fault.wav' `
  "sine=frequency=190:duration=0.55,volume=2.3" 0.55

# Brake. Short, dry, no pitch, so it reads as mechanism and not as a warning.
Make 'brake_tick.wav' `
  "anoisesrc=color=white:amplitude=0.4:r=44100,highpass=f=1200,volume=0.22" 0.12

Write-Host ""
Write-Host "Verifying (rate must be 44100, peak must be well above -30 dB):"

foreach ($f in Get-ChildItem "$out/*.wav") {
    $rate = & ffprobe -v error -select_streams a:0 -show_entries stream=sample_rate `
                      -of default=nw=1:nk=1 $f.FullName
    $err = "$env:TEMP\vol-$($f.BaseName).txt"
    $p = Start-Process -FilePath ffmpeg `
         -ArgumentList @('-i', $f.FullName, '-af', 'volumedetect', '-f', 'null', 'NUL') `
         -NoNewWindow -Wait -PassThru -RedirectStandardError $err
    $peak = (Select-String -Path $err -Pattern 'max_volume: (.*)').Matches.Groups[1].Value
    "{0,-18} {1} Hz   peak {2}" -f $f.Name, $rate, $peak
}
