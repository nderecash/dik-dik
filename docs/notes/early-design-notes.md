# Your notes, sorted by what they cost

Raw notes from 23 July, processed. Nothing here is discarded; some of it is scheduled
rather than built.

---

## Already true, and now proven rather than asserted

**"System adapting to the person, not fixed inputs."** Built. The fuzzy matcher, the
synonym vocabulary, the negation guard. Today it went further than a design principle: the
vocabulary literally learned your phrasings from a recorded session. "Whoa, whoa, whoa" is
in the Stop list because you said it and the rover ignored you.

**"No time pressure, no mistakes, just attempts."** Built. `SimulationReset`. Nothing in
the game is on a timer, including the test harness.

**"Say whatever comes to mind and don't try to help it."** This is already the spike's
rule three, and it should be promoted to the game's only instruction. It is a better
tutorial than a tutorial.

---

## Your best insight, and we have evidence for it

> **"Framing of the situation matters because it can determine what the user is likely to
> say. Here is where easy language is important."**

We proved this by accident today, and it cost us a run.

The spike's first task read *"Tell the rover to start moving."* You said **"Start moving."**
The prompt did not just fail to help, it actively supplied a phrasing the vocabulary did
not contain. My framing produced your utterance, and then my system did not understand the
utterance it had induced.

That is not a bug story, it is a finding. In a voice interface, **the words you put on
screen are part of the input design.** Every prompt is a suggestion, whether you meant it
as one or not. Get the framing wrong and you have built a trap where the interface tells
people what to say and then punishes them for saying it.

This goes in the README as its own section. It is the kind of observation a research group
recognises, and we have the log to back it.

**Consequence for level design:** every piece of on-screen text in this game is input
design. Level intros will be written the way the spike prompts were rewritten, describing
the *situation* rather than the *action*, so the player reaches for their own words.

---

## New, cheap, and the best idea in the notes

> **"In the end you get to hear your own voice repeated to you as the level progressed,
> which is the broadcasted call that will be used to wake the other rovers."**

Build this. It is the ending.

The game keeps every clip you speak. At the final level you stop giving commands and
instead broadcast, and what goes out on the open loop is **your actual recorded voice**,
in sequence, unedited. Not a synthesised summary. Not text. The sounds you made.

Why it works better than anything else we had planned for the ending:

- The thesis lands without being stated. The thing that wakes everyone else is a human
  voice that was never converted into anything.
- It is mechanically trivial. `MicrophoneRecord` already hands us `float[]` samples per
  utterance. We keep them instead of discarding them after transcription and play them
  back. Perhaps a day including the presentation.
- It makes the whole game a recording session the player did not know they were doing.
- It is dual-coded for free: the transcript of each clip is already on screen.

**This changes what I build starting now.** `VoiceCommandProducer` currently throws the
audio away once Whisper has read it. It will keep it instead.

The line `sup_final_06` in the voice script, the one I flagged as the only line permitted
to state the theme, should almost certainly be cut once this exists. The ending will say it.

---

## New, expensive, and genuinely the most original thing here

> **"The system is able to navigate without even using words. During remapping, say
> whatever feels right: moan, grunt, whistle, and it will map that input."**

This is the strongest accessibility idea in the project and I want to be straight with you
about what it costs.

**Whisper cannot do this at all.** Your own log proves it: Whisper transcribed one of your
utterances as `[ Grunts ]` and my code now explicitly discards that as non-speech. Speech
recognition is exactly the wrong tool, because it is built to find words and a grunt has
none.

**It needs a second, separate input path,** and the good news is it is simpler than ASR,
not harder:

1. In remapping, record two or three reference clips per intent, whatever noise you like
2. Extract cheap features from raw samples: duration, average pitch, energy envelope shape
3. Match new audio to the nearest template by distance

No machine learning. No model. A few hundred lines against the raw `float[]` we already
have. But it is still a whole second producer with its own recording UI, its own storage,
its own tuning, and its own failure modes. **Realistically three to five days.**

### The decision I want from you

The honest trade is: **six levels, or five levels plus non-verbal input?**

I would take five and the non-verbal input, and it is not close.

A sixth level is a sixth level. Non-verbal vocal mapping is a genuinely original
contribution to the thing PIRG actually studies, and it opens the game to people with
speech differences, non-verbal people, and anyone whose language the model was never
trained on. It is the difference between a game that argues interfaces should adapt to
people and a game that demonstrates it for people no ASR system serves at all.

If it overruns, it degrades gracefully: it becomes a documented experiment in the README
with the reasoning intact, which is still worth more than a level.

---

## Deferred, with reasons

**"Suggest possible constraints that will be overwritten as the user progresses."**
Right instinct: an open microphone with no guidance is paralysing. Cheap version ships:
show two or three example phrasings on the first level, fade them once the player succeeds
twice, and keep them permanently available under Help. Scaffolding that retreats. Not a
separate system.

**"With careful mapping you can sing a song using the sequence."**
This falls out of the ending for free. If the final broadcast replays your clips in
sequence, a player who mapped musical sounds gets music. We do not build it; we simply do
not prevent it, and it goes in the README as something the design allows.

**"Generative AI to produce a score from your sequence, Mozart or a bird."**
Not in this timeline. A genuinely good post-ship idea and a good Medium post. Noted here so
it is not lost.

---

## What I need from you on this page

One answer: **five levels plus non-verbal input, or six levels?**

Everything else here I can act on without you.
