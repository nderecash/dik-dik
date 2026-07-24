using System.IO;
using Dikdik.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Renders a level scene to a PNG from the command line, so the look can be checked
/// without launching the game. Development tool, not shipped.
///
/// Run: Unity.exe -batchmode -projectPath . -executeMethod Screenshotter.Shoot -logFile s.log
/// (No -nographics: it needs a graphics device to actually render.)
/// </summary>
public static class Screenshotter
{
    public static void Shoot()
    {
        var scenePath = GetArg("-scene", "Assets/Scenes/Level01.unity");
        var outPath = GetArg("-shot", "C:/dev/dik-dik/shot.png");

        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var camera = Object.FindAnyObjectByType<Camera>();
        var rover = GameObject.Find("Salty");
        if (camera == null || rover == null)
        {
            Debug.LogError("[Screenshotter] camera or rover missing");
            EditorApplication.Exit(1);
            return;
        }

        // Place the camera the way CameraFollow would at the start, since Start() does
        // not run in edit mode.
        var offset = new Vector3(0f, 6.5f, -12f);
        camera.transform.position = rover.transform.TransformPoint(offset);
        var focus = rover.transform.position + rover.transform.forward * 18f + Vector3.up * 3.5f;
        camera.transform.rotation = Quaternion.LookRotation(focus - camera.transform.position, Vector3.up);

        const int w = 1280, h = 720;
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 2;
        camera.targetTexture = rt;
        camera.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();

        camera.targetTexture = null;
        RenderTexture.active = null;

        File.WriteAllBytes(outPath, tex.EncodeToPNG());
        Debug.Log($"[Screenshotter] wrote {outPath}");

        EditorApplication.Exit(0);
    }

    private static string GetArg(string name, string fallback)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == name)
                return args[i + 1];

        return fallback;
    }
}
