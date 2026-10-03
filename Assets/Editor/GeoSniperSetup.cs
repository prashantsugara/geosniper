using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GeoSniperSetup
{
    [MenuItem("Geo Sniper/Create Bootstrap Scene")]
    public static void CreateScene()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        new GameObject("Geo Sniper").AddComponent<GeoSniper.GeoSniperGame>();
        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/GeoSniper.unity");
        EditorBuildSettings.scenes=new [] {new EditorBuildSettingsScene("Assets/Scenes/GeoSniper.unity",true)};
        PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
        PlayerSettings.companyName="GeoSniper";
        PlayerSettings.productName="Geo Sniper";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android,"com.geosniper.game");
        AssetDatabase.SaveAssets();
    }
}
