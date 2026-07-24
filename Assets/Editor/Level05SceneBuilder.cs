using System.IO;
using Dikdik.Commands;
using Dikdik.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Generates Level 5, the slope.
///
/// Guideline: "Include an option to adjust the game speed."
///
/// <para>The rover is rolling downhill, so it coasts after you say stop. Your word also
/// takes 2.6 fixed seconds to arrive. Both together mean a "stop" has to be said well
/// before the mark, and you have to judge the coast. The game-speed slider is the answer:
/// slower game, the rover covers less ground during those fixed 2.6 seconds and sheds its
/// speed over less distance, so the overshoot shrinks. This is the only level where the
/// setting and the game's central mechanic explain each other, which is why it earns a
/// level of its own.</para>
///
/// <para>The grade is momentum, not tilted geometry. A real slope fights the rover's
/// yaw-only turning, so the visible tilt is an art-pass upgrade; the coast delivers the
/// mechanic now. Nothing can fail: overshoot a pad and roll on, or hit the wall past the
/// last one and reverse back up. It costs time, never a life.</para>
///
/// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod Level05SceneBuilder.Generate
/// </summary>
public static class Level05SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Level05.unity";
    private const string MaterialFolder = "Assets/Materials";

    private const float HalfWidth = 5f;
    private const float PadHalfLength = 2.6f;     // pads are generous, this is hard enough
    private const float WallThickness = 1.2f;

    // Stop pads down the run. The last has a wall just past it to catch a final overshoot.
    private static readonly float[] PadZ = { 14f, 30f, 46f };
    private const float RunEnd = 52f;

    [MenuItem("Dikdik/Generate Level 05")]
    public static void Generate()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var wallMaterial = MakeMaterial("LunarSilhouette", "Unlit/Color", Color.black);
        var groundMaterial = MakeMaterial("LunarGround", "Standard", new Color(0.20f, 0.19f, 0.18f));
        var shellMaterial = MakeMaterial("RoverShell", "Unlit/Color", new Color(0.85f, 0.88f, 1f));
        var padMaterial = MakeMaterial("StopPad", "Unlit/Color", new Color(0.7f, 0.55f, 0.2f));

        // ------------------------------------------------------------------
        // Ground and walls. Wall tops step down the run to suggest descent.
        // ------------------------------------------------------------------
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -0.5f, RunEnd * 0.5f);
        ground.transform.localScale = new Vector3(HalfWidth * 2f + 40f, 1f, RunEnd + 40f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

        var walls = new GameObject("Walls");
        foreach (var side in new[] { 1f, -1f })
        {
            // A few descending segments rather than one wall, so the silhouette steps
            // down toward the far end.
            var segments = 4;
            for (var s = 0; s < segments; s++)
            {
                var segLen = RunEnd / segments;
                var z = segLen * (s + 0.5f);
                var height = Mathf.Lerp(4.2f, 1.8f, s / (float)(segments - 1));

                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"Wall {(side > 0 ? "L" : "R")}{s}";
                wall.transform.SetParent(walls.transform);
                wall.transform.position = new Vector3(side * HalfWidth, height * 0.5f, z);
                wall.transform.localScale = new Vector3(WallThickness, height, segLen);
                wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
            }
        }

        // Backstop just past the last pad. Overshoot the bottom and you meet this and
        // reverse, rather than rolling into nothing.
        var backstop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backstop.name = "Backstop";
        backstop.transform.SetParent(walls.transform);
        backstop.transform.position = new Vector3(0f, 1.4f, RunEnd);
        backstop.transform.localScale = new Vector3(HalfWidth * 2f, 2.8f, 1.2f);
        backstop.GetComponent<Renderer>().sharedMaterial = wallMaterial;

        // ------------------------------------------------------------------
        // Rover, WITH momentum. This is the only level that sets these.
        // ------------------------------------------------------------------
        var rover = BuildRover(wallMaterial, shellMaterial, out var controller,
                               out var roverLight, out var audio);

        var roverSerialized = new SerializedObject(controller);
        // Reaches cruise in a bit over a second, and sheds it slowly: a low deceleration
        // is a long coast. Tuned so a full-speed stop overshoots by several metres and
        // half speed comfortably lands on a pad.
        roverSerialized.FindProperty("acceleration").floatValue = 2.2f;
        roverSerialized.FindProperty("deceleration").floatValue = 1.1f;
        roverSerialized.ApplyModifiedPropertiesWithoutUndo();

        // ------------------------------------------------------------------
        // Level plumbing
        // ------------------------------------------------------------------
        var directorObject = new GameObject("Level Director");
        var simulation = directorObject.AddComponent<SimulationReset>();
        var director = directorObject.AddComponent<LevelDirector>();

        var simulationSerialized = new SerializedObject(simulation);
        SetRef(simulationSerialized, "rover", controller);
        simulationSerialized.ApplyModifiedPropertiesWithoutUndo();

        // ------------------------------------------------------------------
        // Stop pads
        // ------------------------------------------------------------------
        var pingClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/junction_ping.wav");
        var padsParent = new GameObject("Stop Pads");
        var marks = new StopMark[PadZ.Length];

        for (var i = 0; i < PadZ.Length; i++)
        {
            var padObject = new GameObject($"Stop Pad {i + 1}");
            padObject.transform.SetParent(padsParent.transform);
            padObject.transform.position = new Vector3(0f, 0.5f, PadZ[i]);

            var trigger = padObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(HalfWidth * 2f, 3f, PadHalfLength * 2f);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Pad";
            visual.transform.SetParent(padObject.transform, false);
            visual.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            visual.transform.localScale = new Vector3(HalfWidth * 2f - 1f, 0.05f, PadHalfLength * 2f);
            var padRenderer = visual.GetComponent<Renderer>();
            padRenderer.sharedMaterial = padMaterial;
            Object.DestroyImmediate(visual.GetComponent<Collider>());

            var mark = padObject.AddComponent<StopMark>();
            var markSerialized = new SerializedObject(mark);
            SetRef(markSerialized, "pad", padRenderer);
            SetRef(markSerialized, "roverLight", roverLight);
            SetRef(markSerialized, "pingSource", audio);
            SetRef(markSerialized, "pingClip", pingClip);
            markSerialized.ApplyModifiedPropertiesWithoutUndo();

            marks[i] = mark;
        }

        var objective = directorObject.AddComponent<StopMarkObjective>();
        var objectiveSerialized = new SerializedObject(objective);
        SetRef(objectiveSerialized, "director", director);
        var marksProp = objectiveSerialized.FindProperty("marks");
        marksProp.arraySize = marks.Length;
        for (var i = 0; i < marks.Length; i++)
            marksProp.GetArrayElementAtIndex(i).objectReferenceValue = marks[i];
        objectiveSerialized.ApplyModifiedPropertiesWithoutUndo();

        // ------------------------------------------------------------------
        // Camera, pitched a touch steeper to read as looking down a grade
        // ------------------------------------------------------------------
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.72f, 0.76f, 0.82f);
        camera.fieldOfView = 58f;
        cameraObject.AddComponent<AudioListener>();

        var follow = cameraObject.AddComponent<CameraFollow>();
        var followSerialized = new SerializedObject(follow);
        SetRef(followSerialized, "target", rover.transform);
        followSerialized.FindProperty("offset").vector3Value = new Vector3(0f, 13f, -11f);
        followSerialized.FindProperty("lookAhead").floatValue = 9f;
        followSerialized.ApplyModifiedPropertiesWithoutUndo();

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.06f, 0.07f, 0.09f);
        RenderSettings.skybox = null;

        // ------------------------------------------------------------------
        // Director settings
        // ------------------------------------------------------------------
        var directorSerialized = new SerializedObject(director);
        directorSerialized.FindProperty("levelNumber").intValue = 5;
        directorSerialized.FindProperty("levelName").stringValue = "The slope";
        directorSerialized.FindProperty("guideline").stringValue =
            "Include an option to adjust the game speed";
        directorSerialized.FindProperty("nextSceneName").stringValue = "";
        SetRef(directorSerialized, "simulation", simulation);

        var allowed = directorSerialized.FindProperty("allowedIntents");
        allowed.arraySize = 4;
        allowed.GetArrayElementAtIndex(0).enumValueIndex = (int)IntentId.Go;
        allowed.GetArrayElementAtIndex(1).enumValueIndex = (int)IntentId.Stop;
        allowed.GetArrayElementAtIndex(2).enumValueIndex = (int)IntentId.Back;
        allowed.GetArrayElementAtIndex(3).enumValueIndex = (int)IntentId.Left;
        directorSerialized.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Level05SceneBuilder] Wrote {ScenePath}, {PadZ.Length} stop pads, " +
                  $"run {HalfWidth * 2:0}x{RunEnd:0}");
    }

    private static GameObject BuildRover(Material wallMaterial, Material shellMaterial,
                                         out RoverController controller,
                                         out RoverLight roverLight,
                                         out AudioSource audio)
    {
        var rover = new GameObject("Salty");
        rover.transform.position = new Vector3(0f, 0.4f, 0f);

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(rover.transform, false);
        body.transform.localScale = new Vector3(1.1f, 0.6f, 1.6f);
        body.GetComponent<Renderer>().sharedMaterial = wallMaterial;
        Object.DestroyImmediate(body.GetComponent<Collider>());

        var shell = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shell.name = "Shell Light";
        shell.transform.SetParent(rover.transform, false);
        shell.transform.localPosition = new Vector3(0f, 0.34f, 0.1f);
        shell.transform.localScale = new Vector3(0.85f, 0.12f, 0.9f);
        var shellRenderer = shell.GetComponent<Renderer>();
        shellRenderer.sharedMaterial = shellMaterial;
        Object.DestroyImmediate(shell.GetComponent<Collider>());

        var lampObject = new GameObject("Lamp");
        lampObject.transform.SetParent(rover.transform, false);
        lampObject.transform.localPosition = new Vector3(0f, 0.5f, 0.7f);
        lampObject.transform.localRotation = Quaternion.Euler(16f, 0f, 0f);

        var lamp = lampObject.AddComponent<Light>();
        lamp.type = LightType.Spot;
        lamp.range = 24f;
        lamp.spotAngle = 60f;
        lamp.intensity = 3f;
        lamp.color = new Color(0.85f, 0.88f, 1f);
        lamp.shadows = LightShadows.Soft;

        roverLight = lampObject.AddComponent<RoverLight>();
        controller = rover.AddComponent<RoverController>();

        var roverCollider = rover.AddComponent<CapsuleCollider>();
        roverCollider.height = 1.4f;
        roverCollider.radius = 0.7f;
        roverCollider.center = new Vector3(0f, 0.2f, 0f);

        var rigidbody = rover.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;

        audio = rover.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;

        var lightSerialized = new SerializedObject(roverLight);
        SetRef(lightSerialized, "rover", controller);
        SetRef(lightSerialized, "shell", shellRenderer);
        lightSerialized.ApplyModifiedPropertiesWithoutUndo();

        return rover;
    }

    private static Material MakeMaterial(string name, string shader, Color colour)
    {
        Directory.CreateDirectory(MaterialFolder);
        var path = $"{MaterialFolder}/{name}.mat";

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.color = colour;
            return existing;
        }

        var material = new Material(Shader.Find(shader)) { color = colour };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        var property = so.FindProperty(field);
        if (property != null) property.objectReferenceValue = value;
        else Debug.LogWarning($"[Level05SceneBuilder] No field '{field}'");
    }
}
