# Dik-dik

A small game about a rover that cannot move until someone speaks to it.

You are the voice on its radio. Speech recognition runs on your own machine, offline, with
no account and no cloud. Every command also works on the keyboard, and the game never
treats one of those as the real way to play.

> **Status: playable, six sectors, not yet polished.**

### The number, with its working shown

**13 of 16 utterances matched the intended command. 81.2%.** Against a threshold of 80%
that I set before running it.

Read that as a smoke test, because that is what it is. One speaker, one microphone, one
room, one afternoon. **n = 1.** It is a first-person probe that told me whether to keep
going, not an evaluation of anything.

The conditions, so the number means something:

| | |
|---|---|
| Model | Whisper tiny, ggml, CPU only, via whisper.cpp through whisper.unity |
| Audio | 16 kHz mono, energy-and-frequency voice detection on a 100 ms tick |
| Vocabulary | 5 intents: go, stop, left, right, open |
| Utterances | 16, prompted one at a time, read aloud from screen |
| Correct | 13 resolved to the intended intent |
| Wrong | 3, all of them my matcher rather than the transcription |
| Median latency | 1875 ms, from end of speech to resolved command |
| Speaker | One. Me. Malawian English |

The split matters more than the total. Whisper heard almost everything; my own matcher
threw away the rest. See below, because that is the actual finding.

---

## What the measurement actually showed

One person, one microphone, one room. That is a smoke test, not an evaluation, and it is
labelled as one everywhere it appears.

It took four sessions to get one honest number. The first three were destroyed by my own
bugs: a harness that advanced on silence, then an arm key that doubled as the Stop
binding so the keyboard answered every task before the microphone opened. Those runs are
still in the repo. They are the reason I now verify build artefacts rather than exit codes.

**Speech recognition was never the problem.** In the session that diagnosed everything,
Whisper transcribed roughly 15 of 17 utterances correctly. My matcher then threw away six
of them. "Let's continue" meant nothing to it. "Whoa, whoa, whoa" meant nothing to it.
And "Start moving" resolved to **Stop**, because it is three edit-distance steps from
"stop moving" and my fuzzy fallback was 75% confident about it. A sentence landing on its
own opposite is the worst failure this game can produce, and it arrived through a door I
built myself.

Edit distance is now restricted to single words. It keeps the cases it earns its place on,
like "wright" for right, and can no longer match a sentence to its reverse.

### Framing is input design

The spike's first task read *"Tell the rover to start moving."* I said "Start moving." My
own prompt supplied a phrasing the vocabulary did not contain, and then the system punished
me for using it.

That is the most useful thing I learned building this. **In a voice interface, the words on
screen are part of the input design.** Every prompt is a suggestion whether you intended it
as one or not. Get the framing wrong and you have built a trap that tells people what to
say and then fails to understand it.

Every prompt and every line of level text in this game was rewritten because of that: they
describe the situation, not the action, so the player reaches for their own words.

### One accent-specific finding

Whisper's tiny model consistently transcribes my "turn" as **"10"**.

```
"10 to the left."     "10 right."      "10 back."
"10th of the right."  "Left 10 ahead"  "tend to the left"
```

Six occurrences in one sitting. Not random noise, a repeatable error on one word in my
voice. It mostly does not matter, because the direction word survives alongside it and the
fuzzy matching catches the sentence anyway. It cost a point only when the direction was
carried entirely by the word the model got wrong.

This is what the research predicts. Evaluations on the EdAcc and AfriSpeech datasets find
Whisper performs worse on African-accented English than on North American English, and the
tiny and base models are the worst offenders. Koenecke et al. (PNAS, 2020) found commercial
recognisers roughly doubled their word error rate for Black speakers against white speakers,
0.35 against 0.19, on the same scripted phrases.

The published guideline for this case is *Base speech recognition on individual words from a
small vocabulary* (Intermediate, Speech), which is the only place in the corpus that names
regional accents as a recognition failure mode. That is why the vocabulary here is eleven
intents rather than free-form dictation: a small vocabulary is not a limitation I settled
for, it is the documented mitigation.

I could have hidden this by seeding the model with my vocabulary and re-running until the
number flattered me. It seemed a poor way to open a project about being heard.

---

## Why I made this

A game decides who gets to play the moment it decides how it must be played.

That decision is usually invisible, and it is almost never malicious. It is made early, by
someone who assumed one kind of body and one kind of voice, and then it hardens into
everything built on top of it. By the time anyone notices, the fix costs a rewrite.

