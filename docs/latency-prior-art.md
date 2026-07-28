# Latency compensation: what is already known

Prior-art check run before making any claim in the README or a post. The short version:
**none of the three techniques is new as a mechanism. Two are new as applications. One
should not be claimed at all.**

A false novelty claim in a portfolio piece aimed at a research group is worse than no claim.

---

## 1. Anticipatory deceleration on speech onset

**ADJACENT.** Every component is established; the specific composition is not documented.

The rover drops to 45% speed the moment voice detection fires, about 100ms in, before any
transcription exists. It does not guess the command. It only becomes more cautious, so
being wrong costs nothing.

| Prior work | How it differs |
|---|---|
| Incremental processing (Schlangen & Skantze) | Acts on partial **content**. This acts on onset alone |
| Barge-in in IVR | The true content-free precedent: VAD halts prompt playback. But binary, and it acts on the system's own speech, not a continuous control variable |
| Speculative ASR (Amazon, Google prefetch) | Content-**based**. It guesses. This explicitly refuses to |
| Uncertainty-driven slowdown in autonomous driving | Identical mechanism, different trigger |
| Intel US9466296B2, "staging for execution" | Reversible preparatory work, but content-based |

**The citation that grounds the argument:** Loth, Jettka, Giuliani, Kopp & de Ruiter (2018),
*Confidence in uncertainty: Error cost and commitment in early speech hypotheses*, PLoS ONE
13(8): e0201516. It shows humans calibrate commitment to early speech hypotheses against
**error cost**: act early when being wrong is cheap, wait when it is expensive. That is
exactly the principle here, peer-reviewed and citable.

**Not found:** any system using content-free speech onset to modulate a *continuous* control
variable. Searched incremental dialogue, prosody-based early response, backchannel
prediction, VAD-triggered pre-emptive action, robot motion halt on speech, voice-controlled
wheelchair and UAV safety, in-vehicle speed reduction during voice interaction.

---

## 2. Lag compensation for voice commands

**ADJACENT, and the strongest claim available.**

Credit the command at speech onset rather than speech end, and correct the rover back to
where it was when the player started talking.

The mechanism is textbook. Jefferson's Virtual Time (1985) is the origin. **Mauve, Vogel,
Hilt & Effelsberg (2004)**, *Local-lag and timewarp*, IEEE Transactions on Multimedia 6(1),
is the canonical peer-reviewed statement. Valve's public write-up is the engineering
version. GGPO is the fighting-game one.

**The unclaimed corner:** Liu, Xu & Claypool (2022), *A Survey and Taxonomy of Latency
Compensation Techniques for Network Computer Games*, ACM Computing Surveys 54(11s), organises
80+ papers into 11 techniques. It covers **network latency only**. No entry for
input-modality latency. No voice or speech work anywhere in it.

So the honest framing is **transfer, not invention**: rollback compensation, solved for
network latency, applied to a latency source the games literature has not treated this way.

### Two things to confront

**FPS lag compensation never moves the visible world.** It rewinds only to *evaluate* a hit.
Correcting the rover's position visibly teleports it. That is a different and more
disruptive operation, and it is the consistency-versus-responsiveness tradeoff Mauve et al.
name directly. Expect rubber-banding, and expect it to be what kills the technique.

**The right baseline is forward prediction, not "nothing".** Robotics solves delay the
opposite way: Smith predictors and predictive displays project the world *forward* to hide
delay. Nobody there rewinds to the intent timestamp. A study that omits this comparison is
missing the obvious question.

**Techniques 1 and 2 partly cancel.** If you rewind to the onset position, the speed in
between does not matter, so compensation absorbs anticipation's benefit for `Stop` entirely.
Anticipation only earns its keep where rewinding is wrong, such as `Left` and `Right`.
**This is why the study is split by command type rather than run as a 2x2.**

---

## 3. Equalised latency across input modalities

**ESTABLISHED. Do not claim this, and the rationale is contested.**

The same 2.6s delay is applied to keyboard and voice so neither is faster.

- **Zander, Leeder & Armitage (2005)**, *Achieving fairness in multiplayer network games
  through automated latency balancing*, ACE '05. This is the same idea verbatim, with
  players in place of modalities.
- **Local lag** (Mauve et al. 2004): voluntarily reduce responsiveness to remove
  inconsistency. Twenty years old.
- Brun, Safaei & Boustead (2006), CACM, *Managing latency and fairness in networked games*.
- Claypool's taxonomy has a whole **Time Delay** group for it.

### The counter-evidence

- **Bogon et al. (CHI 2025)**: users integrate anticipated delay and slow their own actions
  before it arrives, so a uniform tax degrades the keyboard without improving voice.
- **Limerick, Moore & Coyle (CHI 2015)**: speech already produces measurably lower sense of
  agency than keyboard, and the gap is not a latency artefact, so equalising will not close
  it.
- **Game Accessibility Guidelines** argue the reverse: provide equivalent access, do not
  degrade the working path.

"Fairness" is a competitive-multiplayer concept. Single-player has no adversary.

**Decision:** keep the mechanism, drop the novelty framing, cite Zander et al., state the
actual reason (voice should not read as the lesser mode), name the counter-evidence, **and
test it**. If the data agrees with Bogon et al., publishing that a design decision was
tested and failed is worth more than the decision was.

---

## What to measure

**For compensation**, on `Stop`: signed stop-position error against player intent, three
arms, no compensation / forward prediction / rollback. Rate of visible correction artefacts
and whether players notice them.

**For anticipation**, on `Left` and `Right`, **with compensation disabled** or the effect
vanishes: positional error at command resolution, deceleration on and off. False-onset rate
from coughs, background speech and self-talk, and what each false onset costs.

**For the parity decision**: keyboard with and without the delay, measuring completion time,
error and preference.

**Sense of agency** via intentional binding, the established implicit measure from Limerick
et al.

**Reporting note:** 1.9s and 2.6s are system properties, not results. Report the latency
**distribution** for Whisper tiny on this hardware with variance, not a single number.

---

## Two papers to cite so as not to be caught missing them

- Allison, Carter, Gibbs & Smith (CHI PLAY 2018), *Design Patterns for Voice Interaction in
  Games*. 25 patterns from 449 games. The design-space paper for this domain.
- Zargham, Fetni, Spillner, Muender & Malaka (CHI 2024), *"I Know What You Mean":
  Context-Aware Recognition to Enhance Speech-Based Games*. Improves speech games by
  attacking accuracy rather than latency. A clean contrast to this angle.
