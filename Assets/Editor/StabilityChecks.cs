using System;
using System.IO;
using System.Text;
using GeoSniper;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StabilityChecks
{
    [MenuItem("Geo Sniper/Inspect Imported Models")]
    public static void InspectModels()
    {
        Directory.CreateDirectory("Logs");
        var report=new StringBuilder();
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            foreach(string[] names in new[]{new[]{"GeoSniperRifleProvided","GeoSniperRifle"},new[]{"Enemies/army_character_1","Enemies/army_character_2"},new[]{"Bullet50Cal","Bullet50Cal"}})
            {
                var asset=Resources.Load<GameObject>("Models/"+names[0]);
                var name=names[0];
                if(asset==null) { asset=Resources.Load<GameObject>("Models/"+names[1]); name=names[1]; }
                if(asset==null) throw new Exception("Missing Resources model: "+names[0]+" or "+names[1]);
                var instance=UnityEngine.Object.Instantiate(asset);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance,scene);
                report.AppendLine(name);
                foreach(var renderer in instance.GetComponentsInChildren<Renderer>())
                    report.AppendLine(renderer.name+" center="+renderer.bounds.center.ToString("F3")+" size="+renderer.bounds.size.ToString("F3"));
                UnityEngine.Object.DestroyImmediate(instance);
            }
            File.WriteAllText("Logs/ModelInspection.txt",report.ToString());
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
