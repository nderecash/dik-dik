using System.IO;
using Dikdik.Commands;
using Dikdik.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Generates Level 2, two supervisors.
///
/// Guideline: "Use simple clear language."
///
/// <para>The station reads each procedure in dense jargon, in a synthetic voice, and the
/// human supervisor immediately translates it into a plain instruction. Hearing the two
/// back to back is the whole point: the machine talks like a manual and is useless, the
/// person talks plainly and you can act. The player only ever acts on the plain version;
/// the jargon is never required, which is the honest way to argue for clear language
/// rather than testing whether you can decode it.</para>
///
/// Four beats, in order: open a door, turn left, keep going, turn right.
///
/// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod Level02SceneBuilder.Generate
/// </summary>
public static class Level02SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Level02.unity";
    private const string MaterialFolder = "Assets/Materials";

    private const float HalfWidth = 3.5f;
    private const float WallHeight = 3.2f;
    private const float WallThickness = 1.2f;
    private const float JunctionGap = 5f;

    // Corridor as turns: north to the door, on to the left turn, west, then the right
    // turn and out. The four beats sit one before each action.
    private static readonly (float Heading, float Length)[] Path =
    {
        (0f, 26f),     // north, past the door, to the first junction
        (270f, 18f),   // turn left, head west
        (0f, 14f)      // turn right, north to the exit
    };

    [MenuItem("Dikdik/Generate Level 02")]
    public static void Generate()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var wallMaterial = Environment.LitMaterial("LunarRock", new Color(0.34f, 0.32f, 0.30f));
        var groundMaterial = Environment.LitMaterial("LunarGround", new Color(0.42f, 0.40f, 0.37f));
        var shellMaterial = MakeMaterial("RoverShell", "Unlit/Color", new Color(0.85f, 0.88f, 1f));
        var doorMaterial = MakeMaterial("DoorIndicator", "Unlit/Color", new Color(0.7f, 0.2f, 0.2f));

        // Corners from the path.
        var corners = new System.Collections.Generic.List<Vector3> { Vector3.zero };
        var position = Vector3.zero;
        foreach (var (heading, length) in Path)
        {
            var dir = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
            position += dir * length;
            corners.Add(position);
        }

        var bounds = new Bounds(corners[0], Vector3.zero);
        foreach (var c in corners) bounds.Encapsulate(c);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(bounds.center.x, -0.5f, bounds.center.z);
        ground.transform.localScale = new Vector3(bounds.size.x + 60f, 1f, bounds.size.z + 60f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

        var walls = new GameObject("Walls");
        for (var i = 0; i < corners.Count - 1; i++)
        {
            var from = corners[i];
            var to = corners[i + 1];
            var dir = (to - from).normalized;
            var perp = Vector3.Cross(Vector3.up, dir);

            var startInset = i == 0 ? 0f : JunctionGap;
            var endInset = i == corners.Count - 2 ? 0f : JunctionGap;
            var a = from + dir * startInset;
            var b = to - dir * endInset;
            if ((b - a).magnitude < 0.5f) continue;

            MakeWall(walls.transform, a + perp * HalfWidth, b + perp * HalfWidth, wallMaterial, $"W{i}L");
            MakeWall(walls.transform, a - perp * HalfWidth, b - perp * HalfWidth, wallMaterial, $"W{i}R");
        }

        // Rover.
        var rover = new GameObject("Salty");
        rover.transform.position = new Vector3(0f, 0.4f, 0f);
        Environment.AttachRoverModel(rover.transform, null);

        var shell = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shell.name = "Shell Light";
        shell.transform.SetParent(rover.transform, false);
        shell.transform.localPosition = new Vector3(0f, 0.34f, 0.1f);
        shell.transform.localScale = new Vector3(0.85f, 0.12f, 0.9f);
        shell.GetComponent<Renderer>().sharedMaterial = shellMaterial;
        Object.DestroyImmediate(shell.GetComponent<Collider>());

        var lampObject = new GameObject("Lamp");
        lampObject.transform.SetParent(rover.transform, false);
        lampObject.transform.localPosition = new Vector3(0f, 0.5f, 0.7f);
        lampObject.transform.localRotation = Quaternion.Euler(16f, 0f, 0f);
        var lamp = lampObject.AddComponent<Light>();
        lamp.type = LightType.Spot;
        lamp.range = 22f; lamp.spotAngle = 62f; lamp.intensity = 3f;
        lamp.color = new Color(0.85f, 0.88f, 1f); lamp.shadows = LightShadows.Soft;
        var roverLight = lampObject.AddComponent<RoverLight>();
        var controller = rover.AddComponent<RoverController>();

        var roverCollider = rover.AddComponent<CapsuleCollider>();
        roverCollider.height = 1.4f; roverCollider.radius = 0.7f;
        roverCollider.center = new Vector3(0f, 0.2f, 0f);
        var rb = rover.AddComponent<Rigidbody>();
        rb.isKinematic = true; rb.useGravity = false;
        var audio = rover.AddComponent<AudioSource>();
        audio.playOnAwake = false; audio.spatialBlend = 0f;

        var lightSer = new SerializedObject(roverLight);
        SetRef(lightSer, "rover", controller);
        lightSer.ApplyModifiedPropertiesWithoutUndo();

        // No station AudioSource any more. The station used to speak through its own
        // source with nothing coordinating it against the supervisor, which is how three
        // voices ended up talking at once on this level. Everything spoken now goes
        // through the one arbiter in the Boot scene.

        // Level plumbing.
        var directorObject = new GameObject("Level Director");
        var simulation = directorObject.AddComponent<SimulationReset>();
        var director = directorObject.AddComponent<LevelDirector>();
        var simSer = new SerializedObject(simulation);
        SetRef(simSer, "rover", controller);
        simSer.ApplyModifiedPropertiesWithoutUndo();

        var pingClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/junction_ping.wav");

        // Beat 1: the door. "Actuate the primary egress barrier..." -> "Open the door."
        var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
        door.name = "Door";
        door.transform.position = new Vector3(0f, WallHeight * 0.5f, 13f);
        door.transform.localScale = new Vector3(HalfWidth * 2f, WallHeight, 0.6f);
        var doorRenderer = door.GetComponent<Renderer>();
        doorRenderer.sharedMaterial = doorMaterial;
        var interactable = door.AddComponent<InteractableDoor>();
        var doorSer = new SerializedObject(interactable);
        SetRef(doorSer, "rover", rover.transform);
        SetRef(doorSer, "indicator", doorRenderer);
        doorSer.FindProperty("reach").floatValue = 8f;
        doorSer.FindProperty("openOffset").vector3Value = new Vector3(0f, WallHeight + 0.5f, 0f);
        doorSer.ApplyModifiedPropertiesWithoutUndo();

        MakeBeat("Beat Open", new Vector3(0f, 1f, 6f), "station_01",
                 "Actuate the primary egress barrier via the manual override subsystem.");

        // Beats 2 and 4 are the turns; use junctions so the rover stops for the choice.
        MakeJunction("Junction Left", corners[1], 1, roverLight, audio, pingClip);
        MakeBeat("Beat Left", corners[1] + new Vector3(0f, 0.6f, -4f), "station_02",
                 "Execute a ninety degree rotational adjustment about the port axis.");

        // Beat 3: keep going, on the west stretch.
        var westMid = Vector3.Lerp(corners[1], corners[2], 0.5f);
        MakeBeat("Beat Forward", westMid + new Vector3(0f, 0.6f, 0f), "station_03",
                 "Maintain forward translational momentum along the current heading vector, disregarding prior directives.");

        MakeJunction("Junction Right", corners[2], 2, roverLight, audio, pingClip);
        MakeBeat("Beat Right", corners[2] + new Vector3(4f, 0.6f, 0f), "station_04",
                 "Perform a starboard oriented directional realignment of ninety degrees, authorization pending.");

        // Exit.
        var exit = new GameObject("Exit");
        exit.transform.position = corners[corners.Count - 1] + Vector3.up * 0.5f;
        var exitTrigger = exit.AddComponent<BoxCollider>();
        exitTrigger.isTrigger = true;
        exitTrigger.size = new Vector3(HalfWidth * 2f, 4f, 3f);
        var exitZone = exit.AddComponent<LevelExit>();
        var exitSer = new SerializedObject(exitZone);
        SetRef(exitSer, "director", director);
        exitSer.ApplyModifiedPropertiesWithoutUndo();

        // Camera and environment.
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 55f; camera.farClipPlane = 400f;
        cameraObject.AddComponent<AudioListener>();
        var follow = cameraObject.AddComponent<CameraFollow>();
        var followSer = new SerializedObject(follow);
        SetRef(followSer, "target", rover.transform);
        followSer.ApplyModifiedPropertiesWithoutUndo();

        Environment.ApplyLighting("dusk", new Color(0.62f, 0.6f, 0.62f), new Color(0.04f, 0.05f, 0.11f));
        Environment.BuildHorizon(new Vector3(bounds.center.x, 0f, bounds.center.z), 130f, 2);
        Environment.ScatterKenneyRocks(new Vector3(bounds.center.x, 0f, bounds.center.z),
                                       42f, bounds.size.z * 0.7f + 20f, HalfWidth + 4f, 35, 2);
        Environment.PlaceModel("hangar_smallB", new Vector3(20f, 0f, 10f), -120f, 6f);
        Environment.PlaceModel("satelliteDish_large", new Vector3(-22f, 0f, 20f), 40f, 5f);

        var directorSer = new SerializedObject(director);
        directorSer.FindProperty("levelNumber").intValue = 2;
        directorSer.FindProperty("levelName").stringValue = "Two supervisors";
        directorSer.FindProperty("guideline").stringValue = "Use simple clear language";
        directorSer.FindProperty("nextSceneName").stringValue = "";
        SetRef(directorSer, "simulation", simulation);
        var allowed = directorSer.FindProperty("allowedIntents");
        allowed.arraySize = 5;
        allowed.GetArrayElementAtIndex(0).enumValueIndex = (int)IntentId.Go;
        allowed.GetArrayElementAtIndex(1).enumValueIndex = (int)IntentId.Stop;
        allowed.GetArrayElementAtIndex(2).enumValueIndex = (int)IntentId.Left;
        allowed.GetArrayElementAtIndex(3).enumValueIndex = (int)IntentId.Right;
        allowed.GetArrayElementAtIndex(4).enumValueIndex = (int)IntentId.Open;
        directorSer.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Level02SceneBuilder] Wrote {ScenePath}, 4 beats, corridor to {corners[corners.Count - 1]}");
    }

    private static void MakeBeat(string name, Vector3 pos, string clip, string jargon)
    {
        var beat = new GameObject(name);
        beat.transform.position = pos;
        var trigger = beat.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(HalfWidth * 2f, 3f, 2.5f);

        var comp = beat.AddComponent<Level2Beat>();
        var ser = new SerializedObject(comp);
        ser.FindProperty("stationClip").stringValue = clip;
        ser.FindProperty("jargon").stringValue = jargon;
        ser.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void MakeJunction(string name, Vector3 corner, int index, RoverLight roverLight,
                                     AudioSource audio, AudioClip pingClip)
    {
        var junctionObject = new GameObject(name);
        junctionObject.transform.position = corner + Vector3.up * 0.5f;
        var trigger = junctionObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(3f, 3f, 3f);

        var junction = junctionObject.AddComponent<Junction>();
        var ser = new SerializedObject(junction);
        ser.FindProperty("index").intValue = index;
        SetRef(ser, "roverLight", roverLight);
        SetRef(ser, "pingSource", audio);
        SetRef(ser, "pingClip", pingClip);
        ser.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void MakeWall(Transform parent, Vector3 from, Vector3 to, Material material, string name)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent);
        var middle = (from + to) * 0.5f;
        wall.transform.position = new Vector3(middle.x, WallHeight * 0.5f, middle.z);
        wall.transform.rotation = Quaternion.LookRotation((to - from).normalized, Vector3.up);
        wall.transform.localScale = new Vector3(WallThickness, WallHeight, (to - from).magnitude);
        wall.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static Material MakeMaterial(string name, string shader, Color colour)
    {
        Directory.CreateDirectory(MaterialFolder);
        var path = $"{MaterialFolder}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) { existing.color = colour; return existing; }
        var material = new Material(Shader.Find(shader)) { color = colour };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        var property = so.FindProperty(field);
        if (property != null) property.objectReferenceValue = value;
        else Debug.LogWarning($"[Level02SceneBuilder] No field '{field}'");
    }
}
