# Dik-dik: your Day 3 checklist

Total: about 30 minutes. Do it in one sitting, in a normal room, at a normal volume.
Everything is batched so you are not called back repeatedly.

I have not asked you to score anything. The spike writes its own results to a file and
I read them. Your job is only to talk.

---

## Before you start: 5 minutes

- [ ] **Close anything using the microphone.** Teams, Zoom, Discord, a browser tab on a
      call. Windows will hand the mic to the first thing that grabbed it.
- [ ] **Check Windows can hear you.** Settings, System, Sound, Input. Speak and watch the
      bar move. If it does not move, nothing below will work and I need to know now.
- [ ] Do **not** put on a headset if you would not normally wear one while playing. I want
      the microphone you would actually use, not the best one you own.

---

## The run: 15 minutes

- [ ] Launch `DikdikSpike.exe`. I will give you the exact path when the build lands.
- [ ] It shows you one instruction at a time, fifteen in total. Read it, then say the
      command however you would naturally say it.

**Three rules, and the third is the important one:**

1. Speak normally. Normal pace, normal volume, normal accent. Do not enunciate for the
   machine.
2. Do not use the words on screen. The instruction says "there is a door ahead, deal with
   it", not "say open". Say whatever you would actually say.
3. **Do not help it.** If it mishears you, do not slow down, do not repeat more clearly,
   do not simplify. A misheard result is the result I need. If you unconsciously adapt to
   the machine, we will ship a game that only works for people who do that, which is the
   exact failure this project is about.

- [ ] When it finishes it shows a percentage and a file path. **Tell me the percentage and
      paste the path.** That is all I need.

---

## If something goes wrong

Do not debug it. Just tell me which of these happened:

- Window opens then closes immediately
- Window opens but "I heard" never changes from "(nothing yet)"
- It hears you but the words are wrong
- It hears you correctly but picks the wrong command
- It takes so long you lose patience

Those five have completely different causes and I can tell them apart from the log. The
fourth one is my bug, not the model's.

---

## Then, only if I ask: 10 minutes

If the first number is poor I will send a second build using a different speech model.
Same fifteen instructions, same rules. This is the escalation path, not a retry: tiny to
tiny.en to base.en. Bigger models handle accents better, and I would rather find that out
than blame you or blame the microphone.

---

## What happens with the number

The gate is 80 percent landing on the right command.

**At or above 80:** Whisper stays and I build the game on it.

**Below 80:** I try the larger models first. If those do not clear it either, I switch to
Windows keyword recognition, which is offline, near-instant and reliable, and the game
loses nothing except one talking point. That fallback is written down and ready, so a bad
number costs us a day, not a week.

**Either way it goes in the README.** If speech recognition works worse on your voice than
on an American one, that is not a broken project and it is not something to tune away
quietly. Published research finds Whisper does markedly worse on African-accented English,
and the small models we can afford to run are the worst offenders. A game about being
heard, whose own speech recognition half-hears its maker, is a stronger and more honest
piece of work than one that hides it.

So there is no bad outcome here. There is only a result I have not written down yet.
