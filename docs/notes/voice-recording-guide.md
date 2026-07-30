# Reference: why the audio processing is shaped this way

> **You do not need this page to record.** Follow
> [TASK-voice-recording.md](TASK-voice-recording.md) instead; it walks through the whole
> session step by step.
>
> This page is here for when something sounds wrong and you want to know what the dials
> actually do, or when someone asks why the filter chain looks so repetitive.

Everything here was run and measured before it was written down. What I could not do is
listen to it. The numbers below are real; the judgement of whether it *sounds* right is
yours and only yours.

## First, a suggestion that halves the work

You do not need to voice the rover.

The rover is a small machine. Let it answer in **text on screen plus tones and light**,
never in words. That is cheaper, it is more in character, and it satisfies two guidelines
outright: subtitles for all speech, and no essential information by sound alone.

That leaves one human voice to record: **the supervisor on the loop with you**. One
speaker, one tone, one session. It also makes a nice point without stating it, that the
only human voice in a game about being heard is a human one.

---

## Recording

**Use the microphone you already have.** Radio processing band-limits everything to
roughly 300 Hz to 3 kHz, which is exactly where a cheap microphone sounds fine. Expensive
microphones spend their money on the frequencies we are about to throw away.

**The room matters more than the microphone.** Hard parallel walls give you flutter echo
that survives filtering. A room with soft furnishings, curtains, a bed, clothes, is better
than a bare office. A wardrobe full of hanging clothes is genuinely a good vocal booth.

**Technique:**
- About a hand's width from the mic, slightly off to one side so plosives miss it
- Speak across the mic, not into it
- Keep the level well below clipping. Quiet and clean beats loud and distorted; we add
  loudness back in processing and cannot remove distortion
- **Leave two seconds of silence at the start** of every take. That gives us a clean noise
  profile to subtract later if the room turns out to be noisier than you thought

**Delivery:** flight controllers are calm, brief and dry. Not dramatic. The humour in the
Apollo transcripts works because nobody is performing it. Underplay everything.

**Record one line per file** and name them to match the script:
`sup_boot_01.wav`, `sup_reset_03.wav`. Do not record everything into one long take and
plan to cut it up. You will not enjoy that.

Record at 44.1 kHz or 48 kHz, mono, WAV. Not MP3.

---

## Processing

ffmpeg 8.1.2 is installed and already on your user PATH, so **open a new terminal** and
`ffmpeg -version` should work. This shell was open before the install, which is the only
reason I have been calling it by full path.

### The chain, and why it is shaped this way

```bash
ffmpeg -i raw.wav -af "highpass=f=300:poles=2,highpass=f=300:poles=2,lowpass=f=3000:poles=2,lowpass=f=3000:poles=2,lowpass=f=3000:poles=2,acompressor=threshold=-20dB:ratio=8:attack=5:release=60:makeup=6,alimiter=limit=0.95" filtered.wav
```

The repetition is not a mistake. ffmpeg's `lowpass` is 2-pole, about 12 dB per octave, and
3 kHz to 4 kHz is barely half an octave, so a single stage does almost nothing. I measured
it: one lowpass gave **4.9 dB** of attenuation above 4 kHz, which sounds like a blanket
over a speaker rather than a radio. Cascading three gives **10.9 dB above 4 kHz and
17.2 dB above 6 kHz**, and the fact that it keeps rising with frequency is what tells you
the rolloff is real.

The compressor is doing the other half of the work. Radio audio is heavily compressed
because it has to stay intelligible through noise, so everything sits at the same loudness.
That flatness is a large part of why radio sounds like radio.

### Quindar tones

The real ones: **2,525 Hz on key-down, 2,475 Hz on key-up, 250 ms each.** They were not
decoration. They remotely keyed transmitters at tracking stations over a single audio line.

```bash
ffmpeg -f lavfi -i "sine=frequency=2525:duration=0.25:sample_rate=44100" -ac 1 -af "volume=0.25,afade=t=in:d=0.01,afade=t=out:st=0.24:d=0.01" quindar_in.wav
ffmpeg -f lavfi -i "sine=frequency=2475:duration=0.25:sample_rate=44100" -ac 1 -af "volume=0.25,afade=t=in:d=0.01,afade=t=out:st=0.24:d=0.01" quindar_out.wav
```

The tiny fades stop the tone clicking at its edges.

### Assemble a transmission

```bash
ffmpeg -i quindar_in.wav -i filtered.wav -i quindar_out.wav -filter_complex "[0:a][1:a][2:a]concat=n=3:v=0:a=1" transmission.wav
```

Verified: 0.25 + 3.00 + 0.25 came out at exactly 3.50 seconds.

**Pair every Quindar tone with a light pulse on the rover.** Then the sound design itself
satisfies "no essential information conveyed by sounds alone", instead of it being
something to remember per level.

---

## Doing it in bulk

`tools/radio-filter.ps1` runs the whole chain over a folder. Drop your raw takes in, get
finished transmissions out.

```bash
.\tools\radio-filter.ps1 -InputFolder .\audio\raw -OutputFolder .\audio\processed
```

---

## Tuning by ear

I cannot hear any of this. If it is wrong, these are the dials:

| Sounds like | Change |
|---|---|
| Too muffled, hard to understand | Raise `lowpass` to 3400, or drop to two stages |
| Too clean, not radio enough | Add a fourth `lowpass` stage, or raise `highpass` to 400 |
| Squashed and lifeless | Lower `acompressor` ratio from 8 to 4 |
| Too quiet after processing | Raise `makeup` from 6 to 8 |
| Thin and tinny | Lower `highpass` to 250 |

Change one thing at a time.

---

## One honest note for the README

This processing is a costume. The radio effect is applied to a clean recording made in a
room, and the "signal delay" is really speech recognition taking its time. Both are fair
game fictions, and both should be stated plainly rather than left for someone to work out.
