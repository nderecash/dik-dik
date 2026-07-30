# Dik-dik: voice script v2

**Read the story section first and push back on it before you record anything.** I filled
gaps your notes left open, and every invention is marked ⚑ so you can overrule it.

Recording technique has not changed: [TASK-voice-recording.md](notes/TASK-voice-recording.md).
The tools work now. Read straight through, two seconds between lines, end each line
crisply.

---

# PART 1 — THE STORY

## The situation

A small crew is on the surface of an unnamed planet. ⚑ *Unnamed on purpose — nobody says
where this is, which keeps it from being a Moon story or a Mars story.*

Their shuttle is in cooldown and cannot lift until it recharges. It is not recharging,
because the **relay line** that carries power and data from the ground station to the pad
is faulted somewhere along its length. ⚑ *This is my invention. Your notes had the
shuttle on cooldown and a cable to follow but did not connect them. Tying the two together
is what gives scanning a reason and makes the last checkpoint matter.*

They have a rover out there that was running automated tasks. Their automated control is
down along with everything else. **The player's uplink is the only one still live.**

So the player guides the rover along the relay line, scanning each section, narrowing down
where the break is. Every section reads clean — until the last one, where the fault is
found and the rover repairs it. Power returns. The shuttle can charge. The crew goes home.

## Why this shape works

- **Scanning has a point.** You are not collecting samples for the sake of it, you are
  eliminating sections to find a break.
- **"Clean" is progress, not filler.** Each clear scan narrows the search.
- **The cable is the map.** You follow it because it is the thing you are inspecting. No
  minimap needed, no arbitrary waypoints.
- **The ending is earned and small.** No grand gesture. A repair, power coming back, and
  people who can now leave.
- **There is urgency without a timer.** The crew is stuck. Nothing counts down.

## What this replaces

The old ending — broadcasting your own recorded voice to wake dormant rovers — is **cut**,
per your note that hearing yourself played back felt like an invasion of privacy because
it caught the room and not just your commands.

That goes further than the ending. The game will now **discard audio the moment it has
been transcribed**. Nothing retained, nothing stored, nothing played back. It is a
stronger position than the one the README currently claims, and the honest version of how
we got there — built it, played it, it felt like surveillance, removed it — is worth
writing down.

## The cast

**Control** — the voice on the loop. One of the stranded crew. ⚑ *I have written them as
dry, competent, quite tired, and warming up as the mission goes. Not a chirpy mission-
control cliché. They are stuck on a rock and mildly embarrassed to need help.* This is you.

**Salty** — the rover. Never speaks in words. Answers in light, tone and on-screen text.
Named from *Madoqua saltiana*, a dik-dik species.

**The station system** — synthetic, reads procedures in jargon. Windows text-to-speech,
not you. Appears in the clear-language section.

## The one rule for humour

**Never at the player's expense.** The jokes are at the situation, the equipment, or
Control themselves. Control is never exasperated *with you*, even when you have left the
room for ten minutes.

---

# PART 2 — TONE GLOSSARY

I use these words in the tone column. This is what I mean by each.

| Word | What I mean |
|---|---|
| **Flat** | No colour. Information only. Reading out a number. |
| **Dry** | Flat with amusement underneath. Not played for a laugh. |
| **Deadpan** | Funny *because* you refuse to play it as funny. |
| **Warm** | Friendly, no irony, no edge. |
| **Wry** | A small self-aware smile. Usually at their own expense. |
| **Tired** | End of a long shift. Slightly slower. |
| **Clipped** | Short, businesslike, a bit faster. Something is happening. |
| **Relieved** | Breath out. |
| **Sarcastic** | Pointed — but always at the equipment or the situation, never you. |

You flagged that the first pass had humour where neutral was needed and neutral where it
needed edge. The tone column is now explicit for every line. Where I have marked **Flat**,
please resist making it fun — those are the lines that repeat dozens of times, and warmth
wears out fast on repetition.

---

# PART 3 — WHAT SURVIVES FROM THE FIRST RECORDING

**23 lines are still good. Do not re-record these.**

