# Decision Brief: dik-dik technical assumptions

**Four assumptions came back REFUTED. Three of them are load-bearing. Read the red block first.**

---

## ðŸ”´ REFUTED, stop before you code

| # | Assumption in the plan | Reality |
|---|---|---|
| 1 | `KeywordRecognizer` gives a reliable ~100ms hotword path on Windows 11 | Unity's own doc still says: "Keyword recognizer is currently functional only on Windows 10." Microsoft deprecated Windows speech recognition (Dec 2023 entry, handover on 22H2+ in Sept 2024). Zero tested evidence for Win11. Fails as a thrown `UnityException` on unsupported machines, not a graceful false. |
| 2 | The project is URP | It is Built-In Render Pipeline. `ProjectSettings/GraphicsSettings.asset` line 49 has no custom RP, manifest has no URP package, `GradientSky.shader` is CGPROGRAM/UnityCG.cginc. **Every line of URP camera-stacking code you write is dead.** |
| 3 | `#if UNITY_STANDALONE_WIN && !UNITY_EDITOR` or `ENABLE_WINMD_SUPPORT` guards the speech code | Both wrong. The first compiles speech out of the Editor so you cannot test in Play mode. The second is the WinRT/UWP metadata flag, unrelated to desktop `UnityEngine.Windows.Speech`. |
| 4 | README line 171: the Speech category is empty at Basic level | False. It has exactly one Basic guideline, and it is the most on-point one in the whole set: *Ensure that speech input is not required, and included only as a supplementary / alternative input method*. A reviewer who knows the set spots this in seconds and discounts the rest of the document. |

Also stale: whisper.unity's README lists WebGL as supported. Its own issue #20 has sat unanswered since April 2023 and the package ships no WebGL binaries. Treat Web as unsupported.

---

## 1. Voice recognition path

**True:** `KeywordRecognizer` exists in Unity 6.x, is not `[Obsolete]`, and needs nothing in `manifest.json`. Everything after that is unsupported. Whether it can share the mic with `UnityEngine.Microphone` is answered by no authoritative source anywhere. `MicrophoneRecord.cs:262` holds the device for the whole session, so if the conflict exists it bites every time.

**Code must do differently:**
- Drop `KeywordRecognizer` from the critical path entirely. Instant-STOP must not depend on it.
- One `Microphone.Start`, ever. Wire `MicrophoneRecord.OnVadChanged` as the instant gate (VAD ticks at `vadUpdateRateSec = 0.1f`). On onset, cut throttle and arm the stop before you know the word. That is defensible interaction design, not a workaround.
- Confirm with a second `WhisperManager` on tiny.en over a ~1s slice: `SingleSegment = true`, `AudioCtx` 256 to 384 (`WhisperParams.cs:306`), initial prompt seeded with the four words, greedy decode. Expect 100 to 300ms. Measure it; do not claim sub-200ms.
- Hang the gate on the existing seam at `VoiceCommandProducer.cs:226`, not a parallel system.
- If you keep `KeywordRecognizer` at all: gate on `PhraseRecognitionSystem.isSupported`, try/catch the constructor, treat any `SpeechError` as permanent disable, ship defaulted off.
- Correct compile guard when you do add it: `#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN`.

**Do this first, 20 minutes:** log `PhraseRecognitionSystem.isSupported` at startup on your actual machine. If false, the question is closed. If true, start mic then recognizer, hold 60s, reverse the order, and check whisper transcription quality does not degrade. Record the result in the docs.

**Settles it:** https://docs.unity3d.com/6000.5/Documentation/ScriptReference/Windows.Speech.KeywordRecognizer.html (Windows 10 only, unchanged in 6.5)

---

## 2. WebGL / itch.io build

**True:** It breaks at Emscripten link time, not C# compile time: `undefined symbol: whisper_init_from_file_with_params`. The package asmdef has empty `includePlatforms` and `excludePlatforms`, so it targets everything, and `WhisperNative.cs:15-19` emits ~40 `[DllImport("libwhisper")]` for a library with no WebGL binary. Brotli on itch is a solved problem, not a blocker. Mic capture on Web is now supported, so that is not the blocker either. The missing WebAssembly build of whisper.cpp is.