So I wanted to build the smallest possible game where that decision is the subject rather
than the oversight. The rover is not broken. It is fully capable and completely still, and
it stays still until it is addressed. The disability is not in the machine. It is in the
gap between what it can do and how it is allowed to be asked.

I am not claiming this is research. It is a small game by one person with a documented
argument and its working shown.

---

## What each part is arguing

| Design choice | What it says |
|---|---|
| The rover is capable but helpless until addressed | Exclusion is a design failure, not a personal one |
| On-device speech recognition, understands natural phrasing | Interfaces should adapt to people, not people to interfaces |
| Every voice command has a keyboard equivalent, always | There is no normal player, and no mode is the lesser mode |
| Accessibility settings are on from first launch, never unlocked | Access is not a reward you earn by playing well |
| The rover always shows you what it heard | Silence after you speak is indistinguishable from being ignored |
| A command given during a scan is held and shown as waiting | Swallowing an instruction in silence is the one thing a game about listening may never do |
| The rover eases off when it hears you start talking, before knowing the words | Attention is not the same as obedience, and it can be shown before understanding arrives |
| Recorded audio is discarded the moment it is transcribed | See below. This one I got wrong first and had to fix |
| Free assets, one person, short | You do not need a studio to make this point |

---

## The game

Six sectors of a relay line, on a planet that never gets named.

The shuttle is in cooldown and cannot lift until it charges, and it is not charging, because
something has gone wrong with the line between the surface team and the ground station.
There is a rover out there. The automated control went down with everything else. Your
uplink is the only one still live, so it has to be you.

You drive it along the cable and scan each section until you find the break. Nineteen
checkpoints, one fault, and it is at the last checkpoint of the last sector.

Nothing in it can be failed. There are no lives, no timer, no score and no losing. The
worst outcome available is that something takes longer, and if you get properly stuck
Control eventually says so and drives the rover back to the line itself.

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
receives an `Intent` and **cannot tell which one produced it**. The rover, the doors and
the lights have no way to ask.

`Intent` does carry a `CommandSource`, used for feedback and logging. Nothing in any
gameplay rule is allowed to branch on it. The moment you write
`if (source == CommandSource.Voice)` in a rule, you have made one way of playing the real
one and the other a courtesy.

The working rule while building: a command is not finished until every producer can raise
it. Both ship in the same commit or the feature is not done.

---

## Understanding people rather than keywords

Whisper returns a sentence, not a command. People say "okay go ahead now", "could you open
the door", "uh, left I think". Matching exact keywords would push the work back onto the
player, which is the behaviour this game exists to argue with.

So there is a plain, deterministic matching layer: normalise, strip politeness, match whole
phrases, then fall back to edit distance for near misses. It has no Unity dependency, so it
is tested with plain `dotnet` in about a second.

That testing earned its keep immediately. It caught five real defects before I ever spoke
into a microphone:

| Said | Did | Now |
|---|---|---|
| "don't stop" | **stop** | no command |
| "do not go" | **go** | no command |
| "all right" | **turn right** | no command |

The negation bug is the one that mattered. A rover that hears "don't stop" as **stop** is
not making a near miss, it is doing the opposite of what you said. In a game about being
understood that is the worst failure available.

The cause was three characters wide: my text normaliser turned an apostrophe into a space,
so `don't` became `don t` and never matched the negation word. Whisper writes contractions
constantly, usually with a curly `’`. Both are handled now.

Had I not tested this layer separately, a bad recognition session would have looked like
"the model misheard me" and I would have gone hunting for a bigger model. The fault was
mine.

---

## Accessibility, specifically

