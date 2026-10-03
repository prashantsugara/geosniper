#if UNITY_EDITOR
using System.IO;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GeoSniper.Editor
{
    public class AndroidBuildPostprocessor : IPreprocessBuildWithReport, IPostGenerateGradleAndroidProject
    {
        // High callback order so we run after EDM4U (External Dependency Manager)
        public int callbackOrder => 9999;

        public void OnPreprocessBuild(BuildReport report)
        {
            string templatePath = Path.Combine(Application.dataPath, "Plugins/Android/settingsTemplate.gradle");
            if (File.Exists(templatePath))
            {
                PatchGradleFile(templatePath);
            }
        }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            Debug.Log($"[AndroidBuildPostprocessor] Inspecting generated Gradle project at: {path}");

            string[] candidateDirs = new[]
            {
                path,
                Directory.GetParent(path)?.FullName
            };

            foreach (var dir in candidateDirs)
            {
                if (string.IsNullOrEmpty(dir)) continue;

                string settingsFile = Path.Combine(dir, "settings.gradle");
                if (File.Exists(settingsFile))
                {
                    PatchGradleFile(settingsFile);
                }
            }
        }

        private static void PatchGradleFile(string filePath)
        {
            try
            {
                string content = File.ReadAllText(filePath);
                bool modified = false;

                // EDM4U generates unityProjectPath without space-encoding:
                // def unityProjectPath = $/file:///.../$.replace("\\", "/")
                // On Windows paths with spaces ("Geeta Sugara"), Gradle 9 fails to convert URI to File.
                if (content.Contains(".replace(\"\\\\\", \"/\")") && !content.Contains(".replace(\" \", \"%20\")"))
                {
                    content = content.Replace(".replace(\"\\\\\", \"/\")", ".replace(\"\\\\\", \"/\").replace(\" \", \"%20\")");
                    modified = true;
                }

                if (modified)
                {
                    File.WriteAllText(filePath, content);
                    Debug.Log($"[AndroidBuildPostprocessor] Patched {filePath} to encode path spaces for Gradle.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AndroidBuildPostprocessor] Failed to patch {filePath}: {ex.Message}");
            }
        }
    }
}
#endif
