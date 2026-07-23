# Dik-dik: voice script

Lines for the supervisor on the loop. These are the systemic ones, needed regardless of how
the levels turn out. Level-specific dialogue comes after you have designed the levels.

**Two speakers, one recorded.**

- **Supervisor** (you, recorded, radio-filtered). The other human on the console.
- **Salty**, the rover. Never speaks in words. It answers in text on screen, tones and
  light. Cheaper, more in character, and it satisfies subtitles plus dual-coding by design
  rather than by discipline.

The call sign is **Salty**, from *Madoqua saltiana*, one of the four real dik-dik species.
One clean English word, so speech recognition catches it every time. That matters more than
it sounds: you will address the rover by name constantly, and a call sign the game mishears
would be the project making the exact mistake it is arguing against.

Verified in the matcher: "Salty, go" resolves to Go, "okay Salty, stop" resolves to Stop,
and a bare "Salty" resolves to nothing at all, because calling someone is not ordering them.

**Delivery for all of it:** calm, brief, slightly bored. A flight controller has done this
a thousand times. The dry lines are funny because nobody performs them. If a line sounds
like a joke when you read it aloud, underplay it further.

**File naming:** one line per file, named as marked. `sup_boot_01.wav` and so on.

---

## 1. Boot and handshake — `sup_boot_*`

Played once, at first launch. This is the whole tutorial.

| File | Line | Note |
|---|---|---|
| `sup_boot_01` | "Console is yours. Salty is on the surface and waiting." | |
| `sup_boot_02` | "It will not move on its own. It moves when you tell it to." | The premise, said plainly, once |
| `sup_boot_03` | "Talk to it, or type to it. Same loop either way, it cannot tell the difference." | Parity stated out loud, early |
| `sup_boot_04` | "Round trip is about two and a half seconds. That is the Moon, not you." | Pre-empts the player blaming themselves for latency |
| `sup_boot_05` | "Everything you need is in settings. It is all on already. Change what helps." | Settings are never earned |

---

## 2. Acknowledgements — `sup_ack_*`

**Do not play one of these after every command.** Constant chatter is exhausting. Use them
sparingly: first success in a level, and occasionally after a long silence. The rover's own
light and text carry the routine acknowledgements.

| File | Line |
|---|---|
| `sup_ack_01` | "Copy." |
| `sup_ack_02` | "Salty has it." |
| `sup_ack_03` | "Good."  |
| `sup_ack_04` | "That worked." |
| `sup_ack_05` | "It heard you." |

---

## 3. Not understood — `sup_miss_*`

The most important lines in the script. **Never blame the player.** The failure is the
listening, not the speaking. Read these flat and slightly apologetic on the machine's
behalf.

| File | Line |
|---|---|
| `sup_miss_01` | "It did not catch that. Try it another way." |
| `sup_miss_02` | "Nothing came through. Say it however you like, it is not fussy." |
| `sup_miss_03` | "That did not land. Not your fault." |
| `sup_miss_04` | "Signal is fine. The understanding is the hard part." |
| `sup_miss_05` | "Again, when you are ready. No rush on this end." |

If recognition turns out to be poor on your voice, `sup_miss_04` becomes the most honest
line in the game.

---

## 4. Blocked — `sup_block_*`

The rover stopped because something is in the way.

| File | Line |
|---|---|
| `sup_block_01` | "It has stopped. Something ahead of it." |
| `sup_block_02` | "That is a wall. It will wait." |
| `sup_block_03` | "Held position. Give it another heading." |

---

## 5. Simulation reset — `sup_reset_*`

There is no failure here. This is a rehearsal run being discarded, which is what mission
control actually does before uplinking. **No line may imply the player did badly.**

| File | Line |
|---|---|
| `sup_reset_01` | "Sim aborted. Resetting." |
| `sup_reset_02` | "That is not how that goes. Take two." |
| `sup_reset_03` | "Run it back." |
| `sup_reset_04` | "Noted. Again from the top." |
| `sup_reset_05` | "Good. Now we know. Again." |
| `sup_reset_06` | "This is why we rehearse." |

These cycle in order rather than at random. Random repeats itself in a way players notice
and read as the game not listening.

---

## 6. Level complete — `sup_done_*`

| File | Line |
|---|---|
| `sup_done_01` | "That is the one. Logging it." |
| `sup_done_02` | "Clean run. Uplinking." |
| `sup_done_03` | "Salty is through. Next sector." |

---

## 7. Long silence — `sup_idle_*`

If the player says nothing for a while. Warm, never nagging. They may be thinking, or away,
or working out how to phrase something.

| File | Line |
|---|---|
| `sup_idle_01` | "Still here." |
| `sup_idle_02` | "Salty is holding. It does not mind waiting." |
| `sup_idle_03` | "Take your time. It has nowhere to be." |

---

## 8. The final level — `sup_final_*`

The other rovers wake all at once. This is the argument landing, so it is the only place in
the script where the supervisor is allowed to drop the flat delivery, and even here only
slightly.

| File | Line | Note |
|---|---|---|
| `sup_final_01` | "There are others out there. Dormant. Same build, same problem." | |
| `sup_final_02` | "You are on the open loop now. Everything hears you." | The mechanism |
| `sup_final_03` | "Say it once." | Beat before the player speaks |
| `sup_final_04` | "…" | Silence, held, while they wake |
| `sup_final_05` | "All of them. One transmission." | Understate this. It carries itself |
| `sup_final_06` | "That is the whole thing, really. Build it in once and it reaches everyone." | The only line permitted to say the theme |

`sup_final_06` is the line I would cut first if it feels heavy-handed when you hear it. The
game may say it better without saying it.

---

## Recording order

Record §3 and §5 first, while you are fresh. They are the ones players hear most, and they
are the ones that carry the argument. §1 last, since boot lines only play once.

Roughly 40 lines. About twenty minutes with retakes.
