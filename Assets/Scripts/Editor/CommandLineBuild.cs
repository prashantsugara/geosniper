#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GeoSniper.Editor
{
    public static class CommandLineBuild
    {
        public static void Build()
        {
            string output = "build/GeoSniper.apk";
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/GeoSniper.unity" },
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log("BUILD SUCCESS: " + output);
            else
                Debug.LogError("BUILD FAILED: " + report.summary.result);
        }
    }
}
#endif
