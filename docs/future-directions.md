# Future directions

This is the list of features that were designed for this game and deliberately not built. It
is written for anyone deciding what to contribute: each entry says what the idea is, how it
would work, why it was left out, and roughly what finishing it would cost. Nothing here is a
wish; everything has an approach.

The rule that put things on this page: **anything expensive goes here.** A shipped small
game with an honest list of what came next beats an ambitious dead repository.

---

## 1. Non-verbal input

**The idea.** During remapping, the player records any sound at all: a grunt, a hum, a
whistle, a click, a hand clap near the microphone. Each sound maps to a command. From then
on the game is playable without a single word.

**Why it matters more than anything else on this page.** Every speech interface, including
this one, assumes the player produces words in a language the model was trained on. That
excludes non-speaking people, people with dysarthria or other speech differences, people
whose language has no model, and people who simply do not want to talk. A voice interface
that does not require speech is a different proposition to a voice interface that does.

**Why speech recognition is the wrong tool, specifically.** Whisper is built to find words,
and a grunt has none. This project's own logs show it: one recorded utterance came back
transcribed as `[ Grunts ]`, and the code now discards that as non-speech before it reaches
the matcher. Feeding a hum into an ASR model asks it to hallucinate language that is not
there.

**How it would work.** Not machine learning. Template matching on raw audio:

1. In remapping, record two or three reference clips per intent
2. From each clip extract cheap features: duration, mean fundamental frequency by
   autocorrelation, energy envelope shape over a handful of time bins, zero-crossing rate
3. On new input, compute the same features and take the nearest template by weighted
   distance, with a rejection threshold so silence and coughs match nothing

`MicrophoneRecord` already returns `float[]` samples at 16 kHz, which is everything this
needs. There is no model to download and no inference to wait for, so it would be faster
than the speech path, not slower.

**What it would take.** Three to five days. Not the algorithm, which is a few hundred lines.
It is the recording interface, template persistence, per-player calibration, the confidence
tuning, and the failure modes that only appear when a real person tries it in a real room.

**Why it was not built.** It is a second complete input system in a two and a half week
build. The commitment made at the start of this project was that ship date beats scope, and
this is the largest thing that rule has cost.

---

## 2. Local language model for intent matching

**The idea.** Replace the hand-written synonym vocabulary with a small local model that maps
free-form speech to intents.

**What it would buy.** Coverage of phrasings nobody thought to add. "Can you try going the
other side?" is in the logs as a miss, and it is genuinely ambiguous rather than obscure.

**What it would take.** Two to three days plus a model download.

**Why it was not built.** The plain matcher is deterministic and debuggable. Every decision
it makes can be reproduced from the log, which is how five real bugs were found and fixed in
a single session. A model would have absorbed those failures silently and been much harder
to interrogate, and it multiplies the failure surface at exactly the point where failure is
most visible.

---

## 3. Evaluation with more than one voice

**The honest limitation.** Every number in this project comes from one person, one
microphone, one room. That is a smoke test, not an evaluation, and it is labelled that way
everywhere it appears.

**What would make it real.** Ten to fifteen speakers across a range of accents, with
per-speaker accuracy rather than a pooled figure, and a comparison across model sizes so
the accent gap is quantified rather than described.

**What it would take.** This is not really engineering. It is recruitment, consent, and
about a week.

**Why it is the first thing to pick up.** It is the single change that would move this from
a game with an argument to a piece of work with evidence. It sits at the top of the list of
what I would do with more time.

---

## 4. Generative music from the broadcast

**The idea.** The ending replays every sound the player made, in order. A player who mapped
musical sounds during remapping is already producing a sequence. Feed that sequence to a
generative model and produce a score from it.

**What already works.** Half of this is free and already true: the game does not prevent it.
A player who maps five pitched sounds and plays carefully gets a final broadcast that is
music, because it is their own recordings in the order they made them.

**What it would take.** Only the generative half. The ordered sequence of clips already
exists at the ending; what is missing is the model and the step that turns the sequence into
a score. That was always a post-ship idea.

---

## 5. One-switch play

**The idea.** The whole game playable with a single input.

**What it would take.** Scanning selection, dwell timing, and a redesign of every
interaction around one bit of input.

**Why it was not built.** It is an Advanced item in the Game Accessibility Guidelines rather
than a Basic one, and the work is real. Half-building it would have been worse than not
building it, because a one-switch mode that does not actually work is a broken promise to
precisely the people it claims to serve.

---

## 6. Adaptive scaffolding

**The idea.** An open microphone with no guidance is paralysing. Show two or three example
phrasings early, retire them once the player has succeeded twice, keep them permanently
available under Help.

**Status.** That cheap version, the same fixed examples for everyone, is planned for the
build. The adaptive version is future work.

**What it would take.** Tracking which intents a specific player struggles with and choosing
the suggestions from that, instead of showing one fixed set to everybody.
