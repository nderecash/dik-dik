# Playtest checklist

Type your comments on the blank line under each item. Anything you write is useful,
including "fine" and including "I don't know what I was looking at".

**Time:** about 50 minutes for the whole thing. If you only have 10, do section 2 and
section 9 and stop.

**Build:** `C:\dev\dik-dik\Builds\Playtest\Dikdik.exe`

**Risk key:** 🔴 never run once, expect breakage · 🟠 works but the feel is a guess ·
🟢 verified some other way, just confirming

---

## 0. Before you start

- [ ] Speakers or headphones on. Half of this is audio.
- [ ] Somewhere to note timestamps if something happens too fast to describe.
- [ ] Play the first run **voice only**. Do not touch the keyboard until section 8.

---

## 1. 🔴 The opening cinematic

Never watched by anyone. The camera positions are guesses made against level dimensions,
not against how the shot reads.

- [ ] Does it start on stars, then show a planet?
_______________________________________________

- [ ] Does the camera come down and end behind the rover without a visible jump or snap?
_______________________________________________

- [ ] Do the camera moves land roughly with what Control is saying, or does it feel out of step?
_______________________________________________

- [ ] Do yellow corner brackets appear around the rover and around the cable when they are named?
_______________________________________________

- [ ] Press ESC partway through. Does the briefing skip cleanly and the camera end up in the right place?
_______________________________________________

- [ ] Overall: does it feel like an opening, or like something to sit through?
_______________________________________________

---

## 2. 🔴 The core loop, in sector one

This is the most important section. Everything else is decoration on top of it.

- [ ] Say "go". Does the rover move, roughly 2.5 seconds later?
_______________________________________________

- [ ] Does the comms panel show your words, then a filling bar, then what Salty did?
_______________________________________________

- [ ] Follow the blue cable. Is it easy to see and easy to follow?
_______________________________________________

- [ ] Can you see the checkpoint gates (two posts) before you reach them?
_______________________________________________

- [ ] Drive into a checkpoint. Does the rover **stop itself**?
_______________________________________________

- [ ] Does the lamp go orange, a bar sweep across, and a tone play?
_______________________________________________

- [ ] Does Control report the section clean?
_______________________________________________

- [ ] Does the cable behind you light up brighter?
_______________________________________________

- [ ] Does the mission panel count go up (Sections scanned 1 / 3)?
_______________________________________________

- [ ] **Say "go" while the scan is still running.** Does the panel say it is waiting, and then obey when the scan ends?
_______________________________________________

---

## 3. 🔴 The diagnostic conversation

Sector one, roughly halfway. A big rock on the cable.

- [ ] Does the rover stop and scan it before you reach it?
_______________________________________________

- [ ] Does Control offer three options, and do they also appear on screen?
_______________________________________________

- [ ] Say one of them. Does the rock sink into the ground?
_______________________________________________

- [ ] Try saying something unrelated first. Does it wait politely rather than scolding you?
_______________________________________________

- [ ] Did being asked feel like anything, or did it just feel like another step?
_______________________________________________

---

## 4. 🟠 Level transitions

- [ ] At the end of sector one, does a prompt appear asking you to say "go" when ready?
_______________________________________________

- [ ] Does saying "go" load sector two?
_______________________________________________

- [ ] Does each new sector open with Control saying something specific about that stretch?
_______________________________________________

- [ ] Does it chain all the way 1 → 6 without you touching the settings menu?
_______________________________________________

---

## 5. 🟠 Per sector, quick notes

Just note anything that looks wrong. One line each is plenty.

- [ ] **Sector 1**, dust corridor
_______________________________________________

- [ ] **Sector 2**, station yard. The automated voice should sound different from Control, then Control translates it.
_______________________________________________

- [ ] **Sector 3**, night side. The cable should be the only thing you can follow. Try it with high contrast off, then on.
_______________________________________________

- [ ] **Sector 4**, jammed key. On voice this should just work. The door should slide **down** into the floor, not up through the camera.
_______________________________________________

- [ ] **Sector 5**, the slope. The rover should coast after "stop". Brake light red while it coasts. Try the game speed slider here.
_______________________________________________

