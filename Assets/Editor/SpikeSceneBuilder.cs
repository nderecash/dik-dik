using System.IO;
using Dikdik.Commands;
using Dikdik.Producers;
using Dikdik.Spike;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Whisper;
using Whisper.Utils;

/// <summary>
/// Builds the spike scene and the Windows player from the command line.
///
/// Everything here exists because the scene has to be reproducible. A scene clicked
/// together by hand is a binary blob nobody can review. This is a script, so the
/// exact microphone and whisper settings used for the go/no-go gate are readable
/// in the diff, and anyone can regenerate the identical measuring instrument.
/// </summary>
public static class SpikeSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Spike.unity";
    private const string BuildPath = "Builds/Spike/DikdikSpike.exe";

    [MenuItem("Dikdik/Generate Spike Scene")]
    public static void GenerateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera. The spike draws with OnGUI, but a scene with no camera logs
        // warnings that clutter the very log we are trying to read.
        var cameraObject = new GameObject("Main Camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.06f, 0.07f, 0.09f);
        cameraObject.tag = "MainCamera";

        // Command bus.
        var busObject = new GameObject("CommandBus");
        var bus = busObject.AddComponent<CommandBus>();

        // Whisper and microphone.
        var whisperObject = new GameObject("Whisper");
        var whisper = whisperObject.AddComponent<WhisperManager>();
        var microphone = whisperObject.AddComponent<MicrophoneRecord>();

        var whisperSerialized = new SerializedObject(whisper);
        Set(whisperSerialized, "modelPath", "Whisper/ggml-tiny.bin");
        SetBool(whisperSerialized, "isModelPathInStreamingAssets", true);
        SetBool(whisperSerialized, "initOnAwake", true);
        SetBool(whisperSerialized, "useGpu", false);           // CPU baseline, per the brief
        SetBool(whisperSerialized, "flashAttention", false);   // known to slow things down, issue #118
        whisperSerialized.ApplyModifiedPropertiesWithoutUndo();

        whisper.language = "en";
        whisper.noContext = true;
        whisper.singleSegment = true;   // one short command per clip
        whisper.initialPrompt = "";     // baseline. Seeding the vocabulary is a lever we hold in reserve.

        // Microphone. 16 kHz is what whisper wants, so no resampling.
        microphone.frequency = 16000;
        microphone.echo = false;        // playing the player's voice back at them is irritating
        microphone.useVad = true;
        microphone.vadStop = true;
        microphone.vadStopTime = 1.5f;  // end of utterance on silence, generous enough for slow speech
        microphone.dropVadPart = true;
        microphone.maxLengthSec = 30;

        // Producers.
        var producersObject = new GameObject("Producers");
        var voice = producersObject.AddComponent<VoiceCommandProducer>();

        // No KeyboardCommandProducer in this scene. It belongs in the game, where it is
        // half the argument, but this scene exists to measure speech recognition and a
        // keyboard producer in it can only contaminate the result. It did exactly that
        // in run two: the arm key doubled as the Stop binding and answered every task
        // before the microphone opened.

        var voiceSerialized = new SerializedObject(voice);
        SetRef(voiceSerialized, "whisper", whisper);
        SetRef(voiceSerialized, "microphone", microphone);
        // Off. The runner opens the microphone only when the tester presses a key,
        // so reading the task cannot cost you a task. Version one left this on and
        // recorded eight rows of silence while the tester worked out what to do.
        SetBool(voiceSerialized, "continuousListening", false);
        voiceSerialized.ApplyModifiedPropertiesWithoutUndo();

        // Spike harness.
        var spikeObject = new GameObject("Spike");
        var logger = spikeObject.AddComponent<SpikeLogger>();
        var runner = spikeObject.AddComponent<SpikeRunner>();

        var runnerSerialized = new SerializedObject(runner);
        SetRef(runnerSerialized, "logger", logger);
        SetRef(runnerSerialized, "voice", voice);
        runnerSerialized.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[SpikeSceneBuilder] Scene written to {ScenePath}");
    }

    [MenuItem("Dikdik/Build Spike (Windows)")]
    public static void BuildWindows()
    {
        PlayerSettings.companyName = "Moses Nderemani";
        PlayerSettings.productName = "Dikdik Spike";
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1100;
        PlayerSettings.defaultScreenHeight = 700;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;

        // Mono, not IL2CPP. The spike is a measuring instrument, not a shipping build,
        // and Mono builds in a fraction of the time.
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = BuildPath,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        Debug.Log($"[SpikeSceneBuilder] Build result: {summary.result}, " +
                  $"{summary.totalErrors} errors, {summary.totalWarnings} warnings, " +
                  $"output {summary.outputPath}");

        if (summary.result != BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
            return;
        }

        EditorApplication.Exit(0);
    }

    /// <summary>Generate then build, so one batch mode invocation does the whole job.</summary>
    public static void GenerateAndBuild()
    {
        GenerateScene();
        BuildWindows();
    }

    private static void Set(SerializedObject so, string field, string value)
    {
        var property = so.FindProperty(field);
        if (property != null) property.stringValue = value;
        else Debug.LogWarning($"[SpikeSceneBuilder] No field '{field}'");
    }

    private static void SetBool(SerializedObject so, string field, bool value)
    {
        var property = so.FindProperty(field);
        if (property != null) property.boolValue = value;
        else Debug.LogWarning($"[SpikeSceneBuilder] No field '{field}'");
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        var property = so.FindProperty(field);
        if (property != null) property.objectReferenceValue = value;
        else Debug.LogWarning($"[SpikeSceneBuilder] No field '{field}'");
    }
}
