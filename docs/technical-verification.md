# Technical verification: dik-dik assumptions checked against sources

This is the record of a prior-art and feasibility check on the dik-dik project's technical
assumptions: what was verified, what came back refuted, and what I changed as a result. It is
for anyone reading this repository who wants to know which claims in the plan survived contact
with the documentation.

Four assumptions came back refuted. Three of them are load-bearing.

---

## Refuted

| # | Assumption in the plan | Reality |
|---|---|---|
| 1 | `KeywordRecognizer` gives a reliable ~100ms hotword path on Windows 11 | Unity's own doc still says: "Keyword recognizer is currently functional only on Windows 10." Microsoft deprecated Windows speech recognition (Dec 2023 entry, handover on 22H2+ in Sept 2024). Zero tested evidence for Win11. Fails as a thrown `UnityException` on unsupported machines, not a graceful false. |
| 2 | The project is URP | It is Built-In Render Pipeline. `ProjectSettings/GraphicsSettings.asset` line 49 has no custom RP, manifest has no URP package, `GradientSky.shader` is CGPROGRAM/UnityCG.cginc. **Every line of URP camera-stacking code would be dead.** |
| 3 | `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` or `ENABLE_WINMD_SUPPORT` guards the speech code | Both wrong. The first compiles speech out of the Editor, so it cannot be tested in Play mode. The second is the WinRT/UWP metadata flag, unrelated to desktop `UnityEngine.Windows.Speech`. |
| 4 | README line 171: the Speech category is empty at Basic level | False. It has exactly one Basic guideline, and it is the most on-point one in the whole set: *Ensure that speech input is not required, and included only as a supplementary / alternative input method*. A reviewer who knows the set spots this in seconds and discounts the rest of the document. |

Also stale: whisper.unity's README lists WebGL as supported. Its own issue #20 has sat unanswered
since April 2023 and the package ships no WebGL binaries. Web is unsupported.

---

## 1. Voice recognition path

**Confirmed:** `KeywordRecognizer` exists in Unity 6.x, is not `[Obsolete]`, and needs nothing in
`manifest.json`. Everything after that is unsupported. Whether it can share the mic with
`UnityEngine.Microphone` is answered by no authoritative source anywhere. `MicrophoneRecord.cs:262`
holds the device for the whole session, so if the conflict exists it bites every time.

**What I decided:**
- Drop `KeywordRecognizer` from the critical path entirely. Instant-STOP does not depend on it.
- One `Microphone.Start`, ever. `MicrophoneRecord.OnVadChanged` becomes the instant gate (VAD ticks
  at `vadUpdateRateSec = 0.1f`). On onset, cut throttle and arm the stop before the word is known.
  That is defensible interaction design, not a workaround.
- Confirm with a second `WhisperManager` on tiny.en over a ~1s slice: `SingleSegment = true`,
  `AudioCtx` 256 to 384 (`WhisperParams.cs:306`), initial prompt seeded with the four words, greedy
  decode. Expected range is 100 to 300ms. It gets measured. No sub-200ms claim without a number.
- The gate hangs on the existing seam at `VoiceCommandProducer.cs:226`, not a parallel system.
- If `KeywordRecognizer` stays in at all, the conditions are: gate on
  `PhraseRecognitionSystem.isSupported`, try/catch the constructor, treat any `SpeechError` as a
  permanent disable, ship defaulted off.
- The correct compile guard is `#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN`.

**The 20-minute check that closes this:** log `PhraseRecognitionSystem.isSupported` at startup on
the actual target machine. If false, the question is closed. If true, start mic then recognizer,
hold 60s, reverse the order, and check whisper transcription quality does not degrade. The result
goes in the docs.

**Settles it:** https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Windows.Speech.KeywordRecognizer.html (Windows 10 only, unchanged in 6.5)

---

## 2. WebGL / itch.io build

**Confirmed:** It breaks at Emscripten link time, not C# compile time:
`undefined symbol: whisper_init_from_file_with_params`. The package asmdef has empty
`includePlatforms` and `excludePlatforms`, so it targets everything, and `WhisperNative.cs:15-19`
emits ~40 `[DllImport("libwhisper")]` for a library with no WebGL binary. Brotli on itch is a solved
problem, not a blocker. Mic capture on Web is now supported, so that is not the blocker either. The
missing WebAssembly build of whisper.cpp is.

