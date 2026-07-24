using UnityEditor;
using UnityEngine;

/// <summary>Dev tool: report the bounds of imported Kenney models so scenes can scale them.</summary>
public static class KenneyInspect
{
    public static void Run()
    {
        foreach (var name in new[] { "rover", "rock", "rock_largeA", "crater", "meteor", "satelliteDish" })
        {
            var path = $"Assets/Kenney/Models/{name}.fbx";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.Log($"[KenneyInspect] MISSING {name}"); continue; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var b = new Bounds();
            var first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            Debug.Log($"[KenneyInspect] {name}: size={b.size:F2} center={b.center:F2} " +
                      $"renderers={go.GetComponentsInChildren<Renderer>().Length}");
            Object.DestroyImmediate(go);
        }
        EditorApplication.Exit(0);
    }
}
