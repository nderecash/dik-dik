using System.Collections.Generic;
using Dikdik.Game;
using Dikdik.Game.Cable;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lays the relay line, its checkpoints and the mission panel into a level.
///
/// <para>Every level builder calls this with the same corner list it used to place its own
/// terrain. That shared source is the whole trick: the cable cannot cross a rock, because
/// the rocks were scattered clear of the same route. Recovery drives the cable without any
/// pathfinding, and that is only safe while this stays true.</para>
///
/// <para>Geometry is cubes, matching <c>Environment.BuildHorizon</c>. Two strips per run: a
/// dark body that lies on the ground like a real object, and a thin bright core on top that
/// shows up whatever the scene lighting is doing. Level 3 is played in the dark and the
/// cable has to be findable there without turning on high contrast.</para>
/// </summary>
public static class CableBuilder
{
    private const float BodyWidth = 0.55f;
    private const float BodyHeight = 0.16f;
    private const float CoreWidth = 0.22f;
    private const float CoreHeight = 0.20f;
    private const float CableY = 0.02f;

    public struct Built
    {
        public GameObject Root;
        public CablePath Path;
        public CableVisual Visual;
        public List<CableCheckpoint> Checkpoints;
    }

    /// <summary>Build the line and its checkpoints.</summary>
    /// <param name="corners">The route in world space, start to finish.</param>
    /// <param name="checkpointCount">How many scan points to space along it.</param>
    /// <param name="lastIsFault">True only for level 6: its final checkpoint is the break.</param>
    /// <param name="explicitDistances">
    /// Where to put the checkpoints, in arc distance. Null means space them evenly, which
    /// is right for most levels. Level 5 passes its own, because on that level a scan
    /// landing just short of a stop pad would hand the player the stop they are supposed
    /// to be struggling for.
    /// </param>
    public static Built Build(IReadOnlyList<Vector3> corners, int checkpointCount,
                              bool lastIsFault, RoverController rover, RoverLight roverLight,
                              AudioSource roverAudio, float[] explicitDistances = null)
    {
        if (corners == null || corners.Count < 2)
        {
            Debug.LogError("[CableBuilder] Need at least two corners.");
            return new Built();
        }

        var root = new GameObject("Relay Line");

        var pathComponent = root.AddComponent<CablePath>();
        var pathSerialized = new SerializedObject(pathComponent);
        var cornerArray = pathSerialized.FindProperty("corners");
        cornerArray.arraySize = corners.Count;
        for (var i = 0; i < corners.Count; i++)
            cornerArray.GetArrayElementAtIndex(i).vector3Value = corners[i];
        pathSerialized.ApplyModifiedPropertiesWithoutUndo();

        // Distance at each corner, so checkpoints space by length rather than by corner
        // index. Spacing by index bunches them wherever the route happens to turn a lot,
        // which is where they are least useful.
        var cumulative = new float[corners.Count];
        for (var i = 1; i < corners.Count; i++)
            cumulative[i] = cumulative[i - 1] + Vector3.Distance(corners[i - 1], corners[i]);

        var total = cumulative[corners.Count - 1];

        // Even fractions, ending ON the last one rather than short of it, so the final
        // scan and the end of the run are the same place.
        float[] checkpointDistances;

        if (explicitDistances != null && explicitDistances.Length > 0)
        {
            checkpointDistances = explicitDistances;
            checkpointCount = explicitDistances.Length;
        }
        else
        {
            checkpointDistances = new float[checkpointCount];
            for (var i = 0; i < checkpointCount; i++)
                checkpointDistances[i] = total * ((i + 1f) / checkpointCount);
        }

        var bodyMaterial = Environment.LitMaterial("CableBody", new Color(0.10f, 0.11f, 0.13f));
        var coreMaterial = MakeUnlit("CableCore", new Color(0.45f, 0.95f, 1f));
        // Dim, but not invisible. This is a place the player has not been yet, so it should
        // read as waiting rather than as absent. The old value was so dark it disappeared.
        var markerMaterial = MakeUnlit("CheckpointMarker", new Color(0.30f, 0.48f, 0.56f));
        var sweepMaterial = MakeUnlit("ScanSweep", new Color(1f, 0.75f, 0.3f));

        // ------------------------------------------------------------------
        // The line, one parent per section so sections can light up alone
        // ------------------------------------------------------------------
        var sections = new Transform[checkpointCount];
        var sectionStart = 0f;

        for (var s = 0; s < checkpointCount; s++)
        {
            var sectionObject = new GameObject($"Section {s + 1}");
            sectionObject.transform.SetParent(root.transform);
            sections[s] = sectionObject.transform;

            BuildStrip(sectionObject.transform, corners, cumulative,
                       sectionStart, checkpointDistances[s], bodyMaterial, coreMaterial);

            sectionStart = checkpointDistances[s];
        }

        var visual = root.AddComponent<CableVisual>();
        var visualSerialized = new SerializedObject(visual);
        var sectionArray = visualSerialized.FindProperty("sections");
        sectionArray.arraySize = sections.Length;
        for (var i = 0; i < sections.Length; i++)
            sectionArray.GetArrayElementAtIndex(i).objectReferenceValue = sections[i];
        visualSerialized.ApplyModifiedPropertiesWithoutUndo();

        // ------------------------------------------------------------------
        // Checkpoints
        // ------------------------------------------------------------------
        var scanTone = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/scan_sweep.wav")
                    ?? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/junction_ping.wav");

        var checkpoints = new List<CableCheckpoint>();

        for (var i = 0; i < checkpointCount; i++)
        {
            var distance = checkpointDistances[i];
            var position = PointAt(corners, cumulative, distance);
            var direction = DirectionAt(corners, cumulative, distance);

            var checkpointObject = new GameObject($"Checkpoint {i + 1}");
            checkpointObject.transform.SetParent(root.transform);
            checkpointObject.transform.position = position;
            checkpointObject.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            // Generous trigger. Missing a checkpoint because the rover passed thirty
            // centimetres to the side of it would be a puzzle the player cannot see.
            var trigger = checkpointObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(7f, 4f, 2.5f);
            trigger.center = new Vector3(0f, 1.5f, 0f);

            // A ring on the ground and two posts either side of it.
            //
            // The ring alone was invisible. The follow camera looks along the ground at a
            // shallow angle, so a flat disc is edge-on and vanishes into the dust at any
            // distance, which meant the player could not see where the next scan was until
            // they were standing on it. The posts are what make a checkpoint a place you
            // can aim at. All three pieces are one marker and repaint together.
            var markerGroup = new GameObject("Marker");
            markerGroup.transform.SetParent(checkpointObject.transform);
            markerGroup.transform.localPosition = Vector3.zero;
            markerGroup.transform.localRotation = Quaternion.identity;

            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Ring";
            ring.transform.SetParent(markerGroup.transform);
            ring.transform.localPosition = new Vector3(0f, -0.35f, 0f);
            ring.transform.localScale = new Vector3(3.4f, 0.05f, 3.4f);
            ring.GetComponent<Renderer>().sharedMaterial = markerMaterial;
            Object.DestroyImmediate(ring.GetComponent<Collider>());

            foreach (var side in new[] { -1f, 1f })
            {
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = side < 0 ? "Post L" : "Post R";
                post.transform.SetParent(markerGroup.transform);
                post.transform.localPosition = new Vector3(side * 1.9f, 0.6f, 0f);
                post.transform.localScale = new Vector3(0.22f, 1.9f, 0.22f);
                post.GetComponent<Renderer>().sharedMaterial = markerMaterial;

                // No collider. A checkpoint the rover can crash into would be a checkpoint
                // that punishes you for arriving at it.
                Object.DestroyImmediate(post.GetComponent<Collider>());
            }

            var sweep = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sweep.name = "Sweep Bar";
            sweep.transform.SetParent(checkpointObject.transform);
            sweep.transform.localPosition = Vector3.zero;
            sweep.transform.localRotation = Quaternion.identity;
            sweep.transform.localScale = new Vector3(4.5f, 1.6f, 0.18f);
            sweep.GetComponent<Renderer>().sharedMaterial = sweepMaterial;
            Object.DestroyImmediate(sweep.GetComponent<Collider>());
            sweep.SetActive(false);

            var checkpoint = checkpointObject.AddComponent<CableCheckpoint>();
            var checkpointSerialized = new SerializedObject(checkpoint);
            checkpointSerialized.FindProperty("distanceAlongCable").floatValue = distance;
            checkpointSerialized.FindProperty("isFault").boolValue =
                lastIsFault && i == checkpointCount - 1;
            SetRef(checkpointSerialized, "rover", rover);
            SetRef(checkpointSerialized, "roverLight", roverLight);
            SetRef(checkpointSerialized, "marker", markerGroup.transform);
            SetRef(checkpointSerialized, "sweepBar", sweep.transform);
            SetRef(checkpointSerialized, "audioSource", roverAudio);
            SetRef(checkpointSerialized, "scanTone", scanTone);
            checkpointSerialized.ApplyModifiedPropertiesWithoutUndo();

            checkpoints.Add(checkpoint);
        }

        return new Built
        {
            Root = root,
            Path = pathComponent,
            Visual = visual,
            Checkpoints = checkpoints,
        };
    }

