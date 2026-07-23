using System.Collections.Generic;
using System.IO;
using Dikdik.Commands;
using Dikdik.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Generates Level 1, the dust corridor.
///
/// Guideline: "Ensure no essential information is conveyed by sounds alone."
///
/// Built from primitives on purpose. The art direction is hard silhouette, which means
/// geometry reads as shape rather than as detail, so boxes are not a placeholder for
/// the real thing here, they are the real thing. Kenney assets can add detail later
/// without any of this changing.
///
/// Two materials carry the whole look:
///   walls  unlit black, so they are pure silhouette against a pale sky
///   ground lit, so Salty's lamp actually pools on it and the lamp means something
///
/// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod Level01SceneBuilder.Generate
/// </summary>
public static class Level01SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Level01.unity";
    private const string MaterialFolder = "Assets/Materials";

    // Tuned after the first playtest. The corridor was too tight and the walls too
    // tall: turns needed precision the transport delay cannot deliver, and the camera
    // spent half of every corner staring at a wall face.
    //
    // Precision under latency is Level 5's job. This level teaches the loop and should
    // forgive everything.
    private const float CorridorHalfWidth = 5f;     // was 3.5
    private const float WallHeight = 3.2f;          // was 5, camera now sees over them
    private const float WallThickness = 1.2f;
    private const float JunctionGap = 5.5f;         // was 4, wider openings

    /// <summary>
    /// The corridor, as turns. Each entry is a heading in degrees and a length.
    /// Readable, diffable, and trivially adjustable after the first playtest, which
    /// is the whole reason the level is a script and not a hand-built scene.
    /// </summary>
    private static readonly (float Heading, float Length)[] Path =
    {
        (0f, 16f),    // north, out of the start
        (90f, 14f),   // right
        (0f, 14f),    // left, north again
        (270f, 12f)   // left, west to the exit
    };

    [MenuItem("Dikdik/Generate Level 01")]
    public static void Generate()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var wallMaterial = MakeMaterial("LunarSilhouette", "Unlit/Color", Color.black);
        var groundMaterial = MakeMaterial("LunarGround", "Standard", new Color(0.20f, 0.19f, 0.18f));
        var shellMaterial = MakeMaterial("RoverShell", "Unlit/Color", new Color(0.85f, 0.88f, 1f));
        var markerMaterial = MakeMaterial("JunctionMarker", "Unlit/Color", new Color(0.15f, 0.16f, 0.2f));

        // ------------------------------------------------------------------
        // Work out the corridor
        // ------------------------------------------------------------------
        var corners = new List<Vector3> { Vector3.zero };
        var position = Vector3.zero;

        foreach (var (heading, length) in Path)
        {
            var direction = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
            position += direction * length;
            corners.Add(position);
        }

        // ------------------------------------------------------------------
        // Ground
        // ------------------------------------------------------------------
        var bounds = new Bounds(corners[0], Vector3.zero);
        foreach (var corner in corners) bounds.Encapsulate(corner);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(bounds.center.x, -0.5f, bounds.center.z);
        ground.transform.localScale = new Vector3(bounds.size.x + 60f, 1f, bounds.size.z + 60f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

        // ------------------------------------------------------------------
        // Walls, with a gap left at every turn. Those gaps are the junctions.
        // ------------------------------------------------------------------
        var walls = new GameObject("Walls");

        for (var i = 0; i < corners.Count - 1; i++)
        {
            var from = corners[i];
            var to = corners[i + 1];
            var direction = (to - from).normalized;
            var perpendicular = Vector3.Cross(Vector3.up, direction);

            // Pull the ends in so a turn reads as an opening rather than a sealed box.
            var startInset = i == 0 ? 0f : JunctionGap;
            var endInset = i == corners.Count - 2 ? 0f : JunctionGap;

            var a = from + direction * startInset;
            var b = to - direction * endInset;

            if ((b - a).magnitude < 0.5f)
                continue;

            MakeWall(walls.transform, a + perpendicular * CorridorHalfWidth,
                     b + perpendicular * CorridorHalfWidth, wallMaterial, $"Wall {i}L");

            MakeWall(walls.transform, a - perpendicular * CorridorHalfWidth,
                     b - perpendicular * CorridorHalfWidth, wallMaterial, $"Wall {i}R");
        }

        // ------------------------------------------------------------------
        // Rover
        // ------------------------------------------------------------------
        var rover = new GameObject("Salty");
        rover.transform.position = new Vector3(0f, 0.4f, 0f);

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(rover.transform, false);
        body.transform.localScale = new Vector3(1.1f, 0.6f, 1.6f);
        body.GetComponent<Renderer>().sharedMaterial = wallMaterial;
        Object.DestroyImmediate(body.GetComponent<Collider>());

        // The only lit part of the rover. In a silhouette world this is how you see it
        // at all, which makes light the machine's single means of expression.
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
        lampObject.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);

        var lamp = lampObject.AddComponent<Light>();
        lamp.type = LightType.Spot;
        lamp.range = 22f;
        lamp.spotAngle = 62f;
        lamp.intensity = 3f;
        lamp.color = new Color(0.85f, 0.88f, 1f);
        lamp.shadows = LightShadows.Soft;

        var roverLight = lampObject.AddComponent<RoverLight>();
        var controller = rover.AddComponent<RoverController>();

        var roverCollider = rover.AddComponent<CapsuleCollider>();
        roverCollider.height = 1.4f;
        roverCollider.radius = 0.7f;
        roverCollider.center = new Vector3(0f, 0.2f, 0f);

        // Kinematic body so trigger volumes notice the rover. RoverController moves the
        // transform directly, so physics must not also be trying to.
        var rigidbody = rover.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;

        var lightSerialized = new SerializedObject(roverLight);
        SetRef(lightSerialized, "rover", controller);
        SetRef(lightSerialized, "shell", shellRenderer);
        lightSerialized.ApplyModifiedPropertiesWithoutUndo();

        // ------------------------------------------------------------------
        // Junctions at every turn
        // ------------------------------------------------------------------
        var junctions = new GameObject("Junctions");
        var audio = rover.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;

        for (var i = 1; i < corners.Count - 1; i++)
        {
            var junctionObject = new GameObject($"Junction {i}");
            junctionObject.transform.SetParent(junctions.transform);
            junctionObject.transform.position = corners[i] + Vector3.up * 0.5f;

            // Small and centred on the corner. The rover halts the moment it enters,
            // so a large trigger would stop it well short and the following 90 degree
            // turn would point it into a wall. Three units is comfortably caught at
            // walking pace and leaves the rover close enough to the corner that
            // turning lines it up with the next corridor.
            var trigger = junctionObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(3f, 3f, 3f);

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Marker";
            marker.transform.SetParent(junctionObject.transform, false);
            marker.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            marker.transform.localScale = new Vector3(7f, 0.05f, 7f);
            var markerRenderer = marker.GetComponent<Renderer>();
            markerRenderer.sharedMaterial = markerMaterial;
            Object.DestroyImmediate(marker.GetComponent<Collider>());

            var junction = junctionObject.AddComponent<Junction>();
            var junctionSerialized = new SerializedObject(junction);
            junctionSerialized.FindProperty("index").intValue = i;
            SetRef(junctionSerialized, "roverLight", roverLight);
            SetRef(junctionSerialized, "pingSource", audio);
            SetRef(junctionSerialized, "marker", markerRenderer);
            junctionSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------
        // The way out
        // ------------------------------------------------------------------
        var exit = new GameObject("Exit");
        exit.transform.position = corners[corners.Count - 1] + Vector3.up * 0.5f;
        var exitTrigger = exit.AddComponent<BoxCollider>();
        exitTrigger.isTrigger = true;
        exitTrigger.size = new Vector3(6f, 4f, 3f);

        // ------------------------------------------------------------------
        // Camera. Pale sky, so everything solid reads as silhouette against it.
        // ------------------------------------------------------------------
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.72f, 0.76f, 0.82f);
        camera.fieldOfView = 55f;
        cameraObject.AddComponent<AudioListener>();

        var follow = cameraObject.AddComponent<CameraFollow>();
        var followSerialized = new SerializedObject(follow);
        SetRef(followSerialized, "target", rover.transform);
        followSerialized.ApplyModifiedPropertiesWithoutUndo();

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.06f, 0.07f, 0.09f);
        RenderSettings.skybox = null;

        // ------------------------------------------------------------------
        // Level plumbing
        // ------------------------------------------------------------------
        var directorObject = new GameObject("Level Director");
        var simulation = directorObject.AddComponent<SimulationReset>();
        var director = directorObject.AddComponent<LevelDirector>();

        var simulationSerialized = new SerializedObject(simulation);
        SetRef(simulationSerialized, "rover", controller);
        simulationSerialized.ApplyModifiedPropertiesWithoutUndo();

        var directorSerialized = new SerializedObject(director);
        directorSerialized.FindProperty("levelNumber").intValue = 1;
        directorSerialized.FindProperty("levelName").stringValue = "Dust corridor";
        directorSerialized.FindProperty("guideline").stringValue =
            "Ensure no essential information is conveyed by sounds alone";
        directorSerialized.FindProperty("nextSceneName").stringValue = "";
        SetRef(directorSerialized, "simulation", simulation);

        var allowed = directorSerialized.FindProperty("allowedIntents");
        allowed.arraySize = 4;
        allowed.GetArrayElementAtIndex(0).enumValueIndex = (int)IntentId.Go;
        allowed.GetArrayElementAtIndex(1).enumValueIndex = (int)IntentId.Stop;
        allowed.GetArrayElementAtIndex(2).enumValueIndex = (int)IntentId.Left;
        allowed.GetArrayElementAtIndex(3).enumValueIndex = (int)IntentId.Right;
        directorSerialized.ApplyModifiedPropertiesWithoutUndo();

        var exitZone = exit.AddComponent<LevelExit>();
        var exitSerialized = new SerializedObject(exitZone);
        SetRef(exitSerialized, "director", director);
        exitSerialized.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Level01SceneBuilder] Wrote {ScenePath}, " +
                  $"{corners.Count - 2} junctions, corridor {bounds.size.x:0}x{bounds.size.z:0}");
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
        else Debug.LogWarning($"[Level01SceneBuilder] No field '{field}'");
    }
}