- [ ] **Sector 6**, the last stretch. The final checkpoint is the fault.
_______________________________________________

---

## 6. 🔴 The ending

- [ ] At the last checkpoint, does the lamp go **red** and Control say it found the break?
_______________________________________________

- [ ] Does a prompt ask you to tell Salty to patch it?
_______________________________________________

- [ ] Say anything. Does the whole cable light up end to end?
_______________________________________________

- [ ] Does Control say the power is back and that you are going home?
_______________________________________________

- [ ] Did the ending land, or does it need more?
_______________________________________________

---

## 7. 🔴 Deliberately break it

Do these on purpose. This is where I expect problems.

- [ ] Drive away from the cable into open ground. Does the panel warn you how far off you are?
_______________________________________________

- [ ] Keep driving to the edge of the map. Do you hit hills and stop, or drive forever?
_______________________________________________

- [ ] Get properly wedged against something. Does Control warn you twice, then take over and drive you back?
_______________________________________________

- [ ] While Control is driving, say anything. Does it hand control back straight away?
_______________________________________________

- [ ] Say "turn right" three times fast. Does the rover turn **once**, and does the panel say it replaced the earlier one?
_______________________________________________

- [ ] Say "left" then immediately "right". Does it only turn right?
_______________________________________________

- [ ] Press ESC mid-drive. Does absolutely everything stop, including the sending bar?
_______________________________________________

- [ ] Say nothing for a minute. Does Control get funny about it? Does the panel say Idle?
_______________________________________________

---

## 8. 🟢 Keyboard only

Now play sector one again with the microphone off. Settings, turn off "Listen to my voice".

- [ ] Does every command work on the keyboard?
_______________________________________________

- [ ] **Does it feel slower or faster than voice?** It should feel the same. If the keyboard feels snappier, the parity is broken and that matters more than any bug here.
_______________________________________________

- [ ] Does the panel show key presses the same way it showed your words?
_______________________________________________

---

## 9. 🟠 The judgement calls I need you on

These are not bugs. They are decisions I made without you and cannot check myself.

- [ ] **The rover slows to 45% the moment you start talking**, then obeys when the words land. Does that read as attentive, or as sluggish? This is the one I am least sure about.
_______________________________________________

- [ ] **Ambient wind and drifting dust.** Too much, too little, or right?
_______________________________________________

- [ ] **Tire sound.** Irritating after twenty minutes?
_______________________________________________

- [ ] **The scan takes 1.4 seconds** and you do about 19 of them. Too slow across a whole playthrough?
_______________________________________________

- [ ] **Levels are still corridors.** The plan said open ground with rock funnels. I kept the corridors because the cable already gives you the legibility, and I could not playtest open terrain. Do they feel too tight now that there is a cable to follow?
_______________________________________________

- [ ] **Easter eggs.** Try "can you jump", "spin", "dance", "hello", "who are you". Do the replies land?
_______________________________________________

- [ ] **Voice volume against world volume.** Can you hear Control clearly over the tires and wind?
_______________________________________________

---

## 10. Settings

- [ ] Game speed slider does something obvious on sector 5.
_______________________________________________

- [ ] High contrast makes a real difference on sector 3.
_______________________________________________

- [ ] Text size affects the settings screen itself while you drag it.
_______________________________________________

- [ ] World volume slider works and does not touch the voices.
_______________________________________________

- [ ] Quit works and does not need two tries to understand.
_______________________________________________

---

## 11. Anything else

Write anything here. Things that annoyed you, things you expected that were not there,
moments where you did not know what to do.

_______________________________________________
_______________________________________________
_______________________________________________
_______________________________________________
_______________________________________________

---

## What happens after this

1. You hand this back with comments.
2. I fix what is broken, starting with anything in sections 2 and 7.
3. Then tests: the matcher suite exists, the rest does not. Play-mode tests for the
   command bus, the cable maths and the scan hold.
4. Then the refactor pass. There is real duplication to remove: six scene builders with
   near-identical `BuildRover` methods, and six copies of `SetRef`.
5. Then ship.

**Milestones as they stand:** all five build phases are done. The gate between here and
shipping is this checklist, and nothing else.
