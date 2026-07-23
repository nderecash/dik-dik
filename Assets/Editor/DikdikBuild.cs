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

    [MenuItem("Dikdik/Build/Web")]
    public static void Web()
    {
        Shared();

        // The browser build has no voice. whisper.cpp is native code with no WebGL
        // target, so VoiceCommandProducer is compiled out with #if !UNITY_WEBGL and the
        // settings screen says voice is Windows only, rather than showing a microphone
        // button that quietly does nothing.
        //
        // KNOWN ISSUE, must be fixed before this target will build:
        // com.whisper.unity.asmdef declares empty includePlatforms AND empty
        // excludePlatforms, which means "every platform", WebGL included. Its DllImport
        // declarations then have no library to bind to and the build fails on undefined
        // symbols. Excluding our own code is not enough; the package's own assembly has
        // to be excluded too.
        //
        // Fix: embed the package (copy PackageCache/com.whisper.unity@<hash> into
        // Packages/com.whisper.unity), then set "excludePlatforms": ["WebGL"] in its
        // asmdef. Embedding also pins a dependency that has no registry release and
        // could otherwise move under us mid-project.
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.WebGL, ScriptingImplementation.IL2CPP);
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.memorySize = 512;

        // itch.io serves these fine, and the exception support costs size we do not need.
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;

        Run(new BuildPlayerOptions
        {
            scenes = Scenes(),
            locationPathName = WebPath,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None
        });
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
    /// Every enabled scene in build settings, in order. Kept as one source of truth so a
    /// level added to the editor list cannot be missing from the shipped build.
    /// </summary>
    private static string[] Scenes()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
            throw new InvalidOperationException(
                "No enabled scenes in build settings. Nothing would ship.");

        return scenes;
    }

    private static void Run(BuildPlayerOptions options)
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

        EditorApplication.Exit(0);
    }
}
