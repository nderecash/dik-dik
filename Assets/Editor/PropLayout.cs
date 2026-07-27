using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Lets props be placed by hand in the editor and survive a regeneration.
///
/// <para>This project builds every scene from code, which is what makes the scenes
/// reviewable in a diff instead of being an opaque binary blob. The cost of that is real:
/// spend an afternoon moving rocks into a composition you like, regenerate for any other
/// reason, and the afternoon is gone.</para>
///
/// <para>So props get an exception, and only props. Move them wherever you like, click
/// <b>Dikdik &gt; Save Prop Layout</b>, and a text file records where everything ended up.
/// Every regeneration after that puts them back exactly there.</para>
///
/// <para>The file is plain text, one prop per line, so it stays diffable like everything
/// else. A commit that moves a hangar shows a hangar moving.</para>
///
/// <para><b>What this does not cover:</b> terrain, walls, the cable, checkpoints, the rover
/// and the camera. Those are load-bearing. The cable especially: it is laid along the same
/// route the props are scattered clear of, and hand-editing one without the other is how a
/// rock ends up sitting on the line the recovery drive assumes is clear.</para>
/// </summary>
public static class PropLayout
{
    private const string Folder = "Assets/Layouts";

    private static string PathFor(int levelNumber) =>
        $"{Folder}/level{levelNumber:00}-props.txt";

    // ------------------------------------------------------------------
    // Applying a saved layout, called by each level builder
    // ------------------------------------------------------------------

    /// <summary>
    /// Replace whatever the builder scattered with the saved layout, if there is one.
    ///
    /// <para>Called at the end of generation rather than instead of the scatter, so a level
    /// with no saved layout still gets its procedural dressing and nothing has to change
    /// when a layout is added later.</para>
    /// </summary>
    public static void TryApply(int levelNumber)
    {
        // Touch the root so it always exists, even in a level with no procedural props.
        // Levels 3 and 6 scatter almost nothing, and without this there would be no
        // object to drag a new model into and nothing for Save Prop Layout to find.
        var dressing = Environment.Dressing;

        var path = PathFor(levelNumber);
        if (!File.Exists(path))
            return;

        var props = Read(path);
        if (props.Count == 0)
            return;

        // Clear the procedural scatter. Everything under Dressing came from PlaceModel and
        // is about to be replaced by the same models in the saved positions.
        for (var i = dressing.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(dressing.GetChild(i).gameObject);

        var placed = 0;
        foreach (var prop in props)
        {
            var go = Environment.InstantiateModel(prop.Model, null, prop.Scale, prop.Solid);
            if (go == null)
                continue;

            // Position and rotation are recorded as the final world values, not as the
            // yaw and offset that produced them. Reconstructing from inputs would drift
            // as soon as anything about the model's pivot or bounds changed.
            go.transform.position = prop.Position;
            go.transform.rotation = Quaternion.Euler(prop.Euler);
            go.transform.SetParent(dressing, true);
            placed++;
        }

        Debug.Log($"[PropLayout] Level {levelNumber}: restored {placed} hand-placed props " +
                  $"from {path}, replacing the procedural scatter.");
    }

    // ------------------------------------------------------------------
    // Saving what is in the open scene
    // ------------------------------------------------------------------

    [MenuItem("Dikdik/Save Prop Layout", priority = 40)]
    public static void SaveOpenScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var levelNumber = LevelNumberFrom(scene.name);

        if (levelNumber == 0)
        {
            EditorUtility.DisplayDialog(
                "Not a level scene",
                $"The open scene is \"{scene.name}\". Open Level01 through Level06 and try again.",
                "OK");
            return;
        }

        GameObject dressing = null;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "Dressing")
                dressing = root;

        if (dressing == null)
        {
            EditorUtility.DisplayDialog(
                "No props found",
                "This scene has no object called \"Dressing\". Regenerate the level once, " +
                "then move things and save again.",
                "OK");
            return;
        }

        Directory.CreateDirectory(Folder);
        var path = PathFor(levelNumber);

        var lines = new List<string>
        {
            $"# Prop layout for Level {levelNumber:00}.",
            "#",
            "# Written by Dikdik > Save Prop Layout. Read back by every regeneration of",
            "# this level, replacing the random scatter entirely.",
            "#",
            "# Delete this file to go back to procedural placement.",
            "#",
            "# model, posX, posY, posZ, eulerX, eulerY, eulerZ, scale, solid"
        };

        var count = 0;
        foreach (Transform child in dressing.transform)
        {
            var t = child.transform;
            var solid = child.GetComponentInChildren<Collider>() != null ? 1 : 0;

            lines.Add(string.Format(CultureInfo.InvariantCulture,
                "{0}, {1:0.###}, {2:0.###}, {3:0.###}, {4:0.##}, {5:0.##}, {6:0.##}, {7:0.###}, {8}",
                child.name, t.position.x, t.position.y, t.position.z,
                t.eulerAngles.x, t.eulerAngles.y, t.eulerAngles.z,
                t.localScale.x, solid));

            count++;
        }

        File.WriteAllLines(path, lines);
        AssetDatabase.Refresh();

        Debug.Log($"[PropLayout] Saved {count} props to {path}. " +
                  "Regenerating this level will now put them back exactly here.");

        EditorUtility.DisplayDialog(
            "Saved",
            $"{count} props written to {path}.\n\n" +
            "Regenerating this level will now restore them instead of scattering randomly.\n\n" +
            "Delete that file to go back to random placement.",
            "OK");
    }

    [MenuItem("Dikdik/Forget Prop Layout", priority = 41)]
    public static void ForgetOpenScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var levelNumber = LevelNumberFrom(scene.name);

        if (levelNumber == 0)
            return;

        var path = PathFor(levelNumber);
        if (!File.Exists(path))
        {
            EditorUtility.DisplayDialog("Nothing saved",
                $"Level {levelNumber:00} has no saved layout. It is already procedural.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Go back to random placement?",
                $"This deletes {path}.\n\nThe next regeneration of this level will scatter " +
                "props randomly again. Your arrangement is not recoverable unless it is in git.",
                "Delete", "Cancel"))
            return;

        AssetDatabase.DeleteAsset(path);
        Debug.Log($"[PropLayout] Deleted {path}. Level {levelNumber:00} is procedural again.");
    }

    // ------------------------------------------------------------------
    // Reading
    // ------------------------------------------------------------------

    private struct Prop
    {
        public string Model;
        public Vector3 Position;
        public Vector3 Euler;
        public float Scale;
        public bool Solid;
    }

    private static List<Prop> Read(string path)
    {
        var props = new List<Prop>();

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#"))
                continue;

            var parts = line.Split(',');
            if (parts.Length < 9)
            {
                Debug.LogWarning($"[PropLayout] Skipped a line with {parts.Length} fields, " +
                                 $"expected 9: {line}");
                continue;
            }

            props.Add(new Prop
            {
                Model = parts[0].Trim(),
                Position = new Vector3(Number(parts[1]), Number(parts[2]), Number(parts[3])),
                Euler = new Vector3(Number(parts[4]), Number(parts[5]), Number(parts[6])),
                Scale = Number(parts[7]),
                Solid = Number(parts[8]) > 0.5f
            });
        }

        return props;
    }

    /// <summary>Invariant culture, always. A machine set to comma decimals would otherwise
    /// read every position in this file as zero and silently pile the props at the origin.</summary>
    private static float Number(string s) =>
        float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;

    private static int LevelNumberFrom(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || !sceneName.StartsWith("Level"))
            return 0;

        return int.TryParse(sceneName.Substring(5), out var n) ? n : 0;
    }
}
