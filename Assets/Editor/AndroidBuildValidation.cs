using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class AndroidBuildValidation
{
    public static void ValidateAndBuild()
    {
        if (UnityEngine.Application.isBatchMode)
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(EditorBuildSettings.scenes.First(s => s.enabled).path);
        MapLoadingChecks.Run();
        MapWorldChecks.Run();
        if (File.ReadAllText("Logs/MapWorldChecks.txt").Contains("FAIL:"))
            throw new InvalidOperationException("Map regression checks failed; APK build cancelled.");
        Build();
    }

    [MenuItem("Geo Sniper/Build Android Test APK")]
    public static void QueueBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("Stop Play mode and wait for the current build first.");
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/AndroidBuildResult.txt", "QUEUED");
        EditorApplication.delayCall += Build;
    }

    public static void Build()
    {
        try
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("No enabled build scenes.");
            Directory.CreateDirectory("Builds");
            bool wasBundle = EditorUserBuildSettings.buildAppBundle;
            bool wasCustomSigning = PlayerSettings.Android.useCustomKeystore;
            try
            {
                EditorUserBuildSettings.buildAppBundle = false;
                PlayerSettings.Android.useCustomKeystore = false;
                File.WriteAllText("Logs/AndroidBuildResult.txt", "BUILDING");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = scenes,
                    locationPathName = "Builds/GeoSniper-tested.apk",
                    target = BuildTarget.Android,
                    options = BuildOptions.Development
                });
                File.WriteAllText("Logs/AndroidBuildResult.txt",
                    report.summary.result + " | Errors: " + report.summary.totalErrors);
                if (report.summary.result != BuildResult.Succeeded)
                    UnityEngine.Debug.LogError("Android test APK failed. See Logs/Editor.log.");
            }
            finally { EditorUserBuildSettings.buildAppBundle = wasBundle; PlayerSettings.Android.useCustomKeystore = wasCustomSigning; }
        }
        catch (Exception error)
        {
            File.WriteAllText("Logs/AndroidBuildResult.txt", "FAILED: " + error);
            UnityEngine.Debug.LogException(error);
        }
    }
}
