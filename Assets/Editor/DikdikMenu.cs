using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor conveniences for working on this project by hand.
///
/// <para>Two gaps this fills. There was no menu item for regenerating every scene at once,
/// only one per level, so a change to shared code meant clicking seven things in the right
/// order. And pressing Play with a level scene open starts a game with no CommandBus, no
/// comms panel and no voice, because all three live in the Boot scene, which looks exactly
/// like the game being broken.</para>
/// </summary>
public static class DikdikMenu
{
    private const string BootScene = "Assets/Scenes/Boot.unity";
    private const string PlayFromBootMenu = "Dikdik/Play From Boot";

    /// <summary>
    /// Regenerate every scene. Destroys anything moved by hand in the editor.
    ///
    /// <para>Confirmed first, because that is exactly what somebody who has spent twenty
    /// minutes nudging rocks around does not want to find out by doing it.</para>
    /// </summary>
    [MenuItem("Dikdik/Generate All Scenes", priority = 0)]
    public static void GenerateAllScenes()
    {
        var ok = EditorUtility.DisplayDialog(
            "Regenerate every scene?",
            "This rebuilds all seven scenes from the builder scripts.\n\n" +
            "Anything you moved, added or deleted by hand in the editor will be gone. " +
            "Changes made in the builder scripts are what survive.\n\n" +
            "Save your work first if you have not.",
            "Regenerate",
            "Cancel");

        if (!ok)
            return;

        GenerateAll.Generate();
    }

    /// <summary>
    /// Make Play always start from Boot, whatever scene is open.
    ///
    /// <para>Boot holds the command bus, the comms panel, the voice arbiter and the
    /// settings menu, all marked DontDestroyOnLoad. Pressing Play on Level03 with none of
    /// those present gives you a rover that ignores you and a screen with no console, which
    /// reads as a broken game rather than a missing scene.</para>
    ///
    /// <para>Boot then loads Level01. To reach any other level from there, press Escape and
    /// use the Levels tab, which is a shipped feature and not a debug shortcut: nothing in
    /// this game is locked.</para>
    /// </summary>
    [MenuItem(PlayFromBootMenu, priority = 20)]
    public static void TogglePlayFromBoot()
    {
        var on = EditorSceneManager.playModeStartScene != null;

        if (on)
        {
            EditorSceneManager.playModeStartScene = null;
            Debug.Log("[Dikdik] Play from Boot is OFF. Play now starts in whatever scene " +
                      "is open, which for a level scene means no command bus and no voice.");
        }
        else
        {
            var boot = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScene);
            if (boot == null)
            {
                Debug.LogError($"[Dikdik] {BootScene} not found. Run Generate All Scenes.");
                return;
            }

            EditorSceneManager.playModeStartScene = boot;
            Debug.Log("[Dikdik] Play from Boot is ON. Press Escape in game and use the " +
                      "Levels tab to reach any sector.");
        }
    }

    [MenuItem(PlayFromBootMenu, validate = true)]
    private static bool TogglePlayFromBootValidate()
    {
        Menu.SetChecked(PlayFromBootMenu, EditorSceneManager.playModeStartScene != null);
        return true;
    }

    /// <summary>
    /// Open Boot, since it is the one scene you almost always want and the one nobody
    /// thinks to look for.
    /// </summary>
    [MenuItem("Dikdik/Open Boot Scene", priority = 21)]
    public static void OpenBoot()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene(BootScene);
    }
}
