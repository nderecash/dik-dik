using System.Collections.Generic;
using System.IO;
using Dikdik.Commands;
using Dikdik.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Generates Level 4, the jammed key.
///
/// Guideline: "Allow controls to be remapped / reconfigured."
///
/// <para>A sealed door blocks the way, opened by the Open command. The console reports a
/// fault: the key bound to Open is stuck. A keyboard player has to bind Open to another
/// key, or teach Salty a new word for it. A voice player just says "open" and walks
/// through, which is the whole point: when one way in fails, the other carries you, and
/// here it is voice that keeps working rather than the flaky option it is usually cast
/// as.</para>
///
/// <para>The fault is the console's, never the player's. Nothing here implies they did
/// anything wrong, and the fix is one screen away.</para>
///
/// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod Level04SceneBuilder.Generate
/// </summary>
public static class Level04SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Level04.unity";
    private const string MaterialFolder = "Assets/Materials";

    private const float HalfWidth = 5f;
    private const float WallHeight = 3.4f;
    private const float DoorZ = 22f;
    private const float RunEnd = 38f;

    [MenuItem("Dikdik/Generate Level 04")]
    public static void Generate()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var wallMaterial = Environment.LitMaterial("LunarRock", new Color(0.34f, 0.32f, 0.30f));
        var groundMaterial = Environment.LitMaterial("LunarGround", new Color(0.42f, 0.40f, 0.37f));
        var shellMaterial = MakeMaterial("RoverShell", "Unlit/Color", new Color(0.85f, 0.88f, 1f));
        var doorMaterial = MakeMaterial("DoorIndicator", "Unlit/Color", new Color(0.7f, 0.2f, 0.2f));

        // Ground.
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -0.5f, RunEnd * 0.5f);
        ground.transform.localScale = new Vector3(HalfWidth * 2f + 40f, 1f, RunEnd + 40f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

        // Side walls, in two runs so the doorway reads as a break in them.
        var walls = new GameObject("Walls");
        foreach (var side in new[] { 1f, -1f })
        {
            MakeWall(walls.transform, new Vector3(side * HalfWidth, WallHeight * 0.5f, DoorZ * 0.5f),
                     new Vector3(1.2f, WallHeight, DoorZ), wallMaterial, $"Wall near {side}");
            var farStart = DoorZ + 2f;
            MakeWall(walls.transform,
                     new Vector3(side * HalfWidth, WallHeight * 0.5f, (farStart + RunEnd) * 0.5f),
                     new Vector3(1.2f, WallHeight, RunEnd - farStart), wallMaterial, $"Wall far {side}");
        }

        // Rover (instant, no momentum here).
        var rover = BuildRover(wallMaterial, shellMaterial, out var controller, out var roverLight);

        // Level plumbing.
        var directorObject = new GameObject("Level Director");
        var simulation = directorObject.AddComponent<SimulationReset>();
        var director = directorObject.AddComponent<LevelDirector>();

        var simulationSerialized = new SerializedObject(simulation);
        SetRef(simulationSerialized, "rover", controller);
        simulationSerialized.ApplyModifiedPropertiesWithoutUndo();

        // The door. A solid slab that slides up when opened. Its own colour is the
        // indicator: red closed, green open, so the state is never sound-only.
        var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.name = "Door";
        door.transform.position = new Vector3(0f, WallHeight * 0.5f, DoorZ);
        door.transform.localScale = new Vector3(HalfWidth * 2f, WallHeight, 0.6f);
        var doorRenderer = door.GetComponent<Renderer>();
        doorRenderer.sharedMaterial = doorMaterial;

        var interactable = door.AddComponent<InteractableDoor>();
        var doorSerialized = new SerializedObject(interactable);
        SetRef(doorSerialized, "rover", rover.transform);
        SetRef(doorSerialized, "indicator", doorRenderer);
        doorSerialized.FindProperty("reach").floatValue = 8f;
        // Down, into the floor, not up. The follow camera sits at y 6.9 and the door used
        // to rise into y 3.9-7.3, so opening it drove a green slab through the lens.
        doorSerialized.FindProperty("openOffset").vector3Value = new Vector3(0f, -(WallHeight + 0.5f), 0f);
        doorSerialized.ApplyModifiedPropertiesWithoutUndo();

        // The jam and the level's spoken intro.
        var systems = new GameObject("Level Systems");
        var jam = systems.AddComponent<KeyJam>();
        var jamSerialized = new SerializedObject(jam);
        jamSerialized.FindProperty("jammedIntent").enumValueIndex = (int)IntentId.Open;
        jamSerialized.ApplyModifiedPropertiesWithoutUndo();

        var intro = systems.AddComponent<LevelIntroVoice>();
        var introSerialized = new SerializedObject(intro);
        introSerialized.FindProperty("clipName").stringValue = "sup_sector_04";
        introSerialized.FindProperty("group").stringValue = "console";
        introSerialized.ApplyModifiedPropertiesWithoutUndo();

        // Exit past the door.
        var exit = new GameObject("Exit");
        exit.transform.position = new Vector3(0f, 0.5f, RunEnd - 3f);
        var exitTrigger = exit.AddComponent<BoxCollider>();
        exitTrigger.isTrigger = true;
        exitTrigger.size = new Vector3(HalfWidth * 2f, 4f, 3f);

        var exitZone = exit.AddComponent<LevelExit>();
        var exitSerialized = new SerializedObject(exitZone);
        SetRef(exitSerialized, "director", director);
        exitSerialized.ApplyModifiedPropertiesWithoutUndo();

        // The relay line, running under the door to the exit. Three checkpoints, so one
        // falls on each side of the door and the jammed key never blocks a scan.
        var cableCorners = new List<Vector3> { Vector3.zero, new Vector3(0f, 0f, RunEnd - 3f) };
        var cable = CableBuilder.Build(cableCorners, 3, false, controller, roverLight,
                                       rover.GetComponent<AudioSource>());
        var mission = CableBuilder.AddMission(cable, controller, roverLight, director);
        CableBuilder.AddHud(mission, controller, director);

        // Tire sound, brake tick, turning wheels, and the attention reflex.
        CableBuilder.AddRoverCharacter(rover, controller);
        CableBuilder.AddWeather();

        // Camera.
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 56f;
        camera.farClipPlane = 400f;
        cameraObject.AddComponent<AudioListener>();

        var follow = cameraObject.AddComponent<CameraFollow>();
        var followSerialized = new SerializedObject(follow);
        SetRef(followSerialized, "target", rover.transform);
        followSerialized.ApplyModifiedPropertiesWithoutUndo();

        Environment.ApplyLighting("dusk", new Color(0.62f, 0.6f, 0.62f), new Color(0.04f, 0.05f, 0.11f));
        Environment.BuildHorizon(new Vector3(0f, 0f, RunEnd * 0.5f), 130f, 4);
        Environment.ScatterKenneyRocks(new Vector3(0f, 0f, RunEnd * 0.5f),
                                       42f, RunEnd * 0.6f + 20f, HalfWidth + 3f, 35, 4);
        Environment.PlaceModel("satelliteDish", new Vector3(-18f, 0f, 8f), 40f, 5f);
        Environment.PlaceModel("hangar_smallA", new Vector3(20f, 0f, RunEnd - 4f), -110f, 6f);
        Environment.ScatterRocks(new Vector3(0f, 0f, RunEnd * 0.5f),
                                 40f, RunEnd * 0.6f + 20f, HalfWidth + 2f, 50, 4);

        // Director settings.
        var directorSerialized = new SerializedObject(director);
        directorSerialized.FindProperty("levelNumber").intValue = 4;
        directorSerialized.FindProperty("levelName").stringValue = "The jammed key";
        directorSerialized.FindProperty("guideline").stringValue =
            "Allow controls to be remapped / reconfigured";
        directorSerialized.FindProperty("nextSceneName").stringValue = "Level05";
        SetRef(directorSerialized, "simulation", simulation);

        var allowed = directorSerialized.FindProperty("allowedIntents");
        allowed.arraySize = 6;
        allowed.GetArrayElementAtIndex(0).enumValueIndex = (int)IntentId.Go;
        allowed.GetArrayElementAtIndex(1).enumValueIndex = (int)IntentId.Stop;
        allowed.GetArrayElementAtIndex(2).enumValueIndex = (int)IntentId.Left;
        allowed.GetArrayElementAtIndex(3).enumValueIndex = (int)IntentId.Right;
        allowed.GetArrayElementAtIndex(4).enumValueIndex = (int)IntentId.Back;
        allowed.GetArrayElementAtIndex(5).enumValueIndex = (int)IntentId.Open;
        directorSerialized.ApplyModifiedPropertiesWithoutUndo();

        // A cable that crosses a hazard is a level that cannot be played as
        // instructed. Checked here rather than found by driving down it.
        CableBuilder.AssertCableIsClear(cable);

        // Put back any props moved by hand and saved. Does nothing until a layout
        // exists for this level, so procedural scatter stays the default.
        PropLayout.TryApply(4);

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Level04SceneBuilder] Wrote {ScenePath}, door at {DoorZ:0}, " +
                  $"Open key jammed, run {HalfWidth * 2:0}x{RunEnd:0}");
    }

    private static GameObject BuildRover(Material wallMaterial, Material shellMaterial,
                                         out RoverController controller, out RoverLight roverLight)
    {
        var rover = new GameObject("Salty");
        rover.transform.position = new Vector3(0f, 0.4f, 0f);

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(rover.transform, false);
        body.transform.localScale = new Vector3(1.1f, 0.6f, 1.6f);
        body.GetComponent<Renderer>().sharedMaterial = wallMaterial;
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

        var audio = rover.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;

        var lightSerialized = new SerializedObject(roverLight);
        SetRef(lightSerialized, "rover", controller);
        SetRef(lightSerialized, "shell", shellRenderer);
        lightSerialized.ApplyModifiedPropertiesWithoutUndo();

        return rover;
    }

    private static void MakeWall(Transform parent, Vector3 position, Vector3 scale,
                                 Material material, string name)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent);
        wall.transform.position = position;
        wall.transform.localScale = scale;
        wall.GetComponent<Renderer>().sharedMaterial = material;
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
        else Debug.LogWarning($"[Level04SceneBuilder] No field '{field}'");
    }
}
