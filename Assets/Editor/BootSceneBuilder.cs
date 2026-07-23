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
        var journal = root.AddComponent<VoiceJournal>();
        BuildVoice(root);
        root.AddComponent<KeyboardCommandProducer>();

        // Settings live with the persistent systems, so Escape works in every scene
        // including any menu we add later. Never per-level, or a level could ship
        // without them and quietly gate access behind progress.
        root.AddComponent<SettingsMenu>();

        var comms = BuildConsole(root, out var camera);

        var bootSerialized = new SerializedObject(bootstrap);
        SetRef(bootSerialized, "bus", bus);
        SetRef(bootSerialized, "journal", journal);
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

        // 2.6 seconds. Round-trip light time to the Moon is about 2.56s and Apollo 12
        // measured 2.712s on the day. Applied to voice AND keyboard, measured from when
        // the player finished, so neither route reaches the rover first.
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

        // Panel across the bottom.
        var panel = NewUiObject("Comms Panel", canvasObject.transform);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.offsetMin = new Vector2(48f, 40f);
        panelRect.offsetMax = new Vector2(-48f, 240f);

        var background = panel.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.65f);
        background.raycastTarget = false;

        var heard = NewText("Heard", panel.transform, 28, TextAnchor.LowerLeft);
        var heardRect = heard.GetComponent<RectTransform>();
        heardRect.anchorMin = new Vector2(0f, 0f);
        heardRect.anchorMax = new Vector2(1f, 1f);
        heardRect.offsetMin = new Vector2(28f, 74f);
        heardRect.offsetMax = new Vector2(-28f, -20f);

        var status = NewText("Status", panel.transform, 18, TextAnchor.LowerLeft);
        var statusRect = status.GetComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(0f, 0f);
        statusRect.anchorMax = new Vector2(1f, 0f);
        statusRect.offsetMin = new Vector2(28f, 34f);
        statusRect.offsetMax = new Vector2(-28f, 70f);

        // Signal bar. Fills left to right as the command crosses the gap. Not a spinner:
        // a spinner claims the software is busy, this claims the Moon is far away, and
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

        var comms = canvasObject.AddComponent<CommsDisplay>();
        var commsSerialized = new SerializedObject(comms);
        SetRef(commsSerialized, "heardText", heard);
        SetRef(commsSerialized, "statusText", status);
        SetRef(commsSerialized, "background", background);
        SetRef(commsSerialized, "signalBar", barImage);
        commsSerialized.ApplyModifiedPropertiesWithoutUndo();

        return comms;
    }

    private static GameObject NewUiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
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
