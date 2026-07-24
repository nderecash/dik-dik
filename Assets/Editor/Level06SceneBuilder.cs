using System.Collections.Generic;
using System.IO;
using Dikdik.Commands;
using Dikdik.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Generates Level 6, the crater rim. The ending.
///
/// Guidelines: "Provide subtitles for all important speech" and "present them in a
/// clear, easy to read way".
///
/// <para>The rover climbs to the rim for line of sight, the dormant rovers come into
/// view on the plain below, and Control hands the player the open loop. What broadcasts
/// is the player's own voice, every command they gave across the whole game, in order,
/// unedited, and the other rovers wake to it. The argument lands without being spoken:
/// the thing that reaches everyone else was never translated into anything.</para>
///
/// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod Level06SceneBuilder.Generate
/// </summary>
public static class Level06SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Level06.unity";
    private const string MaterialFolder = "Assets/Materials";

    private const float HalfWidth = 5f;
    private const float WallHeight = 3.4f;
    private const float RimZ = 30f;

    [MenuItem("Dikdik/Generate Level 06")]
    public static void Generate()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var wallMaterial = Environment.LitMaterial("LunarRock", new Color(0.34f, 0.32f, 0.30f));
        var groundMaterial = Environment.LitMaterial("LunarGround", new Color(0.42f, 0.40f, 0.37f));
        var shellMaterial = MakeMaterial("RoverShell", "Unlit/Color", new Color(0.85f, 0.88f, 1f));
        var dormantMaterial = MakeMaterial("DormantShell", "Unlit/Color", new Color(0.02f, 0.02f, 0.03f));

        // Ground stretches out over the plain beyond the rim, so the dormant field sits
        // on it in view.
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -0.5f, RimZ + 20f);
        ground.transform.localScale = new Vector3(80f, 1f, RimZ + 90f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

        // Climb corridor up to the rim. Walls stop at the rim so the plain opens up.
        var walls = new GameObject("Walls");
        foreach (var side in new[] { 1f, -1f })
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = side > 0 ? "Wall L" : "Wall R";
            wall.transform.SetParent(walls.transform);
            wall.transform.position = new Vector3(side * HalfWidth, WallHeight * 0.5f, RimZ * 0.5f);
            wall.transform.localScale = new Vector3(1.2f, WallHeight, RimZ);
            wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
        }

        var rover = BuildRover("Salty", wallMaterial, shellMaterial, out var controller,
                               out var roverLight, hasController: true);

        // Level plumbing.
        var directorObject = new GameObject("Level Director");
        var simulation = directorObject.AddComponent<SimulationReset>();
        var director = directorObject.AddComponent<LevelDirector>();

        var simulationSerialized = new SerializedObject(simulation);
        SetRef(simulationSerialized, "rover", controller);
        simulationSerialized.ApplyModifiedPropertiesWithoutUndo();

        // The dormant rovers on the plain, a loose grid beyond the rim.
        var field = new GameObject("Dormant Field");
        var dormant = new List<DormantRover>();
        var rng = new System.Random(6);

        for (var row = 0; row < 3; row++)
        {
            for (var col = 0; col < 4; col++)
            {
                var x = (col - 1.5f) * 9f + (float)(rng.NextDouble() * 3 - 1.5);
                var z = RimZ + 12f + row * 12f + (float)(rng.NextDouble() * 4 - 2);
                var yaw = (float)(rng.NextDouble() * 360);

                var d = BuildDormant(field.transform, new Vector3(x, 0.4f, z), yaw,
                                     dormantMaterial, out var comp);
                dormant.Add(comp);
            }
        }

        // Ending trigger at the rim.
        var ending = new GameObject("Ending");
        ending.transform.position = new Vector3(0f, WallHeight * 0.5f, RimZ - 2f);
        var endTrigger = ending.AddComponent<BoxCollider>();
        endTrigger.isTrigger = true;
        endTrigger.size = new Vector3(HalfWidth * 2f, WallHeight + 2f, 3f);

        var broadcastSource = ending.AddComponent<AudioSource>();
        broadcastSource.playOnAwake = false;
        broadcastSource.spatialBlend = 0f;
        broadcastSource.volume = 1f;

        var sequence = ending.AddComponent<EndingSequence>();
        var sequenceSerialized = new SerializedObject(sequence);
        SetRef(sequenceSerialized, "director", director);
        SetRef(sequenceSerialized, "broadcastSource", broadcastSource);
        var dormantProp = sequenceSerialized.FindProperty("dormant");
        dormantProp.arraySize = dormant.Count;
        for (var i = 0; i < dormant.Count; i++)
            dormantProp.GetArrayElementAtIndex(i).objectReferenceValue = dormant[i];
        sequenceSerialized.ApplyModifiedPropertiesWithoutUndo();

        // Camera. Sits higher and looks well ahead, so cresting the rim reveals the plain.
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 60f;
        camera.farClipPlane = 400f;
        cameraObject.AddComponent<AudioListener>();

        var follow = cameraObject.AddComponent<CameraFollow>();
        var followSerialized = new SerializedObject(follow);
        SetRef(followSerialized, "target", rover.transform);
        followSerialized.ApplyModifiedPropertiesWithoutUndo();

        // Dusk over the plain. A low sun so the dormant field beyond the rim reads as
        // shapes on lit ground, and the field itself is the horizon interest, so no ridge
        // ring here. The waking lights do the rest.
        Environment.ApplyLighting("dusk", new Color(0.62f, 0.6f, 0.62f), new Color(0.04f, 0.05f, 0.11f),
                                  sunIntensity: 0.7f);

        // Director settings.
        var directorSerialized = new SerializedObject(director);
        directorSerialized.FindProperty("levelNumber").intValue = 6;
        directorSerialized.FindProperty("levelName").stringValue = "The crater rim";
        directorSerialized.FindProperty("guideline").stringValue =
            "Provide subtitles for all important speech";
        directorSerialized.FindProperty("nextSceneName").stringValue = "";
        SetRef(directorSerialized, "simulation", simulation);

        var allowed = directorSerialized.FindProperty("allowedIntents");
        allowed.arraySize = 5;
        allowed.GetArrayElementAtIndex(0).enumValueIndex = (int)IntentId.Go;
        allowed.GetArrayElementAtIndex(1).enumValueIndex = (int)IntentId.Stop;
        allowed.GetArrayElementAtIndex(2).enumValueIndex = (int)IntentId.Left;
        allowed.GetArrayElementAtIndex(3).enumValueIndex = (int)IntentId.Right;
        allowed.GetArrayElementAtIndex(4).enumValueIndex = (int)IntentId.Wake;
        directorSerialized.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Level06SceneBuilder] Wrote {ScenePath}, rim at {RimZ:0}, " +
                  $"{dormant.Count} dormant rovers");
    }

    private static GameObject BuildRover(string name, Material bodyMaterial, Material shellMaterial,
                                         out RoverController controller, out RoverLight roverLight,
                                         bool hasController)
    {
        var rover = new GameObject(name);
        rover.transform.position = new Vector3(0f, 0.4f, 0f);

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(rover.transform, false);
        body.transform.localScale = new Vector3(1.1f, 0.6f, 1.6f);
        body.GetComponent<Renderer>().sharedMaterial = bodyMaterial;
        body.SetActive(false);   // replaced by the Kenney rover model below
        Object.DestroyImmediate(body.GetComponent<Collider>());

        Environment.AttachRoverModel(rover.transform, null);

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

        rover.AddComponent<AudioSource>().playOnAwake = false;

        var lightSerialized = new SerializedObject(roverLight);
        SetRef(lightSerialized, "rover", controller);
        SetRef(lightSerialized, "shell", shellRenderer);
        lightSerialized.ApplyModifiedPropertiesWithoutUndo();

        return rover;
    }

    private static GameObject BuildDormant(Transform parent, Vector3 position, float yaw,
                                           Material shellMaterial, out DormantRover component)
    {
        var rover = new GameObject("Dormant");
        rover.transform.SetParent(parent);
        rover.transform.position = position;
        rover.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // The dormant rover's body is a dark Kenney rover, so the plain is dotted with
        // the same machine as Salty, unlit until the broadcast wakes each one.
        var dark = Environment.LitMaterial("DormantBody", new Color(0.06f, 0.06f, 0.08f));
        Environment.AttachRoverModel(rover.transform, dark);

        var shell = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shell.transform.SetParent(rover.transform, false);
        shell.transform.localPosition = new Vector3(0f, 0.34f, 0.1f);
        shell.transform.localScale = new Vector3(0.85f, 0.12f, 0.9f);
        var shellRenderer = shell.GetComponent<Renderer>();
        // Own material instance so it can glow independently.
        shellRenderer.sharedMaterial = new Material(shellMaterial);
        Object.DestroyImmediate(shell.GetComponent<Collider>());

        var lampObject = new GameObject("Lamp");
        lampObject.transform.SetParent(rover.transform, false);
        lampObject.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        var lamp = lampObject.AddComponent<Light>();
        lamp.type = LightType.Point;
        lamp.range = 8f;
        lamp.intensity = 0f;
        lamp.color = new Color(0.75f, 0.85f, 1f);

        component = rover.AddComponent<DormantRover>();
        var serialized = new SerializedObject(component);
        SetRef(serialized, "shell", shellRenderer);
        SetRef(serialized, "lamp", lamp);
        serialized.ApplyModifiedPropertiesWithoutUndo();

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
        else Debug.LogWarning($"[Level06SceneBuilder] No field '{field}'");
    }
}