    /// <summary>
    /// Add progress tracking and recovery. Separate from Build because both need the
    /// level's director, and some builders create that after their terrain.
    /// </summary>
    /// <param name="completeOnScan">
    /// Whether finishing the scans finishes the level. True everywhere except Level 5,
    /// where the stop pads are the objective and completing on the scan instead would let
    /// the player skip the one thing that level is about.
    /// </param>
    public static MissionProgress AddMission(Built built, RoverController rover,
                                             RoverLight roverLight, LevelDirector director,
                                             bool completeOnScan = true)
    {
        var progress = built.Root.AddComponent<MissionProgress>();
        var progressSerialized = new SerializedObject(progress);
        progressSerialized.FindProperty("completeLevelWhenScanned").boolValue = completeOnScan;
        SetRef(progressSerialized, "path", built.Path);
        SetRef(progressSerialized, "visual", built.Visual);
        SetRef(progressSerialized, "rover", rover.transform);
        SetRef(progressSerialized, "director", director);
        progressSerialized.ApplyModifiedPropertiesWithoutUndo();

        var recovery = built.Root.AddComponent<CableRecovery>();
        var recoverySerialized = new SerializedObject(recovery);
        SetRef(recoverySerialized, "path", built.Path);
        SetRef(recoverySerialized, "rover", rover);
        SetRef(recoverySerialized, "roverLight", roverLight);
        SetRef(recoverySerialized, "progress", progress);
        recoverySerialized.ApplyModifiedPropertiesWithoutUndo();

        return progress;
    }

