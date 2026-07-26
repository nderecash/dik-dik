using System.Collections.Generic;
using System.IO;
using Dikdik.Commands;
using Dikdik.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Generates Level 3, the night side.
///
/// Guidelines: "Provide high contrast between text/UI and background" and
/// "Use an easily readable default font size".
///
/// <para>The level exists because of an assumption in this game's art direction. Every
/// other level is black shapes against a bright sky and reads cleanly because the sky
/// does the work for free. Here there is no sky glow. Shapes stop being shapes and a
/// crevasse looks exactly like ground.</para>
///
/// <para>High contrast puts the edges back with outlines, artificially, because the sky
/// is no longer providing them. That is the argument of the whole game expressed in
/// light rather than in speech.</para>
///
/// <para>It reuses stop-at-junction rather than asking the player to steer around
/// hazards while moving. Level 1 already taught us what precision under a 2.6 second
/// delay feels like, and it is Level 5's job, not this one's.</para>
///
/// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod Level03SceneBuilder.Generate
/// </summary>
public static class Level03SceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Level03.unity";
    private const string MaterialFolder = "Assets/Materials";

    private const float HalfWidth = 7f;
    private const float WallHeight = 3.2f;
    private const float SegmentLength = 18f;
    private const int Segments = 3;

    /// <summary>
    /// Which side the crevasse sits on at each decision point. The safe way is the
    /// other one. Fixed rather than random: a level that rolls dice on you is a
    /// different and worse argument than a level you cannot see.
    /// </summary>
    private static readonly float[] HazardSide = { 1f, -1f, 1f };

    [MenuItem("Dikdik/Generate Level 03")]
    public static void Generate()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Lit materials like the day levels, but the night lighting keeps them dark. The
        // crevasses stay too dark to see without high contrast, which is the whole level.
        var wallMaterial = Environment.LitMaterial("LunarRock", new Color(0.34f, 0.32f, 0.30f));
        var groundMaterial = Environment.LitMaterial("LunarGround", new Color(0.42f, 0.40f, 0.37f));
        var shellMaterial = MakeMaterial("RoverShell", "Unlit/Color", new Color(0.85f, 0.88f, 1f));
        var markerMaterial = MakeMaterial("JunctionMarker", "Unlit/Color", new Color(0.12f, 0.13f, 0.16f));

        // Barely darker than the ground. In daylight this would be obvious; at night it
        // is very nearly nothing, which is the level.
        var crevasseMaterial = MakeMaterial("Crevasse", "Unlit/Color", new Color(0.03f, 0.03f, 0.04f));

        var outlineShader = Shader.Find("Dikdik/HazardOutline");
        if (outlineShader == null)
        {
            Debug.LogError("[Level03SceneBuilder] Dikdik/HazardOutline shader not found. " +
                           "Level 3 cannot show hazards under high contrast without it.");
            return;
        }

        var outlineMaterial = MakeMaterialFromShader("HazardOutlineMat", outlineShader,
                                                     new Color(1f, 0.85f, 0.3f));

        var length = SegmentLength * (Segments + 1);

        // ------------------------------------------------------------------
        // Ground and walls
        // ------------------------------------------------------------------
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -0.5f, length * 0.5f);
        ground.transform.localScale = new Vector3(HalfWidth * 2f + 40f, 1f, length + 40f);
        ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;

        var walls = new GameObject("Walls");
        foreach (var side in new[] { 1f, -1f })
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = side > 0 ? "Wall L" : "Wall R";
            wall.transform.SetParent(walls.transform);
            wall.transform.position = new Vector3(side * HalfWidth, WallHeight * 0.5f, length * 0.5f);
            wall.transform.localScale = new Vector3(1.2f, WallHeight, length);
            wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
        }

        // ------------------------------------------------------------------
        // Rover
        // ------------------------------------------------------------------
        var rover = BuildRover(wallMaterial, shellMaterial, out var controller,
                               out var roverLight, out var audio);

        // ------------------------------------------------------------------
        // Level plumbing, needed before hazards so they can reference it
        // ------------------------------------------------------------------
        var directorObject = new GameObject("Level Director");
        var simulation = directorObject.AddComponent<SimulationReset>();
        var director = directorObject.AddComponent<LevelDirector>();

        var simulationSerialized = new SerializedObject(simulation);
        SetRef(simulationSerialized, "rover", controller);
        simulationSerialized.ApplyModifiedPropertiesWithoutUndo();

        var intro = directorObject.AddComponent<LevelIntroVoice>();
        var introSerialized = new SerializedObject(intro);
        introSerialized.FindProperty("clipName").stringValue = "sup_sector_03";
        introSerialized.ApplyModifiedPropertiesWithoutUndo();

        // ------------------------------------------------------------------
        // Decision points and the crevasses beyond them
        // ------------------------------------------------------------------
        var pingClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/junction_ping.wav");
        var queryClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/rover_query.wav");

        var doubt = rover.AddComponent<RoverDoubt>();
        var doubtSerialized = new SerializedObject(doubt);
        SetRef(doubtSerialized, "rover", controller);
        SetRef(doubtSerialized, "roverLight", roverLight);
        SetRef(doubtSerialized, "source", audio);
        SetRef(doubtSerialized, "queryClip", queryClip);

        // Shorter than Level 1's 20. A rover feeling its way through the dark should
        // check in more often; the pacing dial is doing character work here.
        doubtSerialized.FindProperty("distanceBeforeDoubt").floatValue = 14f;
        doubtSerialized.ApplyModifiedPropertiesWithoutUndo();

        var hazards = new GameObject("Hazards");
        var junctions = new GameObject("Decision Points");

        for (var i = 0; i < Segments; i++)
        {
            var z = SegmentLength * (i + 1);

            // Stop point, so the player chooses standing still rather than steering
            // around a hole they cannot see while a 2.6 second delay runs.
            var stop = new GameObject($"Decision {i + 1}");
            stop.transform.SetParent(junctions.transform);
            stop.transform.position = new Vector3(0f, 0.5f, z - 6f);

            var stopTrigger = stop.AddComponent<BoxCollider>();
            stopTrigger.isTrigger = true;
            stopTrigger.size = new Vector3(HalfWidth * 2f, 3f, 3f);

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Marker";
            marker.transform.SetParent(stop.transform, false);
            marker.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            marker.transform.localScale = new Vector3(HalfWidth * 2f, 0.05f, 2f);
            var markerRenderer = marker.GetComponent<Renderer>();
            markerRenderer.sharedMaterial = markerMaterial;
            Object.DestroyImmediate(marker.GetComponent<Collider>());

            var junction = stop.AddComponent<Junction>();
            var junctionSerialized = new SerializedObject(junction);
            junctionSerialized.FindProperty("index").intValue = i + 1;
            SetRef(junctionSerialized, "roverLight", roverLight);
            SetRef(junctionSerialized, "pingSource", audio);
            SetRef(junctionSerialized, "pingClip", pingClip);
            SetRef(junctionSerialized, "marker", markerRenderer);
            junctionSerialized.ApplyModifiedPropertiesWithoutUndo();

            // The crevasse, on one side of the way ahead.
            var crevasse = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crevasse.name = $"Crevasse {i + 1}";
            crevasse.transform.SetParent(hazards.transform);
            crevasse.transform.position = new Vector3(HazardSide[i] * 3.4f, 0.02f, z);
            crevasse.transform.localScale = new Vector3(6.4f, 0.06f, 7f);
            crevasse.GetComponent<Renderer>().sharedMaterial = crevasseMaterial;

            var hazardCollider = crevasse.GetComponent<BoxCollider>();
            hazardCollider.isTrigger = true;

            var hazard = crevasse.AddComponent<Hazard>();
            var hazardSerialized = new SerializedObject(hazard);
            SetRef(hazardSerialized, "simulation", simulation);
            hazardSerialized.ApplyModifiedPropertiesWithoutUndo();

            var outline = crevasse.AddComponent<HazardOutline>();
            var outlineSerialized = new SerializedObject(outline);
            SetRef(outlineSerialized, "outlineMaterial", outlineMaterial);
            outlineSerialized.FindProperty("width").floatValue = 0.14f;
            outlineSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------
        // Exit
        // ------------------------------------------------------------------
        var exit = new GameObject("Exit");
        exit.transform.position = new Vector3(0f, 0.5f, length - 4f);
        var exitTrigger = exit.AddComponent<BoxCollider>();
        exitTrigger.isTrigger = true;
        exitTrigger.size = new Vector3(HalfWidth * 2f, 4f, 3f);

        var exitZone = exit.AddComponent<LevelExit>();
        var exitSerialized = new SerializedObject(exitZone);
        SetRef(exitSerialized, "director", director);
        exitSerialized.ApplyModifiedPropertiesWithoutUndo();

        // The relay line, straight down the corridor to the exit. This is the night level,
        // so the cable's bright core is doing real work here: it is the only thing in the
        // scene that can be followed without the high contrast setting turned on.
        var cableCorners = new List<Vector3> { Vector3.zero, new Vector3(0f, 0f, length - 4f) };
        var cable = CableBuilder.Build(cableCorners, 4, false, controller, roverLight, audio);
        var mission = CableBuilder.AddMission(cable, controller, roverLight, director);
        CableBuilder.AddHud(mission, controller, director);

        // ------------------------------------------------------------------
        // Night. This is the whole level in three lines.
        // ------------------------------------------------------------------
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.fieldOfView = 55f;
        camera.farClipPlane = 400f;
        cameraObject.AddComponent<AudioListener>();

        var follow = cameraObject.AddComponent<CameraFollow>();
        var followSerialized = new SerializedObject(follow);
        SetRef(followSerialized, "target", rover.transform);
        followSerialized.ApplyModifiedPropertiesWithoutUndo();

        // The night side. Its own dark sky, a sky full of stars, and the sun turned almost
        // all the way down: the ground has to stay too dark to see the crevasses on,
        // because that is the whole level. High contrast is what puts their edges back.
        // A faint ridge on the horizon for depth, and no scattered rocks, which would
        // only be invisible clutter here.
        Environment.ApplyLighting("night", new Color(0.07f, 0.08f, 0.12f),
                                  new Color(0.01f, 0.014f, 0.03f), sunIntensity: 0.02f);
        Environment.BuildHorizon(new Vector3(0f, 0f, 36f), 120f, 3);

        // ------------------------------------------------------------------
        // Director
        // ------------------------------------------------------------------
        var directorSerialized = new SerializedObject(director);
        directorSerialized.FindProperty("levelNumber").intValue = 3;
        directorSerialized.FindProperty("levelName").stringValue = "Night side";
        directorSerialized.FindProperty("guideline").stringValue =
            "Provide high contrast between text/UI and background";
        directorSerialized.FindProperty("nextSceneName").stringValue = "Level04";
        SetRef(directorSerialized, "simulation", simulation);

        var allowed = directorSerialized.FindProperty("allowedIntents");
        allowed.arraySize = 5;
        allowed.GetArrayElementAtIndex(0).enumValueIndex = (int)IntentId.Go;
        allowed.GetArrayElementAtIndex(1).enumValueIndex = (int)IntentId.Stop;
        allowed.GetArrayElementAtIndex(2).enumValueIndex = (int)IntentId.Left;
        allowed.GetArrayElementAtIndex(3).enumValueIndex = (int)IntentId.Right;
        allowed.GetArrayElementAtIndex(4).enumValueIndex = (int)IntentId.Light;
        directorSerialized.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Level03SceneBuilder] Wrote {ScenePath}, {Segments} decision points, " +
                  $"{Segments} crevasses, corridor {HalfWidth * 2:0}x{length:0}");
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
        lampObject.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);

        var lamp = lampObject.AddComponent<Light>();
        lamp.type = LightType.Spot;
        lamp.range = 26f;
        lamp.spotAngle = 54f;
        lamp.intensity = 4f;
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
        return MakeMaterialFromShader(name, Shader.Find(shader), colour);
    }

    private static Material MakeMaterialFromShader(string name, Shader shader, Color colour)
    {
        Directory.CreateDirectory(MaterialFolder);
        var path = $"{MaterialFolder}/{name}.mat";

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.color = colour;
            return existing;
        }

        var material = new Material(shader) { color = colour };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        var property = so.FindProperty(field);
        if (property != null) property.objectReferenceValue = value;
        else Debug.LogWarning($"[Level03SceneBuilder] No field '{field}'");
    }
}