**What I decided to do, in this order:**
1. Embed the package: copy `Library\PackageCache\com.whisper.unity@de9ce92ad4c5` to
   `Packages\com.whisper.unity`, drop the git URL from `manifest.json`. PackageCache is immutable,
   so the Plugin Inspector route is a dead end.
2. Set `"excludePlatforms": ["WebGL"]` on the runtime asmdef and the tests asmdef. This is the fix.
   `#if UNITY_WEBGL` on project scripts alone is not sufficient, because the package assembly still
   compiles.
3. Guard call sites with `#if !UNITY_WEBGL`, not `#if UNITY_STANDALONE_WIN`:
   `VoiceCommandProducer.cs` (whole file), `Bootstrap.cs:100,121`, `SpikeRunner.cs:42`,
   `BootSceneBuilder.cs:148`, `SpikeSceneBuilder.cs:73`. Confirm `Bootstrap` survives with only
   `KeyboardCommandProducer`.
4. Strip the models. `StreamingAssets\Whisper` is 148 MB of two ggml files and ships on Web by URL.
   That is a third of itch's 500 MB extracted budget, for a build with no voice.
5. Three fixes in `DikdikBuild.cs`: line 87 `WebGL.memorySize = 512` is a deprecated no-op, delete
   it. Line 90 `exceptionSupport = None` has a known build-failure issue, use
   `ExplicitlyThrownExceptionsOnly`. Optionally switch line 86 to Brotli. Add
   `decompressionFallback = false` and `dataCaching = true` explicitly. Leave Code Optimization at
   default (Runtime Speed breaks with unsafe code, and whisper.unity sets `allowUnsafeCode`). Leave
   threading off.
6. Zip so `index.html` is at the ZIP root. Kind of project = HTML, viewport 1280x720, SharedArrayBuffer
   checkbox unticked.

**Judgement call, taken:** the browser build ships keyboard-only and the itch page says so. The
voice control is the point of the project. Web is the trailer.

**Settles it:** https://docs.unity3d.com/6000.4/Documentation/Manual/assembly-definition-file-format.html (`excludePlatforms: ["WebGL"]`, the exact platform string)

---

## 3. Opening cinematic

**Confirmed:** Procedural, code-generated, coroutine-driven, extending `GradientSky` is all correct.
The pipeline premise is not. Neither Cinemachine nor Timeline is installed (`packages-lock.json` has
`com.unity.modules.director`, the Playables module, not `com.unity.timeline`), so `TimelineAsset`
does not exist in this project. And audio sync is an architecture problem, not a tuning problem:
`VoiceArbiter.Update` (lines 274-283) already runs a frame clock (`Time.unscaledDeltaTime`) against
an audio clock (`clip.length`), and each `Play()` starts on the next mix-buffer boundary, so error
accumulates across a multi-line briefing.

**What I decided:**
- One camera. Built-In pipeline: `Camera.depth` plus Clear Flags, not URP stacking. Near clip 0.3 to
  1.0, far ~5000.
- The cinematic gets no timer of its own. Camera beats and highlight cues run off the existing
  `VoiceArbiter.LineStarted` / `LineFinished` / `SequenceFinished` events (`VoiceArbiter.cs:87-89`).
  Drift becomes structurally impossible, and it survives `SkipCurrentSequence()`, which an absolute
  schedule does not. `AudioSettings.dspTime` only becomes an option if the briefing is recorded as
  one clip.
- Extend the existing shader rather than write a second one. Two new properties:
  `_StarsEverywhere` to defeat the `if (h > 0.05)` upper-hemisphere gate (lines 76-83), and
  `_SpaceBlend` for the descent. Swap `step` for `smoothstep` on the star hash, or slow pans will
  crawl.
- `RenderSettings.skybox = new Material(skyAsset)` in Awake. Animating the loaded asset permanently
  dirties `Assets/Materials/GradientSky_*.mat` on every Play.
- Ambient is `AmbientMode.Trilight` with explicit colours (`Environment.cs:49-52`), so light will
  not change on descent unless `ambientSkyColor` / `ambientEquatorColor` are lerped explicitly.
- Planet: `CreatePrimitive(Sphere)`, `DestroyImmediate` the collider, lit by the existing
  directional light. Not in the skybox: it has to translate and grow.
