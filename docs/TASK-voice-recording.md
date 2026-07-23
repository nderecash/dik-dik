# TASK: record the mission control voice

**Time:** about 45 minutes total, and only 20 of that is talking
**You need:** a quiet-ish room, your normal microphone, a new terminal
**Result:** 41 processed radio transmissions in `audio/processed/`

Read from **[voice-script.md](voice-script.md)**. That file is only the lines. This file is
everything you do.

You read the whole thing straight through in one take. You do **not** stop and start for
each line. A script cuts it up afterwards.

Six stages. Each ends with a **CHECK** you must pass before continuing.

---

# STAGE 1 — Set up

**Time: 10 minutes**

- [ ] **Close everything using the microphone.** Teams, Zoom, Discord, any browser tab on a
      call. Windows gives the mic to whoever grabbed it first.
- [ ] **Pick the softest room you have.** Soft furnishings beat a bare office. A wardrobe
      full of hanging clothes is genuinely one of the best vocal booths in a house.
- [ ] **Use your normal microphone.** Not the best one you own. Radio processing throws away
      everything above 3 kHz, which is exactly where expensive microphones spend their money.
- [ ] Sit about a hand's width from the mic, slightly off to one side so plosives miss it.
- [ ] Open a **new terminal** and check ffmpeg is reachable:

```bash
ffmpeg -version
```

### CHECK

- [ ] `ffmpeg -version` printed a version number.

If it said "not recognized", you are in a terminal that was open before ffmpeg was
installed. Close it and open a new one.

---

# STAGE 2 — Test recording

**Time: 5 minutes. Do not skip this.**

This catches a dead microphone before you have read forty lines into it.

- [ ] Make a folder for the session:

```bash
mkdir -p audio
```

- [ ] Record ten seconds of yourself talking normally:

```bash
ffmpeg -f dshow -i audio="Microphone (Realtek High Definition Audio)" -t 10 -ac 1 -ar 48000 audio/test.wav
```

- [ ] Play it back and listen.

### CHECK

- [ ] You can hear yourself clearly.
- [ ] It is **not** distorted or crackly. Quiet and clean beats loud and clipped — we add
      loudness back later and cannot remove distortion.
- [ ] Background noise is low. Fridge, fan, traffic.

**If it is distorted:** move further from the mic, or turn the input level down in
Settings → System → Sound → Input.

**If there is no sound at all:** the device name may differ. List them:

```bash
ffmpeg -list_devices true -f dshow -i dummy
```

Use whatever name it prints, in quotes, in place of the one above.

---

# STAGE 3 — Record the take

**Time: 20 minutes**

- [ ] Open [voice-script.md](voice-script.md) where you can read it.
- [ ] Start recording:

```bash
ffmpeg -f dshow -i audio="Microphone (Realtek High Definition Audio)" -ac 1 -ar 48000 audio/take1.wav
```

- [ ] **Wait two seconds before your first line.** That gives us clean room tone.
- [ ] Read every line in order, top to bottom.

### The four rules while reading

1. **Pause about two seconds between lines.** This is how the splitter finds the breaks. Do
   not rush from one line into the next.
2. **Do not pause in the middle of a line.** A gap inside a line splits it in two.
3. **Underplay everything.** A flight controller has done this a thousand times. If a line
   sounds like a joke when you read it, say it flatter.
4. **Fluffed a line?** Pause, then say it again. Keep the mistake in. You will hear both
   versions and can delete the bad one. Do not stop recording.

- [ ] When you reach the end, wait two seconds, then press **Ctrl+C** to stop.

### CHECK

- [ ] `audio/take1.wav` exists and is a sensible size — a few tens of megabytes.

---

# STAGE 4 — Split it up

**Time: 2 minutes**

- [ ] Save the clip names. I have prepared them:

```bash
cp docs/clip-names.txt audio/names.txt
```

- [ ] Split:

```bash
.\tools\split-takes.ps1 -Take .\audio\take1.wav -OutputFolder .\audio\raw -NamesFile .\audio\names.txt
```

### CHECK

The script prints how many clips it found and how many names it expected.

- [ ] **Do those two numbers match?**

**They match** → Stage 5.

**Too many clips** → you paused inside a line somewhere. Run it again with longer pauses
required:

```bash
.\tools\split-takes.ps1 -Take .\audio\take1.wav -OutputFolder .\audio\raw -NamesFile .\audio\names.txt -SilenceSeconds 1.6
```

**Too few clips** → your pauses were too short, or the room is noisy enough that the gaps
were not silent:

```bash
.\tools\split-takes.ps1 -Take .\audio\take1.wav -OutputFolder .\audio\raw -NamesFile .\audio\names.txt -SilenceSeconds 0.8 -SilenceDb -40
```

**Still wrong after two tries** → send me the numbers it printed and I will tune it. Do not
re-record.

---

# STAGE 5 — Make it sound like radio

**Time: 3 minutes**

```bash
.\tools\radio-filter.ps1 -InputFolder .\audio\raw -OutputFolder .\audio\processed
```

This band-limits to roughly 300 Hz to 3 kHz, compresses hard, and tops and tails each clip
with real Quindar tones — 2,525 Hz on key-down and 2,475 Hz on key-up, 250 ms each, the
actual frequencies Apollo used to key transmitters remotely.

### CHECK

- [ ] Play `audio/processed/sup_boot_01.wav`.
- [ ] Does it sound like a radio transmission, or like someone talking through a blanket?

**I cannot hear any of this.** You are the only one who can judge it. If it is wrong, these
are the dials:

| It sounds | Run this |
|---|---|
| Muffled, hard to make out | `-LowPassHz 3400` |
| Too clean, not radio enough | `-LowPassStages 4` |
| Squashed and lifeless | `-CompressRatio 4` |
| Too quiet | `-MakeupDb 8` |
| Thin and tinny | `-HighPassHz 250` |

Change one thing at a time. Example:

```bash
.\tools\radio-filter.ps1 -InputFolder .\audio\raw -OutputFolder .\audio\processed -LowPassHz 3400
```

---

# STAGE 6 — Hand it over

**Time: 1 minute**

- [ ] Tell me it is done and roughly how it went.
- [ ] Tell me if any clip is bad and should be re-read. Do not re-record the whole thing for
      two bad lines.

I take it from there: into the project, wired to the right moments, checked against the
subtitle rules.

---

# What you are not doing

- **Not voicing Salty.** The rover never speaks in words. It answers in text, tones and
  light. Cheaper, more in character, and it satisfies two guidelines by construction.
- **Not voicing the station system.** The jargon voice in Level 2 is synthetic on purpose:
  the machine talks in jargon, the person talks plainly. That argument only works if the
  two voices are actually different in kind.

One human voice in the whole game. Yours.
