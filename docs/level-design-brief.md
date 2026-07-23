# Dik-dik: level design brief

You are designing **six** levels. This document gives you the verified constraints so
you are designing against real guidelines rather than remembered ones.

> Six, not five. Non-verbal input was briefly going to replace the sixth level and is now
> deferred to [future-directions.md](future-directions.md) instead, written up as designed
> and costed rather than as something we could not do. The rule from here on: anything
> expensive goes on that page rather than into the build.

Source for every quoted guideline below: the
[Game Accessibility Guidelines, Basic level](https://gameaccessibilityguidelines.com/basic/).

## The message

A game decides who gets to play the moment it decides how it must be played.

The rover is fully capable and completely still until someone speaks to it. The player
is the voice on the radio. That is the whole feeling: capability waiting on access.

The name fits better than I expected. A dik-dik is named after its own alarm call, a
sharp whistle it pushes through its nose. In Swahili it is *digidigi*. So the game is
named after an animal that is named after the sound it makes, and it is a game about a
machine that only moves when it hears a voice.

## The verified guideline list

These are the Basic-level items, quoted exactly. Basic means easy to implement, wide
reaching, applies to almost all game mechanics.

**Motor**
- Allow controls to be remapped / reconfigured
- Include an option to adjust the game speed
- Include an option to adjust the sensitivity of controls

**Cognitive**
- Use simple clear language
- Offer a wide choice of difficulty levels

**Vision**
- Provide high contrast between text/UI and background
- Use an easily readable default font size

**Hearing**
- Provide subtitles for all important speech
- If any subtitles / captions are used, present them in a clear, easy to read way
- Ensure no essential information is conveyed by sounds alone

**Speech**
- Nothing. The category exists and is empty at this level.

That last line is worth a paragraph in the README on its own. The published guidelines
have almost nothing to say about voice input. This game is entirely about voice input.
You are working in a gap, and you can say so without overclaiming.

## Three constraints

**1. Never gate a setting behind progress.** Every accessibility option sits in the menu
from the first launch. A level can show you why an option exists. It must never make you
earn one. Unlocking access as a reward inverts the argument you are making.

**2. Nothing essential by sound alone.** This rules out a level that requires hearing to
complete. You can still build an audio-rich level. Every cue just needs a visual twin.

**3. One-switch play is not a level.** It is an Advanced guideline and it is days of work.
Put it in the "what I would do next time" section instead of half-building it.

## What the game can already do

The command system is built and tested. Every command below works by voice and by key,
and nothing downstream of the input layer can tell which one you used.

| Intent | Meaning | Default key |
|---|---|---|
| Go | start or resume moving | W |
| Back | reverse | S |
| Left | turn left | A |
| Right | turn right | D |
| Stop | hold position | Space |
| Open | open a door or gate | E |
| Light | light on or off | F |
| Wake | wake another rover | Q |
| Repeat | ask the rover to say it again | R |
| Help | what can I say here | H |

Voice matching is fuzzy, so "could you open the door", "open up" and "open" all land on
Open. Adding a phrasing is one line in `IntentVocabulary.cs`.

It also refuses to act on negation. "Don't stop" produces no command rather than Stop,
because a rover doing the opposite of what you said is worse than a rover doing nothing.

If a level needs a verb that is not on this list, say so and I will add it. Adding one is
cheap. Just know that every new verb is another thing speech recognition can confuse, so
a small vocabulary is a feature.

## Per level, I need four things from you

1. **Which guideline it embodies**, quoted from the list above.
2. **What the player does**, in three or four sentences.
3. **What the barrier feels like** before the relevant setting is used.
4. **Which intents it needs**, from the table.

## A worked example, so you can argue with it

**Level: Signal**
- Guideline: "Ensure no essential information is conveyed by sounds alone"
- What happens: the rover is in a dark corridor. It pings when it reaches a junction.
  The ping is a sound and a pulse of light on the rover's shell, always both. The player
  learns the loop: speak, wait, listen, watch.
- The barrier: if you turn the sound off, you lose nothing. That is the point, and the
  level is built so a deaf player and a hearing player have exactly the same information.
- Intents: Go, Stop, Left, Right

You do not have to keep this one. It is here to show the shape.

## Decisions made

**The rover answers in text and sound, always both.** Never one alone. That satisfies
"provide subtitles for all important speech" and "no essential information conveyed by
sounds alone" in a single rule, applied everywhere, rather than remembered per level.

**The final level wakes every rover at once.** One transmission, all of them. Access
multiplying is the argument, and doing it one at a time would say the opposite.

**Two minutes a level, six levels.** Roughly twelve to fifteen minutes end to end, plus the
final broadcast.

**The ending is your own voice.** The game keeps every clip you speak. At the end you stop
commanding and start broadcasting, and what goes out on the open loop is your actual
recording, in order, unedited, waking every dormant rover. Design the last level knowing
the payoff is already written and it is made of whatever the player said along the way.

Two consequences for you. The last level does not need to earn its ending, because the
player already did that across the other five. And every level is quietly a recording
session, so a level that gets someone talking is worth more than a level that gets someone
issuing single words.

### On letting the player pick level length

Don't. Build one version of each level.

"Offer a wide choice of difficulty levels" is a real Basic guideline and we are claiming
it, but it is already served by settings that exist: game speed runs 0.25x to 1.5x, so a
player who wants eight minutes for a two-minute level simply takes them. Alternate level
variants mean building more levels, and more levels is the one thing 2.5 weeks cannot
absorb. Difficulty comes from the settings panel, not from branching content.

## The setting: mission control, and why it earns its place

You are the operator on console. The rover is on the Moon. This is not decoration, it
solves three problems at once.

**1. The delay becomes physics.** Round-trip light time to the Moon is about 2.56 seconds.
A recording from Apollo 12 measured 2.712 seconds. Speech recognition on this machine will
take roughly one to three seconds. Those are the same number, which means the wait between
speaking and the rover moving stops being lag and becomes signal delay.

Mars would not work. Its round trip is six to forty-four minutes.

**Design rule from this:** hold the delay at a *fixed* 2.6 seconds. If transcription
finishes in 1.2 seconds, wait anyway. Consistent latency reads as intentional; latency
that varies between one and three seconds reads as broken software. We get better feel by
being slower sometimes.

Show it as a signal indicator with distance travelled, not a loading spinner. A spinner
says the program is struggling. A signal readout says the Moon is far away. The wait is
identical and the feeling is not.

The README will say plainly that the delay is real transcription time wearing a costume.
That is a fair game fiction, and hiding it would not be.

**2. It gives the keyboard a real seat.** Mission control ran voice loops *and* typed
command uplinks. Both are authentic. So the keyboard is not the accessible alternative
bolted onto the real thing, it is the other console. The fiction now supports the parity
argument instead of quietly working against it.

**3. There is a real job with the right skill.** CAPCOM, the Capsule Communicator, was the
only person in Mission Control allowed to speak to the crew, and the role was traditionally
filled by an astronaut precisely because they would catch tone and phrasing that someone
else would miss. A person whose entire job is understanding *how* something was said. That
is what the matching layer does, and it is what the game is about.

### Free material this hands you

- **Quindar tones**, the beeps that keyed the Apollo transmitter, are an authentic audio
  motif. Pair each one with a light pulse on the rover and the dual-coding rule is met by
  the sound design itself.
- Comms discipline as clear language: "say again", "copy", "stand by". Short, unambiguous
  phrasing is what radio protocol is *for*, which is the "use simple clear language"
  guideline arriving free and in character.
- The final level has a mechanism now. You leave the directed channel and transmit on the
  open loop. One voice, every dormant rover hears it.

## Also decided

**The rover's call sign is Salty.** From *Madoqua saltiana*, one of the four real dik-dik
species. One clean English word, which is not a cosmetic point: you will address the rover
by name constantly, and speech recognition has to catch it every time. A call sign the game
mishears would be the project making the exact mistake it argues against. Verified in the
matcher: "Salty, go" resolves to Go, and a bare "Salty" resolves to nothing, because calling
someone is not the same as ordering them. Worth building on later, since a bare call sign
getting an acknowledgement back is exactly how radio works.

**No failure state.** Every attempt is a rehearsal run, discarded and taken again, which is
what mission control actually does before uplinking. No death screen, no counter, no
loading. Borrowed from Katana Zero, but more honest here because it is real procedure rather
than a fantasy premise. Implemented in `SimulationReset`.

The reason this is not just tone: if speech recognition mishears you and the rover drives
off a ledge, charging you for that would make the game an example of the thing it is
complaining about.

**Mission control talks back.** Moses records the supervisor, radio-filtered. Salty never
speaks in words: text on screen, tones and light. One human voice in the whole game, and it
is a human one.

## Still open

- Level designs. Six of them, one guideline each, two minutes apiece.
- Whether `sup_final_06` survives. It is the only line permitted to state the theme out
  loud, and it may be better cut once you hear it.