- Hand off with `CameraFollow.SetTarget(rover)`, which snaps internally (`CameraFollow.cs:68`). The
  last keyframe equals `rover.TransformPoint(offset)` so the snap is invisible.
- Highlight: code-generated brackets, not `HazardOutline`. That is `[RequireComponent(MeshFilter)]`
  on a single `sharedMesh`, and Kenney props are multi-part hierarchies. Reusing it would also
  overload a signal that already means "hazard" in Level 3. Fires from the same `LineStarted`
  handler as the caption.
- Fade in from black over ~0.5s and start audio after two `WaitForEndOfFrame`, so the new shader
  variant compiles behind a static frame.

**Ruled out:** Cinemachine, Timeline, URP, a second skybox shader, a second camera.

**Settles it:** `C:\dev\dik-dik\ProjectSettings\GraphicsSettings.asset` line 49,
`m_CustomRenderPipeline: {fileID: 0}`. Built-In, confirmed on disk.

---

## 4. Accessibility claims

**Confirmed:** Most areas map to real guidelines. Four are misnamed or do not exist.

| Plan says | Published reality |
|---|---|
| "subtitles for all speech" | *Provide subtitles for all important speech*, Basic, Hearing |
| "no failure state" | No such guideline. Use *Offer a wide choice of difficulty levels* (Basic, General). The nearest match, *Include a means of practicing without failure...*, is Intermediate |
| "high contrast / colour-blind safe" | Two separate Basic Vision guidelines: *Provide high contrast between text/UI and background* and *Ensure no essential information is conveyed by a fixed colour alone* |
| "settings before and during play" | Not a guideline. Assemble from *Ensure that all settings are saved/remembered* (Basic, General) and *Ensure subtitles/captions are or can be turned on before any sound is played* (Intermediate, Hearing). Always-on-from-launch is a design position of this project, not a published requirement |
| "voice input as alternative control scheme" | The guideline is the inverse. Speech must never be required. Keyboard parity is the compliance mechanism, not a bonus |

**Claims that hold, stated correctly:**
- *Base speech recognition on hitting a volume threshold* is **Advanced**, Speech, and the
  record-any-sound remapping (README line 248) satisfies it. One honest Advanced beats six inflated
  Basics.
- The accent problem cites *Base speech recognition on individual words from a small vocabulary*
  (Intermediate, Speech). It is the only place in the corpus that names regional accents as a
  recognition failure mode. Backed with Koenecke et al., PNAS 2020 (WER 0.35 vs 0.19) and EdAcc.
  *Keep background noise to minimum during speech* does not apply here; that one is about output
  mixing, and a reviewer would catch the substitution.

**Privacy:** no accessibility standard covers audio retention. Not GAG, not XAG, not APX, not
WCAG2ICT. Citing WCAG2ICT here is a category error. The citations are GDPR Art. 5(1)(c) and 5(1)(e),
plus EDPB Guidelines 02/2021 on virtual voice assistants (delete after the command executes, absent
a legal basis). A visible mic-live indicator gets added: the W3C Web Speech API requires one and the
game has no equivalent. The docs also have to say where the non-verbal sound templates live on disk
and how a player deletes them. Without that, "no audio leaving the machine" reads as "no audio
retained", which is a different claim.

**What the evaluation section needs before it is worth reading:** whisper.cpp commit, model and
quantisation, sample rate, VAD settings, decode params, utterance count, command classes,
false-accept split from false-reject, median and tail latency, and the sentence "n=1, this is a
first-person probe, not an evaluation." The 81.2% figure has no denominator, and that is the
clearest hobbyist tell in the document.

The tally becomes three columns: exact published title, tier, where it is demonstrable in the build.
Plus rows for partial, and rows for "deliberately not implemented, because." Those last rows earn
more credit than the implemented ones.

**Settles it:** https://gameaccessibilityguidelines.com/ensure-that-speech-input-is-not-required-and-included-only-as-a-supplementary-alternative-input-method/

---

## Sequence

1. README line 171 first. Five-minute edit, and until it is done it is a credibility hole.
2. The `isSupported` spike. It closes topic 1 either way.
3. The WebGL six steps, in the order above. Embedding the package gates everything after it.
4. The cinematic last: event-driven off `VoiceArbiter`, Built-In pipeline, one camera.
