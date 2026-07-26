using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Command line builds. There is no hand-clicked build in this project.
///
/// Run:
///   Unity.exe -batchmode -quit -projectPath . -executeMethod DikdikBuild.Windows -logFile build.log
///   Unity.exe -batchmode -quit -projectPath . -executeMethod DikdikBuild.Web     -logFile build.log
///
/// Then check the output file exists. Unity batch mode detaches on Windows, so the
/// shell's exit code is not a reliable signal that anything happened.
/// </summary>
public static class DikdikBuild
{
    private const string WindowsPath = "Builds/Windows/Dikdik.exe";
    private const string WebPath = "Builds/Web";

    [MenuItem("Dikdik/Build/Windows")]
    public static void Windows()
    {
        Shared();

        // IL2CPP for the shipping Windows build. The throwaway spike used Mono because
        // it builds in a fraction of the time and nobody was going to download it.
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);

        Run(new BuildPlayerOptions
        {
            scenes = Scenes(),
            locationPathName = WindowsPath,
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None
        });
    }

    /// <summary>
    /// Playtest build. Mono rather than IL2CPP, because IL2CPP takes minutes and this
    /// exists to be thrown away after twenty minutes of someone driving a rover around.
    /// Shipping builds still use <see cref="Windows"/>.
    /// </summary>
    [MenuItem("Dikdik/Build/Windows (fast playtest)")]
    public static void WindowsFast()
    {
        Shared();
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

        Run(new BuildPlayerOptions
        {
            scenes = Scenes(),
            locationPathName = "Builds/Playtest/Dikdik.exe",
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.Development
        });
    }

    [MenuItem("Dikdik/Build/Web")]
    public static void Web()
    {
        Shared();

        // The browser build has no voice. whisper.cpp is native code with no WebGL
        // target, so VoiceCommandProducer is compiled out with #if !UNITY_WEBGL and the
        // settings screen says voice is Windows only, rather than showing a microphone
        // button that quietly does nothing.
        //
        // FIXED. Kept here because the failure was not obvious and the fix is not either.
        //
        // com.whisper.unity.asmdef declared empty includePlatforms AND empty
        // excludePlatforms, which Unity reads as "every platform", WebGL included. Its
        // DllImport declarations then had no library to bind to and the build died at
        // Emscripten link time on undefined symbols, not at C# compile time. Guarding our
        // own code was never going to be enough; the package's own assembly had to be
        // excluded.
        //
        // PackageCache is immutable, so the asmdef could not simply be edited. The package
        // is now embedded at Packages/com.whisper.unity with "excludePlatforms": ["WebGL"]
        // and the git URL dropped from the manifest. That also pins a dependency which has
        // no registry release and could otherwise have moved under the project mid-build.
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;

        // WebGL.memorySize was a deprecated no-op and is gone. Unity 6 grows the heap
        // itself; setting it did nothing except look like it was doing something.

        // Not None. Unity has a known build failure with exception support fully disabled,
        // and this is the cheapest setting that still builds.
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

        // itch.io serves compressed builds correctly, so the JS fallback is dead weight.
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.dataCaching = true;

        Run(new BuildPlayerOptions
        {
            scenes = Scenes(),
            locationPathName = WebPath,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None
        }, StripWhisperModels);
    }

    /// <summary>
    /// Delete the Whisper model weights from the finished web build.
    ///
    /// <para>Unity copies everything in StreamingAssets to every platform, so the browser
    /// build shipped 148 MB of model weights for a recogniser that is compiled out of it.
    /// That was 89% of the download, for a feature the build does not have.</para>
    ///
    /// <para>Done after the build rather than by moving files out of the project first.
    /// Moving them would mean a failed or interrupted build leaves the project missing its
    /// models, and the next Windows build then silently has no voice. Deleting from the
    /// output can only ever damage output.</para>
    /// </summary>
    private static void StripWhisperModels()
    {
        var folder = Path.Combine(WebPath, "StreamingAssets", "Whisper");

        if (!Directory.Exists(folder))
            return;

        var freed = 0L;
        foreach (var file in Directory.GetFiles(folder, "*.bin"))
        {
            freed += new FileInfo(file).Length;
            File.Delete(file);
        }

        Debug.Log($"[DikdikBuild] Web: removed {freed / 1048576} MB of Whisper models. " +
                  "The browser build is keyboard only and cannot use them.");
    }

    /// <summary>Settings that must be identical across both builds.</summary>
    private static void Shared()
    {
        PlayerSettings.companyName = "Moses Nderemani";
        PlayerSettings.productName = "Dik-dik";

        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 720;
        PlayerSettings.resizableWindow = true;

        // The game keeps listening when the window loses focus. Alt-tabbing mid sentence
        // should not silently drop what you were saying.
        PlayerSettings.runInBackground = true;
    }

    /// <summary>
    /// The shipped scene list, in order, declared here rather than in the editor's
    /// build settings window.
    ///
    /// Build settings is a binary asset that someone has to remember to update, and a
    /// level missing from it fails silently: the build succeeds and the level simply
    /// is not in the game. Declaring it in code means a missing scene is a compile-time
    /// visible line in a diff, and the editor list is written from this rather than
    /// read into it.
    ///
    /// Boot must be first. It creates the bus, the producers and the journal, then
    /// loads the level after it.
    /// </summary>
    private static readonly string[] ShippedScenes =
    {
        "Assets/Scenes/Boot.unity",
        "Assets/Scenes/Level01.unity",
        "Assets/Scenes/Level02.unity",
        "Assets/Scenes/Level03.unity",
        "Assets/Scenes/Level04.unity",
        "Assets/Scenes/Level05.unity",
        "Assets/Scenes/Level06.unity"
    };

    private static string[] Scenes()
    {
        var missing = ShippedScenes.Where(path => !File.Exists(path)).ToArray();
        if (missing.Length > 0)
            throw new InvalidOperationException(
                "Scenes listed for the build do not exist: " + string.Join(", ", missing));

        // Keep the editor's list in step, so opening the project by hand behaves the
        // same way the command line build does.
        EditorBuildSettings.scenes = ShippedScenes
            .Select(path => new EditorBuildSettingsScene(path, true))
            .ToArray();

        return ShippedScenes;
    }

    /// <param name="afterBuild">
    /// Runs on success, before the editor quits. It has to be a parameter rather than a
    /// line after the Run call, because this method ends in EditorApplication.Exit and
    /// nothing written after it ever executes. That silently swallowed the model-stripping
    /// step on the first attempt: the build reported success, the log had no complaint,
    /// and the output was still 167 MB.
    /// </param>
    private static void Run(BuildPlayerOptions options, Action afterBuild = null)
    {
        var directory = Path.GetDirectoryName(options.locationPathName);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        Debug.Log($"[DikdikBuild] {options.target}: {summary.result}, " +
                  $"{summary.totalErrors} errors, {summary.totalWarnings} warnings, " +
                  $"{summary.totalSize / (1024 * 1024)} MB, output {summary.outputPath}");

        if (summary.result != BuildResult.Succeeded)
        {
            foreach (var step in report.steps)
            foreach (var message in step.messages)
                if (message.type == LogType.Error || message.type == LogType.Exception)
                    Debug.LogError($"[DikdikBuild] {step.name}: {message.content}");

            EditorApplication.Exit(1);
            return;
        }

        afterBuild?.Invoke();

        EditorApplication.Exit(0);
    }
}