    /// <summary>
    /// The mission panel, top left.
    ///
    /// Its own canvas rather than the Boot console's, because Boot survives scene loads and
    /// this is per level. Sharing one would leave the panel describing a level that ended.
    /// </summary>
    public static MissionHud AddHud(MissionProgress progress, RoverController rover,
                                    LevelDirector director)
    {
        var canvasObject = new GameObject("Mission Canvas");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        var panel = new GameObject("Mission Panel");
        panel.transform.SetParent(canvasObject.transform, false);

        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(18f, -18f);
        panelRect.sizeDelta = new Vector2(320f, 132f);

        var background = panel.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.55f);

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var sector = MakeText(panel.transform, "Sector", font, 20, new Vector2(12f, -10f));
        var scanned = MakeText(panel.transform, "Scanned", font, 16, new Vector2(12f, -40f));
        var distance = MakeText(panel.transform, "Distance", font, 16, new Vector2(12f, -62f));
        var state = MakeText(panel.transform, "State", font, 16, new Vector2(12f, -106f));

        // Speed bar, filled left to right like every other gauge anyone has seen.
        var barBack = new GameObject("Speed Bar Back");
        barBack.transform.SetParent(panel.transform, false);
        var barBackRect = barBack.AddComponent<RectTransform>();
        barBackRect.anchorMin = new Vector2(0f, 1f);
        barBackRect.anchorMax = new Vector2(0f, 1f);
        barBackRect.pivot = new Vector2(0f, 1f);
        barBackRect.anchoredPosition = new Vector2(12f, -88f);
        barBackRect.sizeDelta = new Vector2(296f, 8f);
        barBack.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.14f);

        var barFill = new GameObject("Speed Bar");
        barFill.transform.SetParent(barBack.transform, false);
        var barFillRect = barFill.AddComponent<RectTransform>();
        barFillRect.anchorMin = Vector2.zero;
        barFillRect.anchorMax = Vector2.one;
        barFillRect.offsetMin = Vector2.zero;
        barFillRect.offsetMax = Vector2.zero;

        var barImage = barFill.AddComponent<Image>();
        barImage.color = new Color(0.72f, 0.82f, 0.95f);
        barImage.type = Image.Type.Filled;
        barImage.fillMethod = Image.FillMethod.Horizontal;
        barImage.fillAmount = 0f;

        var hud = panel.AddComponent<MissionHud>();
        var hudSerialized = new SerializedObject(hud);
        SetRef(hudSerialized, "progress", progress);
        SetRef(hudSerialized, "rover", rover);
        SetRef(hudSerialized, "director", director);
        SetRef(hudSerialized, "sectorText", sector);
        SetRef(hudSerialized, "scannedText", scanned);
        SetRef(hudSerialized, "distanceText", distance);
        SetRef(hudSerialized, "stateText", state);
        SetRef(hudSerialized, "speedBar", barImage);
        SetRef(hudSerialized, "background", background);
        hudSerialized.ApplyModifiedPropertiesWithoutUndo();

        return hud;
    }

    // ------------------------------------------------------------------
    // Geometry
    // ------------------------------------------------------------------

    /// <summary>
    /// One section: a body cube and a core cube for every straight run inside it, split at
    /// every corner it crosses.
    /// </summary>
    private static void BuildStrip(Transform parent, IReadOnlyList<Vector3> corners,
                                   float[] cumulative, float fromDistance, float toDistance,
                                   Material body, Material core)
    {
        var cuts = new List<float> { fromDistance };

        foreach (var t in cumulative)
            if (t > fromDistance && t < toDistance)
                cuts.Add(t);

        cuts.Add(toDistance);

        for (var i = 1; i < cuts.Count; i++)
        {
            var a = PointAt(corners, cumulative, cuts[i - 1]);
            var b = PointAt(corners, cumulative, cuts[i]);

            if (Vector3.Distance(a, b) < 0.05f)
                continue;

            MakeRun(parent, a, b, BodyWidth, BodyHeight, CableY, body, $"Body {i}");
            MakeRun(parent, a, b, CoreWidth, CoreHeight, CableY + 0.06f, core, $"Core {i}");
        }
    }

    private static void MakeRun(Transform parent, Vector3 a, Vector3 b, float width,
                                float height, float y, Material material, string name)
    {
        var run = GameObject.CreatePrimitive(PrimitiveType.Cube);
        run.name = name;
        run.transform.SetParent(parent);

        var middle = (a + b) * 0.5f;
        run.transform.position = new Vector3(middle.x, y, middle.z);
        run.transform.rotation = Quaternion.LookRotation((b - a).normalized, Vector3.up);

        // Slightly longer than the gap, so runs overlap at corners and the line has no
        // visible seams where it turns.
        run.transform.localScale = new Vector3(width, height, Vector3.Distance(a, b) + 0.3f);
        run.GetComponent<Renderer>().sharedMaterial = material;

        // No collider. The rover drives along this, not into it.
        Object.DestroyImmediate(run.GetComponent<Collider>());
    }

    private static Vector3 PointAt(IReadOnlyList<Vector3> corners, float[] cumulative, float distance)
    {
        if (distance <= 0f)
            return corners[0];

        for (var i = 1; i < corners.Count; i++)
        {
            if (distance > cumulative[i])
                continue;

            var span = cumulative[i] - cumulative[i - 1];
            if (span <= 0.0001f)
                return corners[i];

            return Vector3.Lerp(corners[i - 1], corners[i], (distance - cumulative[i - 1]) / span);
        }

        return corners[corners.Count - 1];
    }

    private static Vector3 DirectionAt(IReadOnlyList<Vector3> corners, float[] cumulative, float distance)
    {
        for (var i = 1; i < corners.Count; i++)
        {
            if (distance > cumulative[i] && i < corners.Count - 1)
                continue;

            var d = corners[i] - corners[i - 1];
            return d.sqrMagnitude < 0.0001f ? Vector3.forward : d.normalized;
        }

        return Vector3.forward;
    }

    // ------------------------------------------------------------------
    // Small helpers
    // ------------------------------------------------------------------

    private static Text MakeText(Transform parent, string name, Font font, int size, Vector2 position)
    {
        var textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);

        var rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(300f, 24f);

        var text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = new Color(0.72f, 0.82f, 0.95f);
        text.text = string.Empty;
        return text;
    }

    private static Material MakeUnlit(string name, Color colour)
    {
        System.IO.Directory.CreateDirectory("Assets/Materials");
        var path = $"Assets/Materials/{name}.mat";

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            existing.color = colour;
            return existing;
        }

        var material = new Material(Shader.Find("Unlit/Color")) { color = colour };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void SetRef(SerializedObject so, string field, Object value)
    {
        var property = so.FindProperty(field);
        if (property == null)
        {
            Debug.LogError($"[CableBuilder] no field '{field}' on {so.targetObject.GetType().Name}");
            return;
        }

        property.objectReferenceValue = value;
    }
}
