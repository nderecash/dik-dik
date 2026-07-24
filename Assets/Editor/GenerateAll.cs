using UnityEditor;
using UnityEngine;

/// <summary>
/// Regenerates every scene in one batch-mode invocation, so an environment change lands
/// across all levels at once instead of six separate runs. Development tool.
/// </summary>
public static class GenerateAll
{
    public static void Generate()
    {
        BootSceneBuilder.Generate();
        Level01SceneBuilder.Generate();
        Level03SceneBuilder.Generate();
        Level04SceneBuilder.Generate();
        Level05SceneBuilder.Generate();
        Level06SceneBuilder.Generate();

        Debug.Log("[GenerateAll] All scenes regenerated.");
    }
}
