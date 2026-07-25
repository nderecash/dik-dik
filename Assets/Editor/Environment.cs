using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared environment dressing for the level scenes: sky, sun, a distant ridge line, and
/// scattered rocks. One place so every level looks like the same world, and so a change
/// to the look is a change in one file rather than six.
///
/// Everything here is procedural or primitive. No downloaded assets, nothing that can
/// fail to import in batch mode, and it all survives the WebGL build.
/// </summary>
public static class Environment
{
    private const string MaterialFolder = "Assets/Materials";

    /// <summary>
    /// Sky, sun and ambient. Call once per scene before adding scenery.
    ///
    /// The key names the sky material, so levels that want the same look share one
    /// material (day levels) while the night side gets its own. Without this every
    /// scene would point at a single shared material and all look identical.
    /// </summary>
    public static Light ApplyLighting(string key, Color horizon, Color zenith, float sunIntensity = 1.15f)
    {
        var sky = MakeSky(key, horizon, zenith);

        // Bright day gets a warm sun disk and a light scatter of stars; the night side
        // gets no visible sun and a dense field of them.
        var day = sunIntensity > 0.3f;
        sky.SetColor("_SunColour", day ? new Color(1f, 0.95f, 0.85f) : new Color(0.05f, 0.05f, 0.07f));
        sky.SetFloat("_StarAmount", day ? 0.35f : 0.95f);
        RenderSettings.skybox = sky;

        // A low sun for long shadows and a lit edge on everything, which is what makes a
        // silhouette read as a solid object catching light rather than a flat cut-out.
        // On the night side the intensity is dialled almost to nothing.
        var sunObject = new GameObject("Sun");
        var sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.96f, 0.88f);
        sun.intensity = sunIntensity;
        sun.shadows = sunIntensity > 0.3f ? LightShadows.Soft : LightShadows.None;
        sunObject.transform.rotation = Quaternion.Euler(14f, -38f, 0f);

        // Fill from the sky itself, so shadowed faces are lit by the horizon glow rather
        // than going pure black.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = zenith * 1.2f;
        RenderSettings.ambientEquatorColor = horizon * 0.5f;
        RenderSettings.ambientGroundColor = new Color(0.04f, 0.04f, 0.05f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = horizon * 0.8f;
        RenderSettings.fogStartDistance = 60f;
        RenderSettings.fogEndDistance = 260f;

        return sun;
    }