| Group | Lines | Why they survive |
|---|---|---|
| `sup_ack_01–05` | Copy / Salty has it / Good / That worked / It heard you | Narrative-neutral |
| `sup_miss_01–05` | All five not-understood lines | Narrative-neutral, and still the best writing in the script |
| `sup_block_01–03` | It has stopped / That is a wall / Held position | Still true |
| `sup_done_01–03` | That is the one / Clean run / Salty is through | Still true |
| `sup_plain_01–04` | The four jargon translations | Section unchanged |
| `sup_console_01–03` | The stuck-control lines | Section unchanged |

**18 lines are dead:**

- `sup_boot_01–05` — replaced by the new opening
- `sup_final_01–04` — the waking-rovers ending is gone
- `sup_idle_01–03` — you want funnier ones
- `sup_reset_01–06` — **these need re-recording and it is worth knowing why.** They were
  written as "Sim aborted", framing every attempt as a rehearsal being discarded. In the
  new fiction the rover is really out there on a real planet, so "simulation" no longer
  makes sense. Same forgiving idea, new frame: the rover has a **safety cutout** that backs
  it away from trouble. Nothing is ever lost, it just reverses and waits.

---

# PART 4 — THE LINES

**Priority A ≈ 12 min · B ≈ 8 min · C ≈ 6 min.** Record A and B and the game is fully
voiced. C is delight and flavour — record it if you still have voice left.

---

## A1 · The opening — `sup_open_*` (9 lines)

**Context:** plays once, over the opening cinematic — stars, then the planet, then descent
into the first shot. **Nothing is listening yet.** The player cannot speak until you
finish, and the last line is what hands them control. This is the whole tutorial, and it
is the first thing anyone hears, so it matters more than the rest combined.

| # | Line | Tone |
|---|---|---|
| `sup_open_01` | Oh — there you are. Line's up. | **Relieved.** Genuine. You have been trying this for an hour. |
| `sup_open_02` | Give me a second, I've been at that a while. | **Tired.** Talking to yourself as much as them. |
| `sup_open_03` | Right. We've run into a problem, and you're the only one who can help with it. | **Clipped.** Down to business. Not dramatic. |
| `sup_open_04` | We're on the surface. The shuttle's in cooldown and we can't lift until it charges. | **Flat.** Just facts. |
| `sup_open_05` | It isn't charging. Something's gone wrong with the relay line between us and the ground station. | **Flat**, with the first hint of worry on "isn't". |
| `sup_open_06` | We've got a rover out there. Our automated control went down with everything else. | **Flat.** |
| `sup_open_07` | Your uplink is the only one still live. So it has to be you. | **Warm.** This is the line that makes it personal. Small pause before "So it has to be you." |
| `sup_open_08` | Take it along the relay line and scan each section until we find the break. | **Clipped.** The mission, stated once, plainly. |
| `sup_open_09` | Console's open — just talk to it. Plain speech is fine. Go left, stop, whatever comes out. It's not fussy. Try something. | **Warm**, ending on an invitation. This is where the player takes over, so land "Try something" gently and then stop. |

---

## A2 · Scan reports — `sup_scan_*` (5 lines)

**Context:** plays every time the rover finishes scanning a checkpoint and the section is
clean. This happens roughly **twenty times across the game**, cycling in order.

**These are the most-repeated lines in the game. Keep them short and genuinely flat.** A
joke here is funny once and irritating the fourth time. The gradual creep of impatience
across the five is the only colour, and it should be very slight — it builds toward the
moment you finally find the fault.

| # | Line | Tone |
|---|---|---|
| `sup_scan_01` | Section reads clean. Move it on. | **Flat.** |
| `sup_scan_02` | Nothing wrong there. Next one. | **Flat.** |
| `sup_scan_03` | That length's fine. Keep going. | **Flat.** |
| `sup_scan_04` | Clean. Logging it. | **Flat**, slightly faster — routine now. |
| `sup_scan_05` | No fault there either. | **Flat** with the faintest edge of "where *is* it". Do not push this. |

---

## A3 · Finding the fault — `sup_fault_*` (3 lines)

**Context:** the last checkpoint of the last level. After twenty clean scans, this one
is not clean. First real change in Control's voice all game.

