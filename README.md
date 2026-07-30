# Dik-dik

A small game about a rover that cannot move until someone speaks to it.

You are the voice on its radio. Speech recognition runs on your own machine, offline, with
no account and no cloud. Every command also works on the keyboard.

> **Status: playable, six sectors, being tuned.** Windows build works end to end. Browser
> build is keyboard only, for reasons in [Running it](#running-it).

This document is the spine. Where something has a longer version, it is linked.

- [The number, and what it actually showed](#the-number-and-what-it-actually-showed)
- [Why I made this](#why-i-made-this)
- [The game](#the-game)
- [Latency, which is the real problem](#latency-which-is-the-real-problem)
- [The architecture is the argument](#the-architecture-is-the-argument)
- [Understanding people rather than keywords](#understanding-people-rather-than-keywords)
- [Accessibility, specifically](#accessibility-specifically)
- [The feature I built, played, and deleted](#the-feature-i-built-played-and-deleted)
- [What went wrong, and what caught it](#what-went-wrong-and-what-caught-it)
- [The stack, honestly](#the-stack-honestly)
- [Running it](#running-it)
- [Credits](#credits)
- [What I would do next](#what-i-would-do-next)

---

## The number, and what it actually showed

**13 of 16 utterances matched the intended command. 81.2%.** Against a threshold of 80% I
set before running it.

Read that as a smoke test, because that is what it is. One speaker, one microphone, one
room, one afternoon. **n = 1.** It told me whether to keep going. It is not an evaluation of
anything.

| | |
|---|---|
| Model | Whisper tiny, ggml, CPU only, via whisper.cpp through whisper.unity |
| Audio | 16 kHz mono, energy-and-frequency voice detection on a 100 ms tick |
| Vocabulary | 5 intents: go, stop, left, right, open |
| Utterances | 16, prompted one at a time, read from screen |
| Correct | 13 resolved to the intended intent |
| Wrong | 3, all of them my matcher rather than the transcription |
| Median latency | 1875 ms, end of speech to resolved command |
| Speaker | One. Me. Malawian English |

The split matters more than the total, and it took four sessions to get one honest number.
The first three were destroyed by my own bugs: a harness that advanced on silence, then an
arm key that doubled as the Stop binding so the keyboard answered every task before the
microphone opened. Those runs are still in the repo. They are why I now verify build
artefacts rather than exit codes.

**Speech recognition was never the problem.** In the session that diagnosed everything,
Whisper transcribed roughly 15 of 17 utterances correctly. My matcher then threw away six of
them. "Let's continue" meant nothing to it. "Whoa, whoa, whoa" meant nothing to it. And
"start moving" resolved to **Stop**, because it is three edit-distance steps from "stop
moving". A sentence landing on its own opposite, through a door I built myself.

### Framing is input design

The spike's first task read *"Tell the rover to start moving."* I said "Start moving." My
own prompt supplied a phrasing the vocabulary did not contain, and then the system punished
me for using it.

That is the most useful thing I learned here. **In a voice interface, the words on screen are
part of the input design.** Every prompt is a suggestion whether you meant it as one or not.
Get the framing wrong and you have built a trap that tells people what to say and then fails
to understand it.

It happened twice more before I learned it properly. See
[what went wrong](#what-went-wrong-and-what-caught-it).

### One accent-specific finding

Whisper's tiny model consistently transcribes my "turn" as **"10"**.

```
"10 to the left."     "10 right."      "10 back."
"10th of the right."  "Left 10 ahead"  "tend to the left"
```

Six occurrences in one sitting. Not noise, a repeatable error on one word in my voice. It
mostly does not matter, because the direction word survives alongside it. It cost a point
only when the direction was carried entirely by the word the model got wrong.

This is what the research predicts. Evaluations on EdAcc and AfriSpeech find Whisper
performs worse on African-accented English than on North American English, and the tiny and
base models are the worst offenders. Koenecke et al. (PNAS, 2020) found commercial
recognisers roughly doubled their word error rate for Black speakers against white speakers,
0.35 against 0.19, on the same scripted phrases.

The published guideline for this case is *Base speech recognition on individual words from a
small vocabulary* (Intermediate, Speech), the only place in the corpus that names regional
accents as a failure mode. That is why the vocabulary here is nineteen intents rather than
free dictation. A small vocabulary is not a limitation I settled for, it is the documented
mitigation.

I could have hidden this by seeding the model with my vocabulary and re-running until the
number flattered me. It seemed a poor way to open a project about being heard.

---

## Why I made this

A game decides who gets to play the moment it decides how it must be played.

That decision is usually invisible and almost never malicious. It is made early, by someone
who assumed one kind of body and one kind of voice, and then it hardens into everything built
on top of it. By the time anyone notices, the fix costs a rewrite.

So I wanted the smallest possible game where that decision is the subject rather than the
oversight. The rover is not broken. It is fully capable and completely still, and it stays
still until it is addressed. The disability is not in the machine. It is in the gap between
what it can do and how it is allowed to be asked.

There was a selfish reason too, and it turned out to be the same reason.

I usually have something on while I work. Radio, a song, a film in the corner of the screen.
I like silence, but sometimes you need the contrast to enjoy it. What I wanted was a game
with no pressure: **look at it, tell it what to do, come back later and see what happened.**
You can leave mid-sentence. Nothing punishes you for having a life going on around it.

Then I built it accessibility first, as the starting constraint rather than a feature to add
near the end, and that decision is what produced the game I wanted. Design so nobody is
forced to react quickly. So nothing can be failed. So every input has an equivalent. So the
settings are there from the first second and are never unlocked by progress. **That is the
same list as designing something a person can put down and pick up again.**

Accessibility is usually discussed as a cost you absorb. Here it was the design.

One thing I did not anticipate: **you cannot play this with other people in the room, and
that is now intentional.** The voice is meant to be someone on a call with you. If there are
people around, talk to them instead.

I am not claiming this is research. It is a small game by one person with a documented
argument and its working shown. It is also my best work in Unity so far, following a 2D game
in Godot, and I picked 3D deliberately to learn it.

---

## The game

Six sectors of a relay line, on a planet that never gets named.

The shuttle is in cooldown and cannot lift until it charges, and it is not charging, because
something has gone wrong with the line between the surface team and the ground station. There
is a rover out there. The automated control went down with everything else. Your uplink is
the only one still live, so it has to be you.

You drive it along the cable and scan each section until you find the break. Nineteen
checkpoints, one fault, at the last checkpoint of the last sector.

Nothing can be failed. No lives, no timer, no score. The worst outcome available is that
something takes longer, and if you get properly stuck Control eventually says so and drives
the rover back to the line itself.

### What each part is arguing

| Design choice | What it says |
|---|---|
| The rover is capable but helpless until addressed | Exclusion is a design failure, not a personal one |
| On-device recognition that understands natural phrasing | Interfaces should adapt to people, not people to interfaces |
| Every voice command has a keyboard equivalent, always | There is no normal player, and no mode is the lesser mode |
| Settings on from first launch, never unlocked | Access is not a reward you earn by playing well |
| The rover always shows you what it heard | Silence after you speak is indistinguishable from being ignored |
| A command given during a scan is held and shown as waiting | Swallowing an instruction is the one thing a game about listening may never do |
| The rover eases off when it hears you start talking | Attention can be shown before understanding arrives. See below |
| Recorded audio is discarded the moment it is transcribed | I got this wrong first and had to fix it |

---

## Latency, which is the real problem

Voice control lives or dies on this, and it is where most of the interesting work went.

**There is no such thing as zero-latency voice.** Measured on this machine: about 1.9
seconds from the end of speech to a resolved command. At the rover's 2.5 m/s that is roughly
five metres between meaning "stop" and the game knowing it.

Two things eat the time:

| Stage | Cost | Why |
|---|---|---|
| Voice activity detection | 300–500 ms | It cannot know you finished until you stop. Dead time |
| Whisper encoder | most of the rest | It processes a full **30-second window** whatever length you actually spoke |

The second is the interesting waste, and it is a configuration nobody set rather than a model
limitation. Capping the audio context to a few hundred tokens instead of the default 1500
should give roughly a **3x encoder speedup** on a one-second command, per whisper.cpp's own
benchmarks. Not yet measured on my hardware, and that distinction matters.

Everyone attacks the delay itself: smaller models, streaming, keyword spotting, cloud. That
is an arms race a local tiny model cannot win, and it never reaches zero anyway.

**So the approach here is the opposite. Accept the latency and remove its cost.**

### Speech carries two signals, not one

| Signal | Available in | Tells you |
|---|---|---|
| Someone is speaking | ~100 ms, from VAD | that a command is coming |
| What they said | ~2000 ms, from ASR | which command |

Most systems discard the first and wait for the second. This one uses it.

**The instant voice detection fires, before any transcription exists, the rover eases off.**
It does not guess what you said, because guessing wrong is expensive. It becomes more
cautious, because being cautious is never wrong.

That is copied from people, and it came from thinking about interfaces rather than about
speech. **We do not expect a reply the moment we start talking.** When someone addresses
you, you stop what you are doing and turn towards them. You have not heard the sentence yet.
You are signalling that you are listening, and buying yourself a moment to understand.

The principle underneath it, which is the part I would defend: **anticipate only with actions
you cannot be wrong about.** Slowing down costs nothing if the command turns out to be
"left". Guessing "stop" and being wrong costs everything. That is what separates this from
speculative execution, which guesses and can be wrong.

The delay is also made diegetic rather than hidden. The panel shows a signal crossing a gap,
not a spinner. A spinner says the software is struggling. A travelling signal says the rover
is far away. The wait is identical and only one of them is true.

### What is prior art, and what is not

I had this checked before claiming anything. Full findings in
[docs/latency-prior-art.md](docs/latency-prior-art.md).

| Technique | Verdict |
|---|---|
| Anticipatory deceleration on speech onset | **Adjacent.** Barge-in is the content-free precedent. Loth et al. (2018) formalises error-cost-calibrated commitment to early speech hypotheses, which is exactly this argument. Applying it to a continuous control variable is not documented |
| Rollback compensation for speech latency | **Adjacent, and the one unclaimed corner.** Mauve et al. (2004) is canonical for network latency. Claypool's ACM survey organises 80+ papers into 11 techniques and covers **network latency only**, with no entry for input-modality latency and nothing on voice. Honest framing is transfer, not invention |
| Equalising latency across keyboard and voice | **Established, and contested.** Zander et al. (ACE 2005) is the same idea with players in place of modalities. Two CHI papers argue it degrades the fast path without helping the slow one |

That third row is a design decision I made and then found the literature had already named
and partly argued against. It stays in the game, reframed as a decision rather than a
contribution, and it is on the list to actually test. Publishing that I tested my own design
choice and it failed would be worth more than the choice was.

### Where this goes next

Two recognisers instead of one. A keyword spotter running continuously on the six movement
commands at 50 to 150 ms, and Whisper reserved for conversation, where you can afford to wait
because the sentence actually needs understanding.

**Latency proportional to how much the command needs to be understood.** "Stop" barely needs
understanding. "Who are you" needs a lot.

The thing I care about more than making latency small is making it **controllable**. With a
dial from 100 ms to 2000 ms you can ask the question nobody has answered for voice-controlled
vehicles: how much latency can a player tolerate, and which compensations help at which point
on that curve.

---

## The architecture is the argument

The whole thesis is one interface:

```csharp
public interface ICommandProducer
{
    event Action<Intent> CommandProduced;
    bool IsAvailable { get; }
    string DisplayName { get; }
}
```

Voice and keyboard both implement it. Both feed one `CommandBus`. Everything downstream
receives an `Intent` and **cannot tell which one produced it**. The rover, the doors and the
lights have no way to ask.

`Intent` does carry a `CommandSource`, used for feedback and logging. Nothing in any gameplay
rule may branch on it. The moment you write `if (source == CommandSource.Voice)` in a rule,
you have made one way of playing the real one and the other a courtesy.

The working rule: a command is not finished until every producer can raise it. Both ship in
the same commit or the feature is not done.

There is a second piece worth naming. Everything spoken goes through one `VoiceArbiter`: one
queue, one `AudioSource`, four priority tiers. It closed twelve defects at once, because the
actual bug was never any of the twelve. It was that nobody was in charge of the speaker.

**Every scene in this game is generated by an Editor script. None is hand-authored.** That is
not a preference. A hand-placed scene is a binary blob nobody can review in a diff, and the
settings that matter here, a probe distance, a transport delay, a checkpoint spacing, are
exactly the things that go wrong silently. It is also what let me check level geometry by
arithmetic instead of by eye, which is how three separate bugs were found.

The exception is props. Rocks and buildings can be moved by hand and saved to a text file
that survives regeneration, because losing an afternoon of composition to a rebuild is a real
cost and one prop position is not load-bearing.

---

## Understanding people rather than keywords

Whisper returns a sentence, not a command. People say "okay go ahead now", "could you open the
door", "uh, left I think". Matching exact keywords would push the work back onto the player,
which is the behaviour this game exists to argue with.

So there is a plain, deterministic matching layer: normalise, strip politeness, match whole
phrases, then fall back to edit distance for near misses. It has no Unity dependency, so it
is tested with plain `dotnet` in about a second.

That testing earned its keep immediately, catching defects before I ever spoke into a
microphone:

| Said | Did | Now |
|---|---|---|
| "don't stop" | **stop** | no command |
| "do not go" | **go** | no command |
| "all right" | **turn right** | no command |

The negation bug is the one that mattered. A rover that hears "don't stop" as **stop** is not
making a near miss, it is doing the opposite of what you said.

The cause was three characters wide. My text normaliser turned an apostrophe into a space, so
`don't` became `don t` and never matched the negation word. Whisper writes contractions
constantly, usually with a curly `’`. Both are handled now.

Had I not tested this layer separately, a bad session would have looked like "the model
misheard me" and I would have gone hunting for a bigger model. The fault was mine.

**And this layer is still the weakest part of the stack.** My own measurement says so: the
recogniser got 15 of 17 and this code discarded six. There is a deeper problem than the
individual bugs. The vocabulary knowledge is in the wrong place, downstream of a free-form
recogniser, when the recogniser itself can be biased toward a command set. I am matching *up*
from free text when I could be constraining *down*.

I looked at fixing that by constraining the decoder to a grammar and decided against it, for
a reason worth recording: **a constrained decoder cannot abstain.** It forces every sound onto
the nearest legal command, so a cough produces a confident match. For a game whose premise is
that the rover only acts when spoken to, a recogniser that structurally cannot say "I did not
understand" is the wrong instrument.

What I am doing instead is smaller and better founded. whisper.unity already exposes Whisper's
per-token acoustic confidence and this code throws it away, inventing a fixed 0.85 for a
whole-word match. Grounding confidence in the model's actual probabilities removes the
weakest part of the design.

---

## Accessibility, specifically

Titles below are quoted from the
[Game Accessibility Guidelines](https://gameaccessibilityguidelines.com/), not paraphrased,
because a paraphrase is where an honest claim quietly becomes a flattering one.

The original plan was one guideline per level, six levels, six guidelines. That was the wrong
shape. Designing a level around a guideline produces a demonstration, not a game, and the
guidelines that matter most here apply everywhere at once. So the game came first and these
are what fell out of it.

### Implemented

| Guideline | Tier | Where |
|---|---|---|
| Provide subtitles for all important speech | Basic, Hearing | Every spoken line has a caption written in the same method that starts the audio, so they cannot drift apart |
| Ensure no essential information is conveyed by a fixed colour alone | Basic, Vision | Every sound has a visual twin and every colour signal has text, enforced in `RoverLight`, the only place the rover can express anything |
| Provide high contrast between text/UI and background | Basic, Vision | High contrast setting; panels go solid black rather than translucent |
| Allow controls to be remapped / reconfigured | Basic, General | Keyboard rebinding, and its voice equivalent: teach the rover a new phrase for any command |
| Ensure that all settings are saved / remembered | Basic, General | All settings persist, available from launch and during play |
| Include an option to adjust the game speed | Basic, Motor | 0.25x to 1.5x. The command delay stays fixed, so slowing the game shrinks the ground a late instruction costs you |
| Provide separate volume controls for effects, speech and background | Basic, Hearing | World volume is separate from voice. At zero you lose nothing you need |
| Ensure that speech input is not required, and included only as a supplementary / alternative input method | Basic, Speech | Full keyboard parity, in the same commit as the voice path |
| Base speech recognition on individual words from a small vocabulary | Intermediate, Speech | Nineteen intents, matched on phrases and single words, never free-form |
| Base speech recognition on hitting a volume threshold rather than word recognition | Advanced, Speech | Any sound can be recorded and bound to a command |

### Partial

| Guideline | Why only partial |
|---|---|
| Offer a wide choice of difficulty levels | There is no difficulty setting. There is also no failure state, no lives and no timer, which covers some of the same ground, but it is not the same claim |
| Ensure subtitles are or can be turned on before any sound is played | Settings are reachable from launch and subtitles default to on. There is no explicit pre-audio prompt |

### Deliberately not implemented

| Guideline | Why |
|---|---|
| Full keyboard-only navigation of all menus | The settings screen takes mouse and keyboard, but the menu is not fully keyboard-navigable. Known gap, not a position |
| Screen reader support | Not attempted. Claiming a partial implementation would be worse than admitting none |

### The correction I had to make

An earlier draft of this file said the Speech category was empty at Basic level, and built a
paragraph on top of that about voice-first design working ahead of received wisdom.

It is not empty. It contains *Ensure that speech input is not required, and included only as a
supplementary / alternative input method*, the single most relevant guideline in the set to
this project, and it says close to the opposite of what I assumed the gap implied. **Voice is
not the accessible option here. Voice is the option that has to be optional**, and the
keyboard parity I had been treating as a courtesy is the actual compliance mechanism.

I am leaving this paragraph in rather than quietly fixing the table, because getting it
backwards and then finding out is more useful to a reader than a table that was always right.

### Where voice goes, for accessibility

The part I think matters most is **voice remapping**. You can teach the rover your own word
for any command. A keyboard player rebinds a key; a voice player should be able to rebind a
word.

And the direction beyond this project: voice is going to matter for player assist. Asking
questions mid-game. Adjusting a level for one objective without changing the rest. **The game
adapting to the person rather than the person to the game.**

---

## The feature I built, played, and deleted

The ending used to be your own voice.

Every command you gave across the whole game was kept, in order, unedited. At the rim the
rover handed you the open loop and it all went out, and a field of dormant rovers woke to the
sound of you. On paper it was the best idea in the project: the thing that reaches everyone
else was never translated into anything, it was just the player, played back.

I built it. It worked. Then I played it, and it felt like being watched.

The reason is simple and I did not see it until I heard it. **A microphone does not record
commands. It records a room.** What came back was not me giving instructions, it was the
background of wherever I had been sitting, captured alongside them and played at me later.
Nothing had leaked anywhere. The audio never left the machine. It still felt like an
intrusion, because retention is its own event regardless of where the file sits.

So the retention went, not just the ending. `VoiceJournal` is deleted. Audio is transcribed
and dropped inside the method that receives it; nothing copies it, stores it or writes it to
disk. The transcript survives for as long as it takes to become a command, and the sound does
not survive at all.

This is a stronger claim than the one it replaces. "Nothing leaves your machine" and "nothing
is kept" sound similar and are not, and the first is easy to say while doing the second badly.
Under GDPR the relevant principles are storage limitation and data minimisation, and the
EDPB's guidance on voice assistants is direct about deleting audio once the command has been
carried out. I did not arrive at that from the regulation. I arrived at it from playing my own
game and not liking how it felt, then found the regulation had been there the whole time.

The ending is now the repair. You find the break in the line, you fix it, the power comes
back. It is smaller and it is about the thing you actually did.

---

## What went wrong, and what caught it

A section for the failures, because they are the most useful thing here and because most of
them were invisible to everything except a person playing.

### The bug that shipped twice

A prompt is a promise. Twice I wrote one the game could not keep.

The blockage conversation named three answers out loud, cut, dissolve and push, and **none of
the three existed in the vocabulary.** All three resolved to nothing and were reported as not
understood. On the keyboard it was worse: a key press carries its key name as text, so the
substring match saw "W" and matched nothing, ever, while the rover was held for a scan. A
keyboard player was stuck in front of a rock in level one with no input that could free them.

Then the ending asked the player to "patch it", and patch, fix, repair and mend were **also
all missing**, which made the game uncompletable by anybody.

Same mistake, one day apart. The prompt text lives in one file and the vocabulary in another,
with nothing connecting them.

### What actually catches things

Four bugs shipped that compiled cleanly, rendered correctly, and looked right in screenshots.

| Bug | Why nothing caught it |
|---|---|
| Prompts naming words the vocabulary lacked | Text and vocabulary are separate files |
| Two progress bars permanently full | `Image.Type.Filled` with no sprite silently ignores `fillAmount` and draws one full quad |
| Level 1 finishable in two words | An exit trigger overhung the start lane by three metres |
| Level 5 mathematically inescapable | Shortest legal reverse overshot the target pad by 31 cm |

So there are now **assertions inside the scene builders that fail the build**: every word a
prompt names must resolve to a real intent, no exit may be reachable in under 85% of the
route, and no cable may cross a hazard. The first two would have caught three of the four
above.

One of those guards immediately caught itself, which was instructive. It reported a hazard
crossing the cable that the scene file showed sitting 2.5 units clear. `Collider.bounds` comes
from the physics engine's copy of the transform, and that copy only refreshes on a physics
step, which never happens in an Editor script. Every hazard was reporting bounds centred on
zero. **A check that reports problems which are not there is worse than no check**, because
the fix for a false positive is to move geometry that was already correct.

### The discipline that came out of it

- **Verify artefacts, not exit codes.** Unity batch mode detaches on Windows and reports
  success while the build has not started. The `.exe` stub never changes; check the assembly
  timestamp.
- **Verify audio numerically.** Both audio bugs in this project were catchable with `ffprobe`
  and `volumedetect`, and both would have survived a listen. One was a sample rate
  reinterpretation playing a voice 1.86x fast. One was an ambient loop peaking at −40 dBFS.
- **Render the scene and actually look at it.** This caught floating geometry and a camera
  pointed at the ground.
- **Compare `origin/main` to local HEAD.** Twenty-two commits once sat unpushed while I
  reported them as pushed, because an error stream was being suppressed.
- **Nothing replaces someone playing it.** Every item in the table above was found by a human
  in the first twenty minutes.

---

## The stack, honestly

Because a reviewer reading the source cannot tell a decision from a default, and for a while
neither could I.

### Decided, with reasons

| Element | Why |
|---|---|
| Unity 6000.5.2f1 over 6.3 LTS | I recommended LTS on support-window grounds, then reversed after 6.3 failed to download twice and 6.5 demonstrably worked. Evidence beat preference |
| whisper.cpp via whisper.unity | Offline was non-negotiable, MIT, C++ with no runtime, and maintained Unity bindings already existed |
| Scenes generated by code | Diffable. See [above](#the-architecture-is-the-argument) |
| Keyboard parity in the same commit as voice | It is the argument, not a feature |
| Deleting voice retention | Built it, played it, it felt wrong |
| Dropping `KeywordRecognizer` for the fast path | Unity documents it as Windows 10 only, Microsoft deprecated the platform beneath it, it throws rather than degrades, and nothing confirms it can share a microphone with an open capture |
| Synthesising all sound effects with ffmpeg | Reproducible from one script, no licence, no attribution |

### Defaults nobody chose

| Element | Reality |
|---|---|
| Built-in render pipeline | Almost certainly never considered. Unity has been winding it down for years |
| Greedy decoding | The default. Beam search never evaluated |
| Full 30-second audio context | The default, and the single biggest latency waste in the project |
| Multilingual tiny with language forced to English | `tiny.en` is on disk, unused. Either 74 MB of waste or a free accuracy gain |
| No test framework | The `dotnet` console project was invented ad hoc when the need appeared |

**No alternatives to Whisper were benchmarked.** Vosk, sherpa-onnx and the rest were not
evaluated. The choice was reasonable engineering and it was not a comparative evaluation, and
for a nineteen-word vocabulary a narrower tool may well have suited a command grammar better.

Saying so is more useful than a flawless-choice narrative, and it is the honest state of the
project.

---

## How this was built

Three weeks of mornings and evenings, alongside a research assistant post. Reduced email and
a clear calendar meant large uninterrupted blocks, which is most of why it exists at all.

**I used Claude heavily throughout**: architecture, C# I would have written more slowly and
worse, the test suite, and this document. I am not going to hide that in a portfolio piece
aimed at a group that studies interaction.

Getting help has always been part of this work. Browsing Stack Overflow. Using excellent open
source libraries somebody else wrote with care, imported in one line. This is a difference of
degree rather than of kind, and the interesting question is not whether to use the tools but
what changes when everyone knows you did.

That question is also my thesis finding, and it landed here in practice: **once AI use is
openly acknowledged, attention moves off the fact of use and onto the dynamics it creates.**
Accountability. Attribution. Verification of outputs. And respecting people who choose not to
use the tools, for reasons that are theirs.

Verification is the one this project felt most. Almost every discipline in
[what went wrong](#what-went-wrong-and-what-caught-it) exists because something plausible was
produced quickly and turned out to be wrong in a way that only measurement caught. The speed
is real. It moves the bottleneck to checking, and checking is not free.

Where it helped most, concretely: avoiding gotchas by researching libraries before committing
to them, finding simpler ways to do the same thing, and debugging problems that could have
taken days or made me abandon the project. Where it cost: at one point I had lost track of my
own repository and had to read it back to understand what everything did. There are now areas
I will confidently modify and areas I will not touch, and knowing which is which took
deliberate work.

**There is a level of skill and experience that cannot be substituted for doing the thing,
and no tool crosses that boundary.** Did this take away an opportunity to learn something? I
cannot know what I did not learn. What I can say is that it kept me on the parts I care
about, and that the judgement calls, the argument, the design and every decision about what
this game is for are mine.

---

## Running it

**Windows build** is the real one, with voice and keyboard.

**Browser build** is keyboard only. Speech recognition here is whisper.cpp, native code
compiled per platform, and there is no WebAssembly build of it in the Unity package this
project uses. So the browser version ships without voice and says so in the settings screen,
rather than showing a microphone button that quietly does nothing. The voice control is the
point; the browser build is the trailer.

Model weights are not in this repository, because a 74 MB binary does not belong in git
history. Fetch them with the setup script and the project will find them in
`StreamingAssets`.

Regenerating everything, which is how this project is built:

```
Unity.exe -batchmode -quit -nographics -projectPath . -executeMethod GenerateAll.Generate
Unity.exe -batchmode -quit -nographics -projectPath . -executeMethod DikdikBuild.WindowsFast
dotnet run --project tools/matcher-tests
```

---

## Credits

- [whisper.unity](https://github.com/Macoron/whisper.unity) by Macoron, MIT, Unity bindings
  for whisper.cpp. Everything hard about running Whisper locally is theirs.
- [whisper.cpp](https://github.com/ggerganov/whisper.cpp) by Georgi Gerganov.
- [Game Accessibility Guidelines](https://gameaccessibilityguidelines.com/), a collaborative
  effort by studios, specialists and academics.
- Art and audio from [Kenney](https://kenney.nl), CC0.
- Claude, used as described in [how this was built](#how-this-was-built).

### Reading behind the design

Full prior-art notes in [docs/latency-prior-art.md](docs/latency-prior-art.md).

- Loth, Jettka, Giuliani, Kopp & de Ruiter (2018). *Confidence in uncertainty: Error cost and
  commitment in early speech hypotheses.* PLoS ONE 13(8).
- Mauve, Vogel, Hilt & Effelsberg (2004). *Local-lag and timewarp.* IEEE Trans. Multimedia 6(1).
- Liu, Xu & Claypool (2022). *A Survey and Taxonomy of Latency Compensation Techniques for
  Network Computer Games.* ACM Computing Surveys 54(11s).
- Zander, Leeder & Armitage (2005). *Achieving fairness in multiplayer network games through
  automated latency balancing.* ACE '05.
- Allison, Carter, Gibbs & Smith (2018). *Design Patterns for Voice Interaction in Games.*
  CHI PLAY.
- Zargham et al. (2024). *"I Know What You Mean": Context-Aware Recognition to Enhance
  Speech-Based Games.* CHI.
- Limerick, Moore & Coyle (2015). *Empirical evidence for a diminished sense of agency in
  speech interfaces.* CHI.
- Koenecke et al. (2020). *Racial disparities in automated speech recognition.* PNAS 117(14).

---

## What I would do next

Designed, costed, and deliberately not built. Longer write-ups in
[docs/future-directions.md](docs/future-directions.md), and open items in
[docs/OPEN-ISSUES.md](docs/OPEN-ISSUES.md).

**Non-verbal input, and it is the one that matters.** During remapping, record any sound at
all: a grunt, a hum, a whistle, a click. Map each to a command. The game becomes playable
without a single word.

**The latency study.** Rollback compensation on "stop", anticipation on "turn", measured
separately because they cancel each other if run as a 2x2. Against forward prediction as the
baseline, which is what teleoperation robotics actually does.

**Test the parity decision** rather than defend it. The literature predicts the uniform
keyboard delay hurts without helping. If the data agrees, that is worth publishing.

**The controller as an edge device.** The thing that started this: a controller doing local
audio processing, so voice input does not depend on the host machine having a model loaded.

If you want to contribute, the future-directions list is the place to start.
