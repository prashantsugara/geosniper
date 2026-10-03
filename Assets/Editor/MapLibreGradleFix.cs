using System.IO;
using UnityEditor.Android;

namespace GeoSniper.Editor
{
    // Unity's GameActivity and frame pacing both require Prefab. Match MapLibre's
    // shared STL instead of disabling native dependency discovery.
    public sealed class MapLibreGradleFix : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1000;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string buildGradle = Path.Combine(path, "build.gradle");
            if (!File.Exists(buildGradle)) return;

            string text = File.ReadAllText(buildGradle);
            string configured = ConfigureGradle(text);
            if (configured != text) File.WriteAllText(buildGradle, configured);
        }

        internal static string ConfigureGradle(string text)
        {
            // Remove the override left by older exports, including incremental builds.
            return text.Replace("android.buildFeatures { prefab false }", "")
                .Replace("-DANDROID_STL=c++_static", "-DANDROID_STL=c++_shared");
        }
    }
}