| # | Line | Tone |
|---|---|---|
| `sup_fault_01` | Hold on. That's not clean. | **Clipped.** Sitting up straight. |
| `sup_fault_02` | That's it. That's our break, right there. | **Clipped**, rising. Let some relief in. |
| `sup_fault_03` | Get me a closer look at it. | **Clipped.** An instruction, not a request. |

---

## A4 · The repair and the end — `sup_fix_*` (4 lines)

**Context:** the player tells the rover to repair. `sup_fix_02` plays *during* the repair
animation with nothing else happening — it is the held breath.

| # | Line | Tone |
|---|---|---|
| `sup_fix_01` | Go on then. Patch it. | **Dry.** Trying not to sound hopeful. |
| `sup_fix_02` | Come on. Come on. | **Quiet**, almost under your breath. Not to the player — to the machine. |
| `sup_fix_03` | That's power. We've got power. | **Relieved**, building. Do not shout the first one. |
| `sup_fix_04` | You did it. We're going home. | **Warm**, and let it be sincere. This is the last thing anyone hears. No irony. |

---

## A5 · Safety cutout — `sup_cut_*` (4 lines)

**Context:** replaces the old "Sim aborted" lines. Plays when the rover hits a hazard and
backs itself away. Cycles in order.

**Nothing here may imply the player did badly.** The cutout is the rover's own safety
system doing its job. It costs a little time and nothing else. There is no failure in this
game and these lines are where that promise is either kept or broken.

| # | Line | Tone |
|---|---|---|
| `sup_cut_01` | Safety cutout. It's backing off. | **Flat.** Routine. |
| `sup_cut_02` | Caught the edge there. Pulling it back. | **Flat.** |
| `sup_cut_03` | Cutout again. It's fine, it does that. | **Dry**, reassuring. The "it's fine" is the important half. |
| `sup_cut_04` | Backed up and holding. New heading when you're ready. | **Warm.** Handing control back, no hurry. |

---

## A6 · Stuck and override — `sup_stuck_*` (4 lines)

**Context:** your three-strike escalation. Fires when the rover jams against terrain
repeatedly. On the third, Control takes manual override and drives it back to the cable
itself — and **any command from the player instantly takes control back**.

| # | Line | Tone |
|---|---|---|
| `sup_stuck_01` | I think it's stuck. It'll need a new bearing. | **Flat.** First time, just reporting. |
| `sup_stuck_02` | Still wedged. Are you giving it directions, or are we waiting for the automated override? | **Dry**, edging toward **sarcastic** — but at the *situation*, not the player. Think "we've both been here a while". |
| `sup_stuck_03` | Right. It's recalculated to the nearest checkpoint. I'm taking it there myself. | **Clipped.** Slightly apologetic about taking over. |
| `sup_stuck_04` | It's back on the line. All yours. | **Warm.** Giving it straight back. |

---

## B1 · Idle — `sup_idle_*` (8 lines)

**Context:** plays when the rover has been sitting still with no command. First after ~40
seconds, and **each one pushes the next further out**, so a player who wanders off is not
nagged. Cycles in order, so write them as if they happen over a long stretch.

These are yours more than mine — make them your own. The brief: easy-going, a bit funny,
occasionally a pointless fact. Control is not chasing the player, they are keeping
themselves company.

| # | Line | Tone |
|---|---|---|
| `sup_idle_01` | Still there? | **Warm.** Genuine question, not a prompt. |
| `sup_idle_02` | No rush. You off making coffee? | **Dry.** |
| `sup_idle_03` | Take your time. It's not going anywhere. Neither are we, which is rather the problem. | **Wry.** The joke is on themselves. |
| `sup_idle_04` | The day here is nineteen hours. Nobody's slept properly in weeks. | **Tired**, conversational. A fact offered to fill silence. |
| `sup_idle_05` | I'm not rushing you. I'm just… aware of you not being there. | **Wry**, slightly awkward. Let the pause sit. |
| `sup_idle_06` | Rover's fine, by the way. Bored, if it could be. | **Dry.** |
| `sup_idle_07` | If you've gone, that's alright. I'll be here. Obviously. | **Deadpan.** The "obviously" is the whole joke. Do not smile it. |
| `sup_idle_08` | I'm going to drop the line and save some power. Say anything when you're back and I'll pick up. | **Warm.** After ten minutes idle. Not a punishment — reassurance that you can come back. |