Titles below are quoted from the
[Game Accessibility Guidelines](https://gameaccessibilityguidelines.com/), not paraphrased,
because a paraphrase is where an honest claim quietly becomes a flattering one.

The original plan was one guideline per level, six levels, six guidelines. That turned out
to be the wrong shape. Designing a level around a guideline produces a demonstration, not a
game, and the guidelines that matter most here are the ones that apply everywhere at once
rather than in one level. So the game came first and these are what fell out of it.

### Implemented

| Guideline | Tier | Where |
|---|---|---|
| Provide subtitles for all important speech | Basic, Hearing | Every spoken line has a caption written in the same method that starts the audio, so they cannot drift apart |
| Ensure no essential information is conveyed by a fixed colour alone | Basic, Vision | Every sound has a visual twin and every colour signal has text. The rule is enforced in `RoverLight`, which is the only place the rover can express anything |
| Provide high contrast between text/UI and background | Basic, Vision | High contrast setting; panels go solid black rather than translucent |
| Allow controls to be remapped / reconfigured | Basic, General | Keyboard rebinding, and its voice equivalent: teach the rover a new phrase for any command |
| Ensure that all settings are saved / remembered | Basic, General | All settings persist; available from launch and during play |
| Include an option to adjust the game speed | Basic, Motor | 0.25x to 1.5x. The command delay stays fixed, so slowing the game shrinks the distance a late instruction costs you |
| Provide separate volume controls or mutes for effects, speech and background | Basic, Hearing | World volume is separate from voice. At zero you lose nothing you need |
| Ensure that speech input is not required, and included only as a supplementary / alternative input method | Basic, Speech | Full keyboard parity, in the same commit as the voice path, with a fixed delay applied to both so neither is faster |
| Base speech recognition on individual words from a small vocabulary | Intermediate, Speech | Eleven intents, matched on phrases and single words, never free-form |
| Base speech recognition on hitting a volume threshold rather than word recognition | Advanced, Speech | Any sound can be recorded and bound to a command |

### Partial

| Guideline | Why it is only partial |
|---|---|
| Offer a wide choice of difficulty levels | There is no difficulty setting. There is also no failure state, no lives and no timer, which covers some of the same ground, but it is not the same claim and I am not going to make it |
| Ensure subtitles / captions are or can be turned on before any sound is played | Settings are reachable from launch, and subtitles default to on. There is no explicit pre-audio prompt |

### Deliberately not implemented

| Guideline | Why |
|---|---|
| Full keyboard-only navigation of all menus | The settings screen is mouse and keyboard, but the menu itself is not fully keyboard-navigable. Known gap, not a design position |
| Screen reader support | Not attempted. Claiming a partial implementation would be worse than admitting none |

### The correction I had to make

An earlier draft of this file said the Speech category was empty at Basic level, and built a
paragraph on top of that about voice-first design working ahead of received wisdom.

It is not empty. It contains *Ensure that speech input is not required, and included only as
a supplementary / alternative input method*, which is the single most relevant guideline in
the whole set to this project, and it says almost the opposite of what I had assumed the
gap implied. Voice is not the accessible option here. Voice is the option that has to be
optional, and the keyboard parity I had been treating as a courtesy is the actual compliance
mechanism.

I am leaving this paragraph in rather than quietly fixing the table, because getting that
backwards and then finding out is more useful to anyone reading than a table that was always
right.

---

## The feature I built, played, and deleted

The ending used to be your own voice.

Every command you gave across the whole game was kept, in order, unedited. At the rim the
rover handed you the open loop and it all went out, and a field of dormant rovers woke to
the sound of you. On paper it was the best idea in the project: the thing that reaches
everyone else was never translated into anything, it was just the player, played back.

I built it. It worked. Then I played it, and it felt like being watched.

The reason is simple and I did not see it until I heard it. A microphone does not record
commands. It records a room. What came back was not me giving instructions, it was the
background of wherever I had been sitting, captured alongside them and played back at me
later. Nothing had leaked anywhere. The audio never left the machine. It still felt like an
intrusion, because retention is its own event regardless of where the file sits.

So the retention went, not just the ending. `VoiceJournal` is deleted. Audio is transcribed
and dropped inside the method that receives it; nothing copies it, stores it or writes it
to disk. The transcript survives for as long as it takes to become a command, and the sound
does not survive at all.

This is a stronger claim than the one it replaces. "Nothing leaves your machine" and
"nothing is kept" sound similar and are not, and the first is the one that is easy to say
while doing the second badly. Under GDPR the relevant principles are storage limitation and
data minimisation, and the EDPB's guidance on voice assistants is direct about deleting
audio once the command has been carried out. I did not arrive at that from the regulation.
I arrived at it from playing my own game and not liking how it felt, and then found the
regulation had been there the whole time.

The ending is now the repair. You find the break in the line, you patch it, the power comes
back. It is smaller and it is about the thing you actually did.

---

## Why on-device

Speech recognition runs locally through [whisper.cpp](https://github.com/ggerganov/whisper.cpp).
No network, no account, no audio leaving the machine, and now nothing kept on it either.

That is partly a privacy position and partly a practical one: it works on a train, in a lab
with no wifi, and for anyone who cannot or will not send their voice to a company.

It also has a real cost, and pretending otherwise would be dishonest. On-device means small
models, and small models are the worst performers on under-represented accents. Published
evaluation on the EdAcc and AfriSpeech datasets finds Whisper does markedly worse on
African-accented English than on North American English, and the tiny and base models rank
at the bottom of those evaluations. The models small enough to run on a laptop are the ones
that serve the fewest people.

That is documented, expected, and I hit it firsthand within a day of building. The model
transcribed my "turn" as "10" six times in one sitting. It is a small, dull, entirely
predictable instance of a known result, which is exactly why it is worth publishing: the
gap is not hypothetical, and it does not take a study to meet it. It takes one person, one
microphone, and an afternoon.

I could have hidden it. Seeding the model with my own vocabulary would have improved the
number, and running the session again until it flattered me would have improved it further.
That seemed a poor way to open a project about being heard.

---

## Running it

**Windows build** is the real one, with voice and keyboard.

**Browser build** plays with the keyboard only. Speech recognition here is whisper.cpp,
which is native code compiled per platform, and there is no WebAssembly build of it in the
Unity package this project uses. So the browser version ships without voice and says so in
the settings screen, rather than showing a microphone button that quietly does nothing. The
voice control is the point of the project; the browser build is the trailer.

Building from source: model weights are not in this repository, because a 74 MB binary does
not belong in git history. Fetch them with the setup script and the project will find them
in `StreamingAssets`.

Regenerating everything, which is how this project is built:

```
Unity.exe -batchmode -quit -nographics -projectPath . -executeMethod GenerateAll.Generate
Unity.exe -batchmode -quit -nographics -projectPath . -executeMethod DikdikBuild.WindowsFast
dotnet run --project tools/matcher-tests
```

Every scene in this game is generated by an Editor script and none of it is hand-authored.
That is not a preference. A hand-placed scene is a binary blob that cannot be reviewed in a
diff, and the settings that matter here, a probe distance, a transport delay, a checkpoint
spacing, are exactly the things that go wrong silently and need to be readable.

---

## Credits

- [whisper.unity](https://github.com/Macoron/whisper.unity) by Macoron, MIT licensed,
  Unity bindings for whisper.cpp. Everything hard about running Whisper locally is theirs.
- [whisper.cpp](https://github.com/ggerganov/whisper.cpp) by Georgi Gerganov.
- [Game Accessibility Guidelines](https://gameaccessibilityguidelines.com/), a
  collaborative effort by a group of studios, specialists and academics.
- Art and audio from [Kenney](https://kenney.nl), CC0.
- **AI assistance.** I used Claude heavily throughout: architecture, C# I would have
  written more slowly and worse, the test suite, and this README. I am not going to hide
  that in a portfolio piece aimed at a research group that studies interaction. The
  judgement calls, the argument, the level design and every decision about what this game
  is for are mine. Using these tools well is part of the work now, and pretending
  otherwise would be both dishonest and pointless.

---

## What I would do next

Designed, costed, and deliberately not built. Full write-ups with the actual approaches are
in [docs/future-directions.md](docs/future-directions.md).

**Non-verbal input, and it is the one that matters.** During remapping, record any sound at
all: a grunt, a hum, a whistle, a click. Map each to a command. The game becomes playable
without a single word.

Every speech interface, this one included, assumes the player produces words in a language
the model was trained on. That excludes non-speaking people, people with speech differences,
people whose language has no model, and people who would simply rather not talk. A voice
interface that does not require speech is a different proposition to one that does.

Speech recognition is the wrong tool for it, and this project has its own evidence:
Whisper transcribed one of my recorded utterances as `[ Grunts ]`, and the code now discards
that as non-speech before it reaches the matcher. Asking an ASR model to hear a hum is
asking it to hallucinate words that were never there.

It does not need a model. Template matching on raw audio does it: record two or three
reference clips per intent, extract duration, mean pitch and energy envelope, match by
nearest distance with a rejection threshold. A few hundred lines against the `float[]` we
already have, and faster than the speech path because there is no inference.

**I did not run out of ideas here, I ran out of days.** It is three to five days for a
second complete input system, and the commitment at the start of this project was that ship
date beats scope. This is the largest thing that rule has cost, and it is first in the queue.

Also on the list: evaluation with more than one voice, which is the change that would move
this from a game with an argument to a piece of work with evidence. One-switch play, cut
because half-building it would be a broken promise to exactly the people it claims to serve.
A local language model for intent matching, rejected in favour of something debuggable, and
the logs show that was the right call.
