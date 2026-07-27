using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Proves the prop layout round trip actually works, from the command line.
///
/// <para>The failure this guards against is silent and expensive: you move thirty rocks,
/// click save, regenerate, and they come back in the wrong places or not at all. By then
/// the arrangement is gone. So the round trip gets checked by a machine before it is
/// trusted with anyone's afternoon.</para>
///
/// <para>Open Level01, move a prop somewhere unmistakable, save, regenerate, and confirm
/// it is still there. Development tool, never shipped.</para>
/// </summary>
public static class PropLayoutSelfTest
{
    public static void Run()
    {
        const string scenePath = "Assets/Scenes/Level01.unity";
        const string layoutPath = "Assets/Layouts/level01-props.txt";
        var marker = new Vector3(123.5f, 0f, -77.25f);

        var hadLayout = File.Exists(layoutPath);
        if (hadLayout)
        {
            Debug.LogError("[PropLayoutSelfTest] level01-props.txt already exists. " +
                           "Refusing to run: this test would overwrite a real layout.");
            EditorApplication.Exit(1);
            return;
        }

        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var dressing = FindDressing();
        if (dressing == null || dressing.transform.childCount == 0)
        {
            Debug.LogError("[PropLayoutSelfTest] No Dressing object or it is empty.");
            EditorApplication.Exit(1);
            return;
        }

        var before = dressing.transform.childCount;
        var subject = dressing.transform.GetChild(0);
        var model = subject.name;

        subject.position = marker;
        EditorSceneManager.MarkSceneDirty(subject.gameObject.scene);
        EditorSceneManager.SaveScene(subject.gameObject.scene);

        Debug.Log($"[PropLayoutSelfTest] Moved '{model}' to {marker}. Saving layout.");
        PropLayout.SaveOpenScene();

        if (!File.Exists(layoutPath))
        {
            Debug.LogError("[PropLayoutSelfTest] FAIL: no layout file written.");
            EditorApplication.Exit(1);
            return;
        }

        // The real test. Blow the scene away and rebuild it from scratch.
        Debug.Log("[PropLayoutSelfTest] Regenerating Level 01 from the builder.");
        Level01SceneBuilder.Generate();

        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        dressing = FindDressing();

        var after = dressing == null ? 0 : dressing.transform.childCount;
        var found = false;

        if (dressing != null)
        {
            foreach (Transform child in dressing.transform)
            {
                if (child.name != model)
                    continue;

                if (Vector3.Distance(child.position, marker) < 0.05f)
                {
                    found = true;
                    break;
                }
            }
        }

        // Clean up, so a self test never leaves a fake layout behind for the real game.
        AssetDatabase.DeleteAsset(layoutPath);
        Level01SceneBuilder.Generate();

        if (found && after == before)
        {
            Debug.Log($"[PropLayoutSelfTest] PASS: {after} props restored, and '{model}' " +
                      $"came back at {marker}.");
            EditorApplication.Exit(0);
            return;
        }

        Debug.LogError($"[PropLayoutSelfTest] FAIL: expected {before} props and '{model}' " +
                       $"at {marker}. Got {after} props, marker found = {found}.");
        EditorApplication.Exit(1);
    }

    private static GameObject FindDressing()
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == "Dressing")
                return root;

        return null;
    }
}