    /// <summary>
    /// A low, continuous, jagged rim far out on the skyline: the far wall of the crater.
    ///
    /// The first attempt used tall scattered blocks and they read as floating boxes. This
    /// is a ring of short segments, edge to edge, with heights rolling smoothly around it,
    /// so from a low camera it sits just above the horizon as distant terrain rather than
    /// nearby objects. Kept low on purpose: a distant ridge takes up very little of the
    /// sky, and height is what made the old one look wrong.
    /// </summary>
    public static void BuildHorizon(Vector3 center, float radius, int seed)
    {
        var parent = new GameObject("Horizon");
        // Dark lit rock rather than pure black, so the distant rim reads as terrain in
        // shadow in a lit world instead of a flat black band.
        var ridge = LitMaterial("HorizonRock", new Color(0.14f, 0.13f, 0.14f));
        var rng = new System.Random(seed);

        var segments = 90;
        var phase = (float)(rng.NextDouble() * 10);

        for (var i = 0; i < segments; i++)
        {
            var angle = (i / (float)segments) * Mathf.PI * 2f;

            // Smoothly rolling height plus a little noise, so it is hills and not spikes.
            var roll = Mathf.Sin(angle * 3f + phase) * 0.5f + Mathf.Sin(angle * 7f + phase * 2f) * 0.25f;
            var height = 4f + (roll + 0.75f) * 6f + (float)(rng.NextDouble() * 2);

            var pos = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = $"Rim {i}";
            block.transform.SetParent(parent.transform);
            block.transform.position = new Vector3(pos.x, height * 0.5f - 1.5f, pos.z);
            block.transform.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);

            // Wide enough to overlap its neighbour so the rim has no gaps.
            var arc = radius * (Mathf.PI * 2f / segments) * 1.6f;
            block.transform.localScale = new Vector3(arc, height, 10f);
            block.GetComponent<Renderer>().sharedMaterial = ridge;

            // The collider stays. This ring is the edge of the world.
            //
            // Every level's route is under 60 units long inside a rim at radius 120 to
            // 130, so a player will only ever meet this by deliberately driving away from
            // the mission, and when they do the rover stops against a hill and says so.
            // That is a better answer than an invisible wall, and a much better one than
            // the previous answer, which was to keep going until the level was a dot.
        }
    }

    /// <summary>
    /// Wheels, a sensor mast and a dish, all in the silhouette material. Turns a plain
    /// body box into a recognisable rover shape without lighting any of it. Shared so
    /// every level's rover looks the same.
    /// </summary>
    public static void BuildRoverDetail(Transform rover, Material black)
    {
        var wheelX = 0.62f;
        var wheelZ = 0.55f;
        foreach (var sx in new[] { -1f, 1f })
        foreach (var sz in new[] { -1f, 1f })
        {
            var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            wheel.name = "Wheel";
            wheel.transform.SetParent(rover, false);
            wheel.transform.localPosition = new Vector3(sx * wheelX, -0.05f, sz * wheelZ);
            wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            wheel.transform.localScale = new Vector3(0.42f, 0.14f, 0.42f);
            wheel.GetComponent<Renderer>().sharedMaterial = black;
            UnityEngine.Object.DestroyImmediate(wheel.GetComponent<Collider>());
        }

        var mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        mast.name = "Mast";
        mast.transform.SetParent(rover, false);
        mast.transform.localPosition = new Vector3(-0.32f, 0.55f, -0.45f);
        mast.transform.localScale = new Vector3(0.08f, 0.5f, 0.08f);
        mast.GetComponent<Renderer>().sharedMaterial = black;
        UnityEngine.Object.DestroyImmediate(mast.GetComponent<Collider>());

        var dish = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        dish.name = "Dish";
        dish.transform.SetParent(rover, false);
        dish.transform.localPosition = new Vector3(-0.32f, 1.02f, -0.42f);
        dish.transform.localRotation = Quaternion.Euler(50f, 0f, 0f);
        dish.transform.localScale = new Vector3(0.34f, 0.04f, 0.34f);
        dish.GetComponent<Renderer>().sharedMaterial = black;
        UnityEngine.Object.DestroyImmediate(dish.GetComponent<Collider>());
    }

    /// <summary>A lit standard material, created once and reused by name.</summary>
    public static Material LitMaterial(string name, Color colour, float smoothness = 0.1f)
    {
        Directory.CreateDirectory(MaterialFolder);
        var path = $"{MaterialFolder}/{name}.mat";

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
            return existing;

        var mat = new Material(Shader.Find("Standard")) { color = colour };
        mat.SetFloat("_Glossiness", smoothness);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    /// <summary>
    /// Attach the Kenney rover model to a rover transform, sitting on the ground and
    /// facing forward. Pass null to keep its own colours, or a material to override.
    /// </summary>
    public static void AttachRoverModel(Transform rover, Material overrideMat, float scale = 4.2f)
    {
        var body = InstantiateModel("rover", overrideMat, scale);
        if (body == null)
            return;

        body.name = "Rover Model";
        body.transform.SetParent(rover, true);
        body.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
    }

    /// <summary>Place one Kenney model in the world, keeping its own colours by default.</summary>
    public static GameObject PlaceModel(string modelName, Vector3 position, float yaw, float scale,
                                        Material overrideMat = null, bool solid = false)
    {
        var go = InstantiateModel(modelName, overrideMat, scale, solid);
        if (go == null)
            return null;

        // InstantiateModel centres the model at the origin sitting on y=0; move it to
        // the requested spot and face it.
        go.transform.position += new Vector3(position.x, position.y, position.z);
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * go.transform.rotation;
        return go;
    }

    /// <summary>
    /// Scatter Kenney rocks, meteors and craters through a rectangle, clear of a centre
    /// lane, each keeping its own colour. The lit counterpart of ScatterRocks.
    /// </summary>
    public static void ScatterKenneyRocks(Vector3 center, float halfX, float halfZ,
                                          float clearLane, int count, int seed)
    {
        var parent = new GameObject("Rocks");
        var rng = new System.Random(seed);
        var kinds = new[] { "rock", "rock_largeA", "rock_largeB", "rocks_smallA", "rocks_smallB", "meteor" };

        for (var i = 0; i < count; i++)
        {
            var x = (float)(rng.NextDouble() * 2 - 1) * halfX;
            var z = (float)(rng.NextDouble() * 2 - 1) * halfZ;
            if (Mathf.Abs(x) < clearLane)
                x += Mathf.Sign(x == 0 ? 1 : x) * clearLane;

            var kind = kinds[rng.Next(kinds.Length)];
            var scale = 2.5f + (float)(rng.NextDouble() * 4);

            // Solid. A rock the rover drives straight through is not scenery, it is a
            // hole in the fiction, and the whole premise is a machine that reacts to
            // what is actually in front of it.
            var rock = PlaceModel(kind, center + new Vector3(x, 0f, z),
                                  (float)(rng.NextDouble() * 360), scale, solid: true);
            if (rock != null)
                rock.transform.SetParent(parent.transform, true);
        }
    }

    /// <summary>Low-poly rocks scattered through a rectangle, kept clear of a centre lane.</summary>
    public static void ScatterRocks(Vector3 center, float halfX, float halfZ,
                                    float clearLane, int count, int seed)
    {
        var parent = new GameObject("Rocks");
        var rockMat = MakeMaterial("Rock", "Standard", new Color(0.14f, 0.13f, 0.13f));
        var rng = new System.Random(seed);

        for (var i = 0; i < count; i++)
        {
            var x = (float)(rng.NextDouble() * 2 - 1) * halfX;
            var z = (float)(rng.NextDouble() * 2 - 1) * halfZ;

            // Keep the driving lane down the middle clear.
            if (Mathf.Abs(x) < clearLane)
                x += Mathf.Sign(x == 0 ? 1 : x) * clearLane;

            var size = 0.4f + (float)(rng.NextDouble() * 1.6);
            var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = $"Rock {i}";
            rock.transform.SetParent(parent.transform);
            rock.transform.position = center + new Vector3(x, size * 0.25f, z);
            rock.transform.rotation = Quaternion.Euler(
                (float)(rng.NextDouble() * 40 - 20),
                (float)(rng.NextDouble() * 360),
                (float)(rng.NextDouble() * 40 - 20));
            rock.transform.localScale = new Vector3(
                size * (0.7f + (float)rng.NextDouble()),
                size * (0.5f + (float)rng.NextDouble() * 0.6f),
                size * (0.7f + (float)rng.NextDouble()));
            rock.GetComponent<Renderer>().sharedMaterial = rockMat;
            UnityEngine.Object.DestroyImmediate(rock.GetComponent<Collider>());
        }
    }

    /// <summary>
    /// Instantiate an imported Kenney model, optionally forcing one material across all
    /// its parts. For the silhouette look we pass the black material and keep only the
    /// shape; the model's own colours are not needed.
    /// </summary>
    public static GameObject InstantiateModel(string modelName, Material overrideMat, float scale = 1f,
                                              bool solid = false)
    {
        var path = $"Assets/Kenney/Models/{modelName}.fbx";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"[Environment] model not found: {path}");
            return null;
        }

        var go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
        UnityEditor.PrefabUtility.UnpackPrefabInstance(go,
            UnityEditor.PrefabUnpackMode.Completely, UnityEditor.InteractionMode.AutomatedAction);

        if (overrideMat != null)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (var i = 0; i < mats.Length; i++)
                    mats[i] = overrideMat;
                r.sharedMaterials = mats;
            }
        }

        // The imported mesh colliders always go. They are per-part, convex-unfriendly and
        // sized for a model that has not been scaled yet. What replaces them, if anything,
        // is one measured box below.
        foreach (var c in go.GetComponentsInChildren<Collider>())
            UnityEngine.Object.DestroyImmediate(c);

        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * scale;

        // Kenney models carry a baked pivot offset, so recentre by measured bounds:
        // X and Z to the origin, the bottom onto the ground at y = 0. Then the caller can
        // parent it and it sits where the transform is.
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            var b = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);

            go.transform.position += new Vector3(-b.center.x, -b.min.y, -b.center.z);

            if (solid)
                AddMeasuredBox(go, b);
        }

        return go;
    }

    /// <summary>
    /// One box collider covering the model, in the model's local space.
    ///
    /// <para>A box and not the imported mesh colliders. The rover's obstacle probe is a
    /// short raycast, and a rock that stops the rover on its silhouette but lets the probe
    /// slip through a gap between two mesh parts is worse than a rock that is slightly
    /// bigger than it looks. Predictable beats accurate here.</para>
    ///
    /// <para>Inset slightly so the collider sits just inside the visible shape. A prop the
    /// rover stops short of reads as caution; a prop the rover visibly overlaps before
    /// stopping reads as a bug.</para>
    /// </summary>
    private static void AddMeasuredBox(GameObject go, Bounds worldBounds)
    {
        var lossy = go.transform.lossyScale;

        // worldBounds was measured after scaling but before the recentre, so its size is
        // already correct in world units. Divide back out to local space, because that is
        // what BoxCollider stores.
        var local = new Vector3(
            worldBounds.size.x / Mathf.Max(0.0001f, lossy.x),
            worldBounds.size.y / Mathf.Max(0.0001f, lossy.y),
            worldBounds.size.z / Mathf.Max(0.0001f, lossy.z));

        // After the recentre the model sits centred on x and z with its base at y = 0,
        // so the box's centre is half its height straight up.
        var box = go.AddComponent<BoxCollider>();
        box.size = local * 0.9f;
        box.center = new Vector3(0f, local.y * 0.5f, 0f);
    }

    private static Material MakeSky(string key, Color horizon, Color zenith)
    {
        Directory.CreateDirectory(MaterialFolder);
        var path = $"{MaterialFolder}/GradientSky_{key}.mat";

        var shader = Shader.Find("Dikdik/GradientSky");
        if (shader == null)
        {
            Debug.LogError("[Environment] Dikdik/GradientSky shader not found.");
            return null;
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.shader = shader;
        mat.SetColor("_Horizon", horizon);
        mat.SetColor("_Zenith", zenith);
        mat.SetVector("_SunDir", new Vector4(0.3f, 0.14f, 1f, 0f));
        return mat;
    }

    private static Material MakeMaterial(string name, string shader, Color colour)
    {
        Directory.CreateDirectory(MaterialFolder);
        var path = $"{MaterialFolder}/{name}.mat";

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
            return existing;

        var material = new Material(Shader.Find(shader)) { color = colour };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
