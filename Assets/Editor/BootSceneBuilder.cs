using System.IO;
using Dikdik.Commands;
using Dikdik.Game;
using Dikdik.Producers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Whisper;
using Whisper.Utils;

/// <summary>
/// Generates the Boot scene: every persistent system plus the comms console.
///
/// Written as a script rather than clicked together because a hand-built scene is a
/// binary blob nobody can review. This way the exact microphone settings, the transport
/// delay and the console layout are all readable in a diff, and anyone can regenerate
/// an identical scene from scratch.
///
/// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod BootSceneBuilder.Generate
/// </summary>
public static class BootSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Boot.unity";

    // Arial was removed from Unity's builtin resources; this is its replacement.
    private static Font BuiltinFont =>
        Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    [MenuItem("Dikdik/Generate Boot Scene")]
    public static void Generate()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var root = new GameObject("Bootstrap");
        var bootstrap = root.AddComponent<Bootstrap>();

        var bus = BuildBus(root);
        BuildVoice(root);
        root.AddComponent<KeyboardCommandProducer>();

        // Settings live with the persistent systems, so Escape works in every scene
        // including any menu we add later. Never per-level, or a level could ship
        // without them and quietly gate access behind progress.
        root.AddComponent<SettingsMenu>();

        var comms = BuildConsole(root, out var camera);

        // Every spoken line in the game goes through this one object and this one
        // AudioSource. Control, the station's automated system and the rover all queue
        // here, so two of them can never talk at once. It also owns the captions, so
        // subtitles cannot drift from audio, and it owns the microphone gate, so the game
        // never transcribes its own voice coming back through the speakers.
        var voiceObject = new GameObject("Voice");
        voiceObject.transform.SetParent(root.transform);
        var voiceSource = voiceObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.spatialBlend = 0f;

        var arbiter = voiceObject.AddComponent<Dikdik.Game.Voice.VoiceArbiter>();
        var arbiterSerialized = new SerializedObject(arbiter);
        SetRef(arbiterSerialized, "source", voiceSource);
        SetRef(arbiterSerialized, "comms", comms);
        arbiterSerialized.FindProperty("voiceFolder").stringValue = "Voice";
        arbiterSerialized.ApplyModifiedPropertiesWithoutUndo();

        // Decides when Control speaks. It no longer owns any audio.
        voiceObject.AddComponent<SupervisorVoice>();

        // A low connection hum, always on. It says "you are still on the loop" so the
        // silence is never dead and the supervisor does not have to keep filling it. Its
        // own persistent source, looping, quiet enough to sit under everything else.
        var ambientObject = new GameObject("Ambient");
        ambientObject.transform.SetParent(root.transform);
        var ambient = ambientObject.AddComponent<AudioSource>();
        ambient.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/connection_loop.wav");
        ambient.loop = true;
        ambient.playOnAwake = true;
        ambient.spatialBlend = 0f;
        // Quiet. The clip peaks near full scale now, so this is doing real work: it is a
        // bed you notice only when it stops, not a sound you listen to. The first attempt
        // at fixing the inaudible hum overshot and made it a presence in the mix.
        ambient.volume = 0.11f;

        if (ambient.clip == null)
            Debug.LogError("[BootSceneBuilder] connection_loop.wav not found. Ambient will be silent.");

        var bootSerialized = new SerializedObject(bootstrap);
        SetRef(bootSerialized, "bus", bus);
        SetRef(bootSerialized, "comms", comms);
        bootSerialized.FindProperty("firstScene").stringValue = "Level01";
        bootSerialized.FindProperty("loadFirstScene").boolValue = true;
        bootSerialized.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[BootSceneBuilder] Wrote {ScenePath}");
    }

    private static CommandBus BuildBus(GameObject root)
    {
        var bus = root.AddComponent<CommandBus>();

        var serialized = new SerializedObject(bus);

        // Zero. The deliberate delay is gone, on the designer's call.
        //
        // It was 2.6 seconds, from round-trip light time to the Moon, and it did two jobs.
        // One was diegetic: the rover is far away and a signal takes time. The other was
        // parity: the keyboard waited the same, so voice was never the slower option.
        //
        // The parity job is now done properly instead of by handicap. Onset timestamping
        // (see Intent.StartedAt) makes both inputs land at decision time plus whatever the
        // pipeline genuinely costs, so there is nothing left to equalise by taxing the fast
        // path. The literature agrees and got there first: Zander et al. named latency
        // balancing in 2005, and Bogon et al. (CHI 2025) found players pre-adapt to
        // anticipated delay, so a uniform tax degrades the quick input without helping the
        // slow one. See docs/latency-prior-art.md.
        //
        // The diegetic job was the honest half, and it loses to the thing this project is
        // now actually about. A fixed delay on top of a real recognition delay meant the
        // player waited twice, and only one of those waits was interesting.
        //
        // Kept as a serialized field rather than deleted, because the latency study sweeps
        // it: how much delay a voice-driven vehicle tolerates is the open question, and you
        // need the dial to ask it.
        serialized.FindProperty("transportDelay").floatValue = 0f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return bus;
    }

    private static void BuildVoice(GameObject root)
    {
        var whisper = root.AddComponent<WhisperManager>();
        var microphone = root.AddComponent<MicrophoneRecord>();

        var serialized = new SerializedObject(whisper);
        serialized.FindProperty("modelPath").stringValue = "Whisper/ggml-tiny.bin";
        serialized.FindProperty("isModelPathInStreamingAssets").boolValue = true;
        serialized.FindProperty("initOnAwake").boolValue = true;
        serialized.FindProperty("useGpu").boolValue = false;          // CPU baseline
        serialized.FindProperty("flashAttention").boolValue = false;  // known to slow inference
        serialized.ApplyModifiedPropertiesWithoutUndo();

        whisper.language = "en";
        whisper.noContext = true;
        whisper.singleSegment = true;
        whisper.initialPrompt = "";   // honest baseline; seeding is a lever held in reserve

        // ------------------------------------------------------------------
        // Latency. Three settings, and between them they were most of the wait.
        // ------------------------------------------------------------------

        // 1. audioCtx, and this was free money sitting on the floor.
        //
        // Zero means the default, 1500, which makes the encoder process a full THIRTY
        // SECOND window no matter how much audio was actually captured. Commands here are
        // about a second. whisper.cpp's own formula for the useful size is
        // (audio_length / 30) * 1500 + 128, so a one-second utterance needs about 178, and
        // upstream reports roughly a 3x encoder speedup on short clips.
        //
        // 256 rather than 178: a margin, because a slow speaker saying "turn to the left"
        // is longer than a second, and the documented failure mode when this is set too
        // low is the decoder producing nonsense rather than merely being less accurate.
        whisper.audioCtx = 256;

        microphone.frequency = 16000;   // what whisper wants, so no resampling
        microphone.echo = false;        // playing the player's voice back at them is irritating
        microphone.useVad = true;
        microphone.vadStop = true;
        microphone.dropVadPart = true;
        microphone.maxLengthSec = 30;

        // 2. vadStopTime, which was the biggest single cost in the whole pipeline and
        //    nobody had noticed, including me.
        //
        // This is how long the microphone waits in silence before deciding the sentence is
        // over. It was 1.5 seconds, and it is paid on every single command, before
        // transcription has even started. The project's headline "1875 ms" figure was
        // measured from the moment the microphone closed, so it never included this at all.
        //
        // 0.45 is short enough to feel responsive and long enough to survive the natural
        // gap in "turn... left". Shorter than about 0.3 starts cutting people off mid
        // sentence, which trades a latency complaint for a much worse one.
        microphone.vadStopTime = 0.45f;

        // 3. vadLastSec, the window the detector looks at to decide whether there is
        //    speech in it. At 1.25 seconds the window has to fill with silence before it
        //    reports silence, which adds lag to noticing the end of an utterance on top of
        //    the timeout above. 0.5 still spans a syllable comfortably.
        microphone.vadLastSec = 0.5f;

        var voice = root.AddComponent<VoiceCommandProducer>();
        var voiceSerialized = new SerializedObject(voice);
        SetRef(voiceSerialized, "whisper", whisper);
        SetRef(voiceSerialized, "microphone", microphone);
        voiceSerialized.FindProperty("continuousListening").boolValue = true;
        voiceSerialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static CommsDisplay BuildConsole(GameObject root, out Camera camera)
    {
        // A camera lives here so the Boot scene is valid on its own. Levels bring their
        // own and this one steps aside.
        var cameraObject = new GameObject("Boot Camera");
        cameraObject.transform.SetParent(root.transform);
        camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.04f, 0.05f, 0.07f);
        camera.depth = -10;

        // AddComponent<Camera> does not bring an AudioListener with it, unlike creating a
        // camera from the editor menu. Without one the Boot scene has no listener at all,
        // so the ambient loop starts playing into nothing on the very first frame and
        // Unity logs a warning. Level cameras have their own; this one covers the gap
        // before the first level loads.
        cameraObject.AddComponent<AudioListener>();

        var canvasObject = new GameObject("Comms Canvas");
        canvasObject.transform.SetParent(root.transform);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        // Panel across the TOP, over the sky. The rover sits at the bottom-centre under
        // the follow camera, so a panel at the bottom covered it. Up here the dark panel
        // reads cleanly against the bright horizon and never hides the rover.
        var panel = NewUiObject("Comms Panel", canvasObject.transform);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.offsetMin = new Vector2(48f, -232f);
        panelRect.offsetMax = new Vector2(-48f, -32f);

        var background = panel.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.65f);
        background.raycastTarget = false;

        var heard = NewText("Heard", panel.transform, 28, TextAnchor.LowerLeft);
        var heardRect = heard.GetComponent<RectTransform>();
        heardRect.anchorMin = new Vector2(0f, 0f);
        heardRect.anchorMax = new Vector2(1f, 1f);
        heardRect.offsetMin = new Vector2(28f, 74f);
        // Leave room at the top for the speaker badge.
        heardRect.offsetMax = new Vector2(-28f, -52f);

        var status = NewText("Status", panel.transform, 18, TextAnchor.LowerLeft);
        var statusRect = status.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(1f, 0f);
        statusRect.offsetMin = new Vector2(28f, 34f);
        statusRect.offsetMax = new Vector2(-28f, 70f);

        // Signal bar. Fills left to right as the command crosses the gap. Not a spinner:
        // a spinner claims the software is busy, this claims the rover is far away, and
        // only the second is true.
        // The microphone row. Its own line, at the top right of the panel, well away from
        // the caption. These two used to share one text field and the mic state kept
        // winning during the briefing, so the player was told to say something while
        // Control was mid-sentence and never saw a subtitle.
        var micDot = NewUiObject("Mic Dot", panel.transform);
        var micDotRect = micDot.GetComponent<RectTransform>();
        micDotRect.anchorMin = new Vector2(1f, 1f);
        micDotRect.anchorMax = new Vector2(1f, 1f);
        micDotRect.pivot = new Vector2(1f, 1f);
        micDotRect.anchoredPosition = new Vector2(-232f, -14f);
        micDotRect.sizeDelta = new Vector2(12f, 12f);

        var micDotImage = micDot.AddComponent<Image>();
        micDotImage.color = new Color(0.4f, 0.85f, 0.55f);
        micDotImage.raycastTarget = false;

        var micState = NewText("Mic State", panel.transform, 18, TextAnchor.UpperRight);
        var micStateRect = micState.GetComponent<RectTransform>();
        micStateRect.anchorMin = new Vector2(1f, 1f);
        micStateRect.anchorMax = new Vector2(1f, 1f);
        micStateRect.pivot = new Vector2(1f, 1f);
        micStateRect.anchoredPosition = new Vector2(-28f, -8f);
        micStateRect.sizeDelta = new Vector2(216f, 26f);

        // A track that is always there, so the bar has somewhere to travel and the player
        // can see how far there is left to go rather than only how far it has come.
        var barTrack = NewUiObject("Signal Track", panel.transform);
        var trackRect = barTrack.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0f, 0f);
        trackRect.anchorMax = new Vector2(1f, 0f);
        trackRect.offsetMin = new Vector2(28f, 14f);
        trackRect.offsetMax = new Vector2(-28f, 28f);

        var trackImage = barTrack.AddComponent<Image>();
        trackImage.color = new Color(1f, 1f, 1f, 0.12f);
        trackImage.raycastTarget = false;

        // Not Image.Type.Filled. It was, and it never worked.
        //
        // A Filled image with no sprite falls through Image.OnPopulateMesh's early return
        // before the type switch and draws one quad across the whole rect. fillAmount was
        // written every frame and read by nothing, so the bar was permanently full and
        // simply blinked on and off for 2.6 seconds. Both bars in this game had it.
        //
        // Assigning a builtin sprite is the obvious fix and does not work here:
        // Resources.GetBuiltinResource already fails in batch mode in this project. So the
        // right anchor does the work instead.
        //
        // Also taller. It was 6 canvas units, which is 4 screen pixels at 720p, and a 4
        // pixel line that blinks is easy to read as nothing at all.
        var bar = NewUiObject("Signal Bar", barTrack.transform);
        var barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = Vector2.zero;
        barRect.anchorMax = new Vector2(0f, 1f);
        barRect.offsetMin = Vector2.zero;
        barRect.offsetMax = Vector2.zero;

        var barImage = bar.AddComponent<Image>();
        barImage.color = new Color(0.55f, 0.75f, 1f);
        barImage.raycastTarget = false;

        // Speaker badge: a small person icon and a name, shown only when someone is
        // speaking TO the player. Sits at the top-left of the panel, above the text.
        // Built from two built-in UI sprites so it needs no art: a circle for the head
        // and a rounded rectangle for the shoulders.
        var circle = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        var rounded = Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");

        var badge = NewUiObject("Speaker Badge", panel.transform);
        var badgeRect = badge.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0f, 1f);
        badgeRect.anchorMax = new Vector2(0f, 1f);
        badgeRect.pivot = new Vector2(0f, 1f);
        badgeRect.anchoredPosition = new Vector2(28f, -12f);
        badgeRect.sizeDelta = new Vector2(300f, 32f);

        var head = MakeIcon("Head", badge.transform, circle, new Vector2(16f, 16f),
                            new Vector2(8f, -3f));
        var body = MakeIcon("Body", badge.transform, rounded, new Vector2(22f, 14f),
                            new Vector2(5f, -17f));

        var speakerName = NewText("Speaker Name", badge.transform, 17, TextAnchor.MiddleLeft);
        speakerName.fontStyle = FontStyle.Bold;
        var nameRect = speakerName.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(0f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.anchoredPosition = new Vector2(38f, -2f);
        nameRect.sizeDelta = new Vector2(240f, 30f);

        // Hidden until someone actually speaks.
        badge.SetActive(false);

        var comms = canvasObject.AddComponent<CommsDisplay>();
        var commsSerialized = new SerializedObject(comms);
        SetRef(commsSerialized, "heardText", heard);
        SetRef(commsSerialized, "statusText", status);
        SetRef(commsSerialized, "background", background);
        SetRef(commsSerialized, "signalBar", barImage);
        SetRef(commsSerialized, "signalTrack", trackImage);
        SetRef(commsSerialized, "micStateText", micState);
        SetRef(commsSerialized, "micDot", micDotImage);
        SetRef(commsSerialized, "speakerBadge", badge);
        SetRef(commsSerialized, "speakerName", speakerName);
        SetRef(commsSerialized, "speakerIconHead", head);
        SetRef(commsSerialized, "speakerIconBody", body);
        commsSerialized.ApplyModifiedPropertiesWithoutUndo();

        return comms;
    }

    private static GameObject NewUiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    /// <summary>A small UI icon from a built-in sprite, top-left anchored inside its parent.</summary>
    private static Image MakeIcon(string name, Transform parent, Sprite sprite,
                                  Vector2 size, Vector2 position)
    {
        var go = NewUiObject(name, parent);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.color = new Color(0.62f, 0.78f, 1f);
        image.raycastTarget = false;

        return image;
    }

    private static Text NewText(string name, Transform parent, int size, TextAnchor anchor)
    {
        var go = NewUiObject(name, parent);
        var text = go.AddComponent<Text>();

        text.font = BuiltinFont;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = new Color(0.92f, 0.94f, 0.96f);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.text = string.Empty;

        return text;
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        var property = so.FindProperty(field);
        if (property != null) property.objectReferenceValue = value;
        else Debug.LogWarning($"[BootSceneBuilder] No field '{field}'");
    }
}