> Note for me, not you: `sup_idle_08` cuts the audio, so the screen must show a visible
> "line dropped, speak to reconnect" state. A player returning to a silent game must not
> think it crashed.

---

## B2 · The obstacle puzzle — `sup_puzzle_*` (4 lines)

**Context:** something blocks the route. The rover scans it, reports, and offers two or
three ways through. The player says which. **There are no wrong answers** — a different
choice just takes a different amount of time. Options are spoken *and* shown on screen.

The rover's own findings are text and tone. These four are Control's part.

| # | Line | Tone |
|---|---|---|
| `sup_puzzle_01` | It's scanning the blockage. Give it a second. | **Flat.** |
| `sup_puzzle_02` | Right — it reckons it can cut through it, dissolve it, or just shove it. Your call. | **Flat**, laying out options clearly. This is functional dialogue, do not decorate it. |
| `sup_puzzle_03` | Any of them work. It's just a question of how long you want to stand here. | **Dry.** Removes the fear of choosing wrong, which is the point. |
| `sup_puzzle_04` | That's it through. Carry on. | **Flat.** |

---

## C1 · Sector framing — `sup_sector_*` (6 lines)

**Context:** one per level, on arrival, telling the player what this stretch is. Optional
but it is what makes six levels feel like one journey rather than six rooms.

| # | Line | Tone |
|---|---|---|
| `sup_sector_01` | This is the first stretch, out past the old survey post. Should be straightforward. | **Flat.** |
| `sup_sector_02` | Next section runs through the station yard. The automated system will be talking. Ignore most of it. | **Dry.** Set-up for the jargon voice. |
| `sup_sector_03` | This length is on the night side. You'll want the contrast setting — it's in your console, always has been. | **Flat.** Practical. |
| `sup_sector_04` | Careful here, we've had a fault on your console reported. Not your doing. | **Flat**, reassuring on the second half. |
| `sup_sector_05` | This part runs downhill. It'll carry further than you expect when you stop it. | **Flat.** A warning, not a threat. |
| `sup_sector_06` | Last stretch. If the break isn't here, I'm out of ideas. | **Tired**, with real weight. Sets up the find. |

---

## C2 · Delight — `sup_fun_*` (5 lines)

**Context:** the easter eggs. The game never asks for these and never needs them — but in
a game where you can say anything, someone will try. These are the reward for playing with
it. **This is the most on-theme thing in the script:** the whole premise is a machine that
responds to natural speech, so rewarding playful speech *is* the argument.

| # | Line | Tone |
|---|---|---|
| `sup_fun_01` | …It doesn't jump. Six wheels, no legs. It appreciated the thought though. | **Deadpan.** Beat before the line, as if checking. |
| `sup_fun_02` | Now that it can actually do. After a fashion. | **Dry.** For "dance" or "spin". |
| `sup_fun_03` | Hello to you too. It can't hear tone, but I can. | **Warm.** Quietly the nicest line in the game. |
| `sup_fun_04` | Please don't encourage it. | **Deadpan.** For "sing". |
| `sup_fun_05` | Nobody important. Just the one who couldn't fix this on their own. | **Wry**, a little exposed. For "who are you". |

---

# PART 5 — WHAT I NEED BACK FROM YOU

1. **Push back on the story** — especially the two ⚑ inventions: the relay-line-causes-the-
   cooldown link, and Control's tired/dry characterisation.
2. **Rewrite any line that does not sound like you.** These are written to be said out
   loud by you, not by me. If a phrase feels borrowed, change it and tell me so the
   captions match.
3. **Then record.** A and B first. C if you still have voice.

Filenames stay the convention the splitter expects: `sup_<group>_<nn>`. I will generate the
matching `clip-names.txt` once you have confirmed which sections you are recording, so the
count lines up exactly and we do not repeat the off-by-one from last time.
