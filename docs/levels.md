# Dik-dik: the six levels

Decided 23 July. This is the build spec. Each level names the guideline it embodies, quoted
exactly from the [Game Accessibility Guidelines, Basic level](https://gameaccessibilityguidelines.com/basic/).

Two minutes each. Roughly twelve to fifteen minutes, plus the broadcast.

## Visual style: hard silhouette

Black shapes against a pale sky. Kenney geometry, unlit or near-unlit, minimal texture.

Chosen for three reasons and one of them was unplanned. It is cheap, it photographs well
for the itch.io page, and Level 3 turns it into an argument: the style depends on a bright
sky behind everything, the night side takes that away, and the high-contrast setting is
what gives it back. An art style with a load-bearing assumption is more useful here than a
neutral one.

---

**Rules that apply to every level:**

- Every accessibility setting is in the menu from first launch. A level shows you why an
  option exists. It never makes you earn one.
- No failure. A bad run is a rehearsal discarded. `SimulationReset`, no counter, no screen.
- Every sound has a visual twin. If you add an audio cue with no counterpart, it is not
  finished.
- All on-screen text is input design. Describe the situation, not the action, so the player
  reaches for their own words.

---

## 1. Dust corridor

> **"Ensure no essential information is conveyed by sounds alone"**

**What happens.** Salty is in a collapsed service tunnel under the installation. Dark, and
its lamp reaches about three metres. At each junction it pings: a tone *and* a pulse of
light across its shell, together, always. You learn the whole loop here with no tutorial
text: speak, wait two and a half seconds, watch.

**The barrier.** There isn't one, and that is deliberate. This level exists to establish
that muting the game costs you nothing. A deaf player and a hearing player have identical
information at every moment. Everything after this assumes the loop is understood.

**Intents.** Go, Stop, Left, Right.

**Build notes.** Three or four junction choices, linear otherwise. No dead ends: a wrong
turn loops gently back rather than stopping you. Vocabulary stays at four commands so the
first thing a player learns is small enough to hold.

---

## 2. Two supervisors

> **"Use simple clear language"**

**What happens.** Two voices on the loop. The station's automated system reads procedures
in dense engineering jargon. Your human supervisor translates them. You act on the plain
version, every time, because the jargon version is unusable.

```
STATION:  "Actuate the primary egress mechanism
           via the manual override subsystem."
CONTROL:  "Open the door."
```

Three of these, the jargon getting worse each time, until the supervisor stops hiding what
they think of whoever wrote it.

**The barrier.** The jargon. There is also a plain-language toggle in settings which
rewrites on-screen text the same way.

**Intents.** Go, Stop, Left, Right, Open.

**Build notes.** The plain translation **always arrives.** The player is never required to
decode jargon and never penalised for failing to. The level demonstrates the guideline, it
does not test it.

The automated voice is synthetic (Piper, local, offline). The human one is Moses. That
split does the arguing for us without a line of dialogue about it: the machine speaks in
jargon, the person speaks plainly.

---

## 3. Night side

> **"Provide high contrast between text/UI and background"**
> **"Use an easily readable default font size"**

**What happens.** Salty crosses onto the night side. There is no sky glow here, and the
installation's lights are dead.

This matters because of how the rest of the game looks. Every other level is hard
silhouette: black shapes read cleanly because there is a bright sky behind them. That is
the whole visual language, and it is doing its work for free. On the night side there is
nothing behind anything. Shapes stop being shapes. Drop-offs, doorways and hazards are all
still there and none of them separate from the ground.

High-contrast mode restores edge definition with rim lighting, artificially, because the
sky is no longer doing it.

**The barrier.** An art style whose central assumption has just failed. The level is
completable without the setting, slower and with more rehearsal runs, but it is genuinely
unpleasant, which is the honest version of the experience.

**Why this is the strongest level of the six for the argument.** The look of this game
assumes a light source behind everything. That assumption is invisible until it is absent,
and when it is absent the design excludes you until a setting puts it back. That is the
entire thesis, expressed in light rather than in speech, and nobody has to say it.

**Intents.** Go, Stop, Left, Right, Light.

**Build notes.** High contrast must change **hazard rendering**, not only UI, or it looks
cosmetic. An outline or rim-light shader on hazard geometry is the cheapest honest version.
This is also where Light finally earns its place in the vocabulary: Salty's lamp is the
only thing you have before you find the setting.

---

## 4. The jammed key

> **"Allow controls to be remapped / reconfigured"**

**What happens.** A fault on your console. One control reports stuck. The remap screen is
one key away and always has been.

**The barrier.** An unusable default binding.

**Tone, and this matters more than the mechanic.** The fault is **the console's**, never
yours. "Fault on your side, not Salty's. Remap it, takes a second." Nothing in this level
may imply the player did something wrong. An accessibility feature introduced by breaking
something on purpose is hostile if the writing gets it even slightly off.

### The thing that fell out of this, which is better than the level

A voice-only player has no key to remap. If Level 4 is only about rebinding, it has nothing
for half the audience, and the parity claim quietly collapses at exactly the level that
claims to be about reconfiguring your controls.

So **remapping means both things:**

- **Keyboard:** rebind the key. Already built.
- **Voice:** teach Salty a new word. If the rover keeps missing your phrasing, you add it,
  and from then on it knows.

That is the same guideline expressed in each modality, and it costs about half a day: a
text field and a runtime addition to `IntentVocabulary`. It also turns the thing the
project has been doing *to* the player into something the player does themselves. Every
phrasing in that vocabulary got there because someone said it and was not understood. This
hands that loop to the player.

There is a nice inversion here too: on this level the **keyboard** is what fails, and voice
keeps working. Voice is usually cast as the unreliable modality. Here it is the one still
standing.

**Intents.** Go, Stop, Left, Right, Back, Open.

---

## 5. The slope

> **"Include an option to adjust the game speed"**

**What happens.** Salty on a downhill grade with marks on the ground to stop at. It keeps
rolling after you say stop, because momentum plus a two and a half second signal delay
means your word arrives late. You overshoot. The game speed slider shrinks the delay
relative to everything else.

```
YOU:    "stop"
        ... 2.6s in transit ...
SALTY:  [still rolling]
        [stops, three metres past the mark]

at 0.5x game speed the same 2.6s costs half the distance
```

**The barrier.** Latency and momentum together. This is the only level where the speed
setting and the game's central mechanic explain each other, which is why it is worth a
level of its own.

**Intents.** Go, Stop, Left, Right.

**Build notes.** Overshooting costs a rehearsal run and nothing else. With no failure
state, speed can only ever produce discomfort, never loss, and the level must be designed
so that stays true.

---

## 6. The crater rim

> **"Provide subtitles for all important speech"**
> **"If any subtitles / captions are used, present them in a clear, easy to read way"**

**What happens.** Salty climbs to the rim for line of sight. At the top, the reveal: the
dormant rovers scattered on the plain below, all of them. You switch off the directed
channel and onto the open loop.

Then the game plays your own voice back. Every clip you recorded across the whole game, in
the order you said it, unedited, with its transcript running underneath. The rovers wake as
it plays.

```
SALTY:    [reaches the rim]
CONTROL:  "There. All of them."
CONTROL:  "Open loop. Say it once."

          [your voice, every clip, in order]

          [they start waking]
```

**Intents.** Go, Stop, Left, Right, Wake.

**Build notes.** `VoiceJournal.BuildBroadcast()` and `BroadcastTranscript()` already exist.
Rovers wake in sequence with the clips, so the count of what wakes is the count of what you
said.

Cut `sup_final_06`, the one line permitted to state the theme. The ending says it, and
saying it twice would be saying it worse.

**The hesitations stay in.** The false starts, the sentence where you told Salty it was
going the wrong way, the "whoa, whoa, whoa". A cleaned broadcast would be a different voice
and a smaller idea.

---

## What this needs from the build, in order

1. Level flow, load, reset, completion
2. Level 1 as the vertical slice: if the corridor feels right, the rest is content
3. `Light` wired to hazard visibility for Level 3
4. Voice remapping, the half day from Level 4
5. Slope physics and stop marks for Level 5
6. Broadcast playback and the wake sequence for Level 6
