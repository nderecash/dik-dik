# Dik-dik

A small game about a rover that cannot move until someone speaks to it.

You are the voice on its radio. Speech recognition runs on your own machine, offline, with
no account and no cloud. Every command also works on the keyboard, and the game never
treats one of those as the real way to play.

> **Status: in development.** The speech pipeline works. Measured **81.2%** on my own
> voice, against a threshold of 80% (13 of 16 utterances, median latency 1875 ms, Whisper
> tiny on CPU). Levels are being built.

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
tiny and base models are the worst offenders. I could have hidden this by seeding the model
with my vocabulary and re-running until the number flattered me. It seemed a poor way to
open a project about being heard.

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
| Each level embodies one documented barrier | The problems are specific, published, and fixable |
| Accessibility settings are on from first launch, never unlocked | Access is not a reward you earn by playing well |
| The rover always shows you what it heard | Silence after you speak is indistinguishable from being ignored |
| Free assets, one person, short | You do not need a studio to make this point |

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

Each level embodies one item from the
[Game Accessibility Guidelines](https://gameaccessibilityguidelines.com/) at Basic level,
quoted rather than paraphrased. Levels are being designed; this table will be filled in as
they are built, and each claim checked against the actual build before it is written here.

One observation while working from those guidelines: **the Speech category is empty at
Basic level.** The published guidance has almost nothing to say about voice input. This
whole game is voice input. I do not think that means the guidelines are wrong; they cover
what the industry has actually had to solve. It does mean anyone building voice-first is
working somewhere the received wisdom has not reached yet, and should be honest about that
rather than pretending otherwise.

---

## Why on-device

Speech recognition runs locally through [whisper.cpp](https://github.com/ggerganov/whisper.cpp).
No network, no account, no audio leaving the machine.

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

<!-- Moses: this is the research-context framing you picked. Sharpen it in your own terms
     if you want to; I have deliberately not written anything about your accent or where
     you are from, because that is yours to say and not mine to assume. -->


---

## Running it

**Browser build** plays with the keyboard. Speech recognition needs native code that does
not run in WebGL, so the browser build ships without it and says so in the settings screen
rather than showing a microphone button that quietly does nothing.

**Windows build** has both.

Building from source: model weights are not in this repository, because a 74 MB binary
does not belong in git history. Fetch them with the setup script and the project will find
them in `StreamingAssets`.

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
