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

        // 2.6 seconds. The number came from round-trip light time to the Moon, about
        // 2.56s, with Apollo 12 measuring 2.712s on the day. The story moved to an
        // unnamed planet and the number stayed, because its real job was never the
        // astronomy: it is applied to voice AND keyboard, measured from when the player
        // finished giving the command, so neither route reaches the rover first.
        serialized.FindProperty("transportDelay").floatValue = 2.6f;
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

        microphone.frequency = 16000;   // what whisper wants, so no resampling
        microphone.echo = false;        // playing the player's voice back at them is irritating
        microphone.useVad = true;
        microphone.vadStop = true;
        microphone.vadStopTime = 1.5f;  // end of utterance on silence, generous for slow speech
        microphone.dropVadPart = true;
        microphone.maxLengthSec = 30;

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
        var bar = NewUiObject("Signal Bar", panel.transform);
        var barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.offsetMin = new Vector2(28f, 14f);
        barRect.offsetMax = new Vector2(-28f, 20f);

        var barImage = bar.AddComponent<Image>();
        barImage.color = new Color(0.55f, 0.75f, 1f);
        barImage.type = Image.Type.Filled;
        barImage.fillMethod = Image.FillMethod.Horizontal;
        barImage.fillOrigin = 0;
        barImage.fillAmount = 0f;
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
