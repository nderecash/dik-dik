# Open issues

Everything hit, deferred, or decided unilaterally during the autonomous build of
Phases 2 to 5. Moses reviews this on return. Newest section at the bottom.

Status key: **OPEN** needs a decision or a fix · **DECIDED** I chose, overrule me if wrong ·
**BLOCKED** needs Moses · **FIXED** resolved during the run, listed for the record.

---

## Needs Moses

| # | Item | Why it needs you |
|---|---|---|
| M1 | itch.io page: create account and upload | Publishing as you. Build and page copy will be ready. |
| M2 | Demo video | Your voice, your screen. Shot list will be drafted. |
| M3 | whisper.unity issue and PR | The contribution should carry your name. Draft at `docs/TASK-github-contribution.md`. |
| M4 | Full playthrough, voice then keyboard | Never been done end to end. Nothing else substitutes. |

---

## Decisions taken without asking

### D1. Dropped `KeywordRecognizer` for the fast stop. **Overrule me if you disagree.**

The plan had a second recogniser running next to Whisper for four words at ~100ms.
Research killed it:

- Unity's own docs still say "functional only on Windows 10". Never updated for 11.
- Microsoft deprecated Windows speech recognition in December 2023, replaced by voice
  access on 22H2+ in September 2024.
- It throws on construction when unsupported rather than failing softly. Silent death
  on some machines.
- No authoritative source anywhere confirms it can share a microphone with Whisper, and
  whisper.unity holds your mic open for the entire session.

**Instead:** whisper.unity already ships voice detection on a 100ms tick off the same
single microphone. The rover cuts power the moment you start speaking, then obeys the
real word when it arrives. Same feel, one microphone, no deprecated dependency.

Full detail in `docs/technical-verification.md`.

### D2. Levels keep their corridors. Not reshaped to open ground.

The plan called for open ground with rock funnels. I did not do it, and I think that is
right. The cable already delivers the legibility the reshaping was meant to enable. The
corridors are proven. Opening the levels up is a large change I cannot playtest before
you are back, and the plan's own risk note said the fallback for bad open terrain is
tighter funnels. Reversible later if you want it.

### D3. Level 5 keeps the stop pads as its objective.

Its two cable checkpoints sit at 6 and 52, clear of the pads at 14, 30 and 46, and
scanning them does not complete the level. A checkpoint that stops the rover for you,
sitting on a pad, would hand the player the exact thing that level asks them to earn.

### D4. Checkpoint markers got posts.

Flat rings were invisible: the camera looks along the ground and a disc is edge-on. You
could not see where the next scan was until you were on it. Two posts either side, so a
checkpoint is a gate you can aim at.

---

## Known problems

### P1. Nobody has driven this yet.

Every verification so far is a compile, a scene-YAML read, or a screenshot. The cable,
the checkpoints, the scan hold and the recovery drive have never run. Highest-value thing
you can do on return.

### P2. The README accessibility claims are wrong. Fixed in Phase 5, flagged now.

Research checked them against the published guidelines:

- "no failure state" is not a guideline. Nearest real one is *Offer a wide choice of
  difficulty levels* (Basic).
- "voice input as an alternative control scheme" is backwards. The published guideline is
  *Ensure that speech input is not required, and included only as a supplementary /
  alternative input method*. Your keyboard parity is the compliance mechanism, not a bonus.
- README line 171 says the Speech category is empty at Basic. It is not. It has exactly
  the guideline above, which is the most on-point one in the whole set. A reviewer who
  knows the set spots that in seconds.
- The 81.2% figure has no denominator and no method. That is the clearest hobbyist tell
  in the document.

### P4. Dust, wind and tire sound are unverified.

They build at runtime, so no screenshot can show them and no scene file can prove them.
The components are present and their clips are wired, checked in the scene YAML. Whether
the dust is too busy, the wind too loud, or the tire roll irritating over twenty minutes
are all questions only playing answers. Every value is a serialized field, so they are
tunable without a rebuild.

### P5. The attention reflex needs a feel check.

The rover drops to 45% speed the moment you start talking and recovers when the command
lands. On paper it makes "stop" feel immediate without breaking the delay. In practice it
means every command you give slows the rover slightly, and that might read as sluggish
rather than attentive. The number is `attentiveSpeedFactor` on RoverController. If it
feels wrong, 0.7 is gentler and 0 makes it a true instant stop.

### P6. The cinematic has never been watched.

It only runs at play time, so no screenshot can show it. The camera keyframes are guesses
made against the level's dimensions, not against how the shot actually reads. Expect to
tune `spaceStart`, `approach`, `arrival` and `easing` on the IntroCinematic component. All
four are serialized fields on the Main Camera in Level 1.

The one structural risk: if the briefing turns out to be shorter than the camera moves,
the shot will feel rushed. The fix is fewer beats, not a longer briefing.

### P7. The blockage rock is solid and sits on the cable.

If the diagnostic conversation somehow fails to start, the rover is stopped by a rock with
no way past. Three things have to fail at once for that, and the recovery drive would
eventually take over, but it is the one place in the game where a player could be properly
stuck. Worth watching for in levels 1 and 3.

### P3. WebGL. FIXED, but read this before you touch the package.

The browser build works: 18.9 MB, 0 errors, 0 warnings, zip ready at
`Builds/dikdik-web.zip`.

Getting there changed something structural that you should know about. **whisper.unity is
no longer a git dependency. It is embedded at `Packages/com.whisper.unity`.**

It had to be. Its asmdef declared empty include and exclude platform lists, which Unity
reads as "every platform", so its DllImports were compiled into the WebGL build with no
library to bind to, and the build died at Emscripten link time. PackageCache is immutable,
so the asmdef could not be edited in place.

Consequences:
- The package is now yours to maintain. It will not update itself.
- Its own test folder is deleted. Embedded packages compile their tests; cached ones do
  not, and those tests need NUnit references the project does not have.
- It contributes **one warning** to every Windows build, `CS4014` in its `FileUtils.cs`.
  That is vendor code and I did not edit it. So the clean-build signal is now "0 errors,
  1 known vendor warning", not zero.
- The version is pinned at commit `de9ce92ad4c5`, which is what you were already on.

### P9. The repo is now 118 MB, and I left it that way on purpose.

Embedding whisper.unity added 77 MB of native libraries, including 25 MB of Linux Vulkan
and 20 MB of Android static libs for platforms this project will never target. Nothing
exceeds GitHub's 100 MB per-file limit and the push went through.

I did not trim them, for one reason: the blobs are already in git history, so deleting the
files now would shrink the working tree without shrinking a clone. Actually fixing it means
rewriting history, and rewriting history on a repo you might have cloned somewhere else is
not something to do while you are away.

If you want it smaller, the move is `git filter-repo` on
`Packages/com.whisper.unity/Plugins/{Android,iOS,macOS,Linux}` plus a force push, done
deliberately and with a backup. Recoverable either way: the package is pinned at commit
`de9ce92ad4c5` upstream.

### P8. Nothing has been uploaded anywhere.

The web zip and the Windows build both exist and are correct. No itch.io page has been
created, nothing has been uploaded, and no video has been recorded. Page copy is in
`docs/itch-page.md`, video shot list in `docs/demo-video.md`.