**Code must do differently:**
1. Embed the package: copy `Library\PackageCache\com.whisper.unity@de9ce92ad4c5` to `Packages\com.whisper.unity`, drop the git URL from `manifest.json`. PackageCache is immutable, so the Plugin Inspector route is a dead end.
2. Set `"excludePlatforms": ["WebGL"]` on the runtime asmdef and the tests asmdef. This is the fix. `#if UNITY_WEBGL` on your own scripts alone is not sufficient because the package assembly still compiles.
3. Guard call sites with `#if !UNITY_WEBGL`, not `#if UNITY_STANDALONE_WIN`: `VoiceCommandProducer.cs` (whole file), `Bootstrap.cs:100,121`, `SpikeRunner.cs:42`, `BootSceneBuilder.cs:148`, `SpikeSceneBuilder.cs:73`. Confirm `Bootstrap` survives with only `KeyboardCommandProducer`.
4. Strip the models. `StreamingAssets\Whisper` is 148 MB of two ggml files and ships on Web by URL. That is a third of itch's 500 MB extracted budget for a build with no voice.
5. Three fixes in `DikdikBuild.cs`: line 87 `WebGL.memorySize = 512` is a deprecated no-op, delete it. Line 90 `exceptionSupport = None` has a known build-failure issue, use `ExplicitlyThrownExceptionsOnly`. Optionally switch line 86 to Brotli. Add `decompressionFallback = false` and `dataCaching = true` explicitly. Leave Code Optimization at default (Runtime Speed breaks with unsafe code, and whisper.unity sets `allowUnsafeCode`). Leave threading off.
6. Zip so `index.html` is at the ZIP root. Kind of project = HTML, viewport 1280x720, leave the SharedArrayBuffer checkbox unticked.

**Judgement call:** ship the browser build keyboard-only and say so on the itch page. The voice control is the point of the project; Web is the trailer.

**Settles it:** https://docs.unity3d.com/6000.4/Documentation/Manual/assembly-definition-file-format.html (`excludePlatforms: ["WebGL"]`, the exact platform string)

---

## 3. Opening cinematic

**True:** Procedural, code-generated, coroutine-driven, extending `GradientSky` is all correct. The pipeline premise is not. Neither Cinemachine nor Timeline is installed (`packages-lock.json` has `com.unity.modules.director`, the Playables module, not `com.unity.timeline`), so `TimelineAsset` does not exist in this project. And audio sync is an architecture problem, not a tuning problem: `VoiceArbiter.Update` (lines 274-283) already runs a frame clock (`Time.unscaledDeltaTime`) against an audio clock (`clip.length`), and each `Play()` starts on the next mix-buffer boundary, so error accumulates across a multi-line briefing.

**Code must do differently:**
- One camera. Built-In pipeline: `Camera.depth` plus Clear Flags, not URP stacking. Near clip 0.3 to 1.0, far ~5000.
- Do not give the cinematic its own timer. Drive camera beats and highlight cues off the existing `VoiceArbiter.LineStarted` / `LineFinished` / `SequenceFinished` events (`VoiceArbiter.cs:87-89`). Drift becomes structurally impossible and it survives `SkipCurrentSequence()`, which an absolute schedule does not. Only switch to `AudioSettings.dspTime` if you record the briefing as one clip.
- Extend the existing shader rather than writing a second one. Two new properties: `_StarsEverywhere` to defeat the `if (h > 0.05)` upper-hemisphere gate (lines 76-83), and `_SpaceBlend` for the descent. Swap `step` for `smoothstep` on the star hash or slow pans will crawl.
- `RenderSettings.skybox = new Material(skyAsset)` in Awake. Animating the loaded asset permanently dirties `Assets/Materials/GradientSky_*.mat` on every Play.
- Ambient is `AmbientMode.Trilight` with explicit colours (`Environment.cs:49-52`), so light will not change on descent unless you lerp `ambientSkyColor` / `ambientEquatorColor` yourself.
- Planet: `CreatePrimitive(Sphere)`, `DestroyImmediate` the collider, lit by the existing directional light. Not in the skybox, it has to translate and grow.
- Hand off with `CameraFollow.SetTarget(rover)`, which snaps internally (`CameraFollow.cs:68`). Make the last keyframe equal `rover.TransformPoint(offset)` so the snap is invisible.
- Highlight: build code-generated brackets, not `HazardOutline`. It is `[RequireComponent(MeshFilter)]` on a single `sharedMesh`, and Kenney props are multi-part hierarchies. Reusing it also overloads a signal that already means "hazard" in Level 3. Fire it from the same `LineStarted` handler as the caption.
- Fade in from black over ~0.5s and start audio after two `WaitForEndOfFrame` so the new shader variant compiles behind a static frame.

