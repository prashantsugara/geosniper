using System.IO;
using UnityEditor;

// Explicit local request file allows building in an already open editor.
[InitializeOnLoad]
public static class AndroidDevelopmentBuildRequest
{
    const string Request=".utmp/android-development-build.request";
    static AndroidDevelopmentBuildRequest() { EditorApplication.update+=Poll; }
    static void Poll()
    {
        if(!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android)
        {
            if(!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android,BuildTarget.Android))
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/AndroidBuildResult.txt","FAILED: Could not activate Android build target.");
                File.Delete(Request);
            }
            return;
        }
        File.Delete(Request);
        AndroidBuildValidation.Build();
    }
}