**Do not:** add Cinemachine, add Timeline, add URP, write a second skybox shader, add a second camera.

**Settles it:** `C:\dev\dik-dik\ProjectSettings\GraphicsSettings.asset` line 49, `m_CustomRenderPipeline: {fileID: 0}`. Built-In, confirmed on disk.

---

## 4. Accessibility claims

**True:** Most areas map to real guidelines. Four are misnamed or do not exist.

| Plan says | Published reality |
|---|---|
| "subtitles for all speech" | *Provide subtitles for all important speech*, Basic, Hearing |
| "no failure state" | No such guideline. Use *Offer a wide choice of difficulty levels* (Basic, General). The nearest match, *Include a means of practicing without failure...*, is Intermediate |
| "high contrast / colour-blind safe" | Two separate Basic Vision guidelines: *Provide high contrast between text/UI and background* and *Ensure no essential information is conveyed by a fixed colour alone* |
| "settings before and during play" | Not a guideline. Assemble from *Ensure that all settings are saved/remembered* (Basic, General) and *Ensure subtitles/captions are or can be turned on before any sound is played* (Intermediate, Hearing). Present always-on-from-launch as your own design position |
| "voice input as alternative control scheme" | The guideline is the inverse. Speech must never be required. Keyboard parity is the compliance mechanism, not a bonus |

**Wins to claim, correctly:**
- *Base speech recognition on hitting a volume threshold* is **Advanced**, Speech, and the record-any-sound remapping (README line 248) satisfies it. One honest Advanced beats six inflated Basics.
- For the accent problem, cite *Base speech recognition on individual words from a small vocabulary* (Intermediate, Speech). It is the only place in the corpus that names regional accents as a recognition failure mode. Back it with Koenecke et al., PNAS 2020 (WER 0.35 vs 0.19) and EdAcc. Do not repurpose *Keep background noise to minimum during speech*; that is about output mixing and a reviewer will catch it.

**Privacy:** no accessibility standard covers audio retention. Not GAG, not XAG, not APX, not WCAG2ICT. Citing WCAG2ICT here is a category error. Use GDPR Art. 5(1)(c) and 5(1)(e), and EDPB Guidelines 02/2021 on virtual voice assistants (delete after the command executes absent a legal basis). Add a visible mic-live indicator; the W3C Web Speech API requires one and the game has no equivalent. Disclose where the non-verbal sound templates live on disk and how a player deletes them, or "no audio leaving the machine" reads as "no audio retained", which is a different claim.

**Evaluation section, required before anyone reads this:** whisper.cpp commit, model and quantisation, sample rate, VAD settings, decode params, utterance count, command classes, false-accept split from false-reject, median and tail latency, and the sentence "n=1, this is a first-person probe, not an evaluation." The 81.2% figure has no denominator and that is the clearest hobbyist tell in the document.

Restructure the tally as three columns: exact published title, tier, where it is demonstrable in the build. Add rows for partial and for "deliberately not implemented, because." Those last rows earn more credit than the implemented ones.

**Settles it:** https://gameaccessibilityguidelines.com/ensure-that-speech-input-is-not-required-and-included-only-as-a-supplementary-alternative-input-method/

---

## Order of work

1. Fix README line 171 today. It is a five-minute edit and it is currently a credibility hole.
2. Run the `isSupported` spike. It closes topic 1 either way.
3. Do the WebGL six-step, in the stated order. Embedding the package is the gate for everything after it.
4. Build the cinematic last, event-driven off `VoiceArbiter`, Built-In pipeline, one camera.