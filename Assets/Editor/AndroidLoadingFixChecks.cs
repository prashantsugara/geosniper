using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class AndroidLoadingFixChecks
{
    public static void ValidateAndBuild()
    {
        if(Application.isBatchMode)
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(EditorBuildSettings.scenes.First(s=>s.enabled).path);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/AndroidLoadingFixChecks.txt","");
        var type=typeof(GeoSniper.GeoSniperGame);
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var budget=type.Assembly.GetType("GeoSniper.WorldLoadingBudget").GetMethod("Deadline",BindingFlags.Static|BindingFlags.NonPublic);
        float Deadline(float start,float progress)=>(float)budget.Invoke(null,new object[]{start,progress});
        Check(Deadline(10,10)==130 && Deadline(10,150)==270,"Advancing generation past two minutes retains its progress window");
        Check(Deadline(10,150)<271,"Stalled generation expires after its progress window");
        Check(Deadline(10,600)==610,"Repeated progress cannot exceed the overall bound");
        var fixture=new GameObject("Loading budget fixture");
        try
        {
            var game=fixture.AddComponent<GeoSniper.GeoSniperGame>();
            type.GetField("loading",flags).SetValue(game,true);
            type.GetField("loadingDeadline",flags).SetValue(game,100f);
            type.GetField("worldLoadingStarted",flags).SetValue(game,10f);
            type.GetField("loadingPausedAt",flags).SetValue(game,10f);
            type.GetMethod("ResumeLoadingAfterPause",flags).Invoke(game,new object[]{40f});
            Check((float)type.GetField("loadingDeadline",flags).GetValue(game)==130f && (float)type.GetField("worldLoadingStarted",flags).GetValue(game)==40f,"Background time is excluded from loading budgets");
            type.GetField("startupStage",flags).SetValue(game,"WORLD");
            type.GetField("loadingDeadline",flags).SetValue(game,-1f);
            type.GetMethod("Update",flags).Invoke(game,null);
            Check((bool)type.GetField("loadFailed",flags).GetValue(game) && !(bool)type.GetField("loading",flags).GetValue(game),"Expired world loads still enter retryable recovery");
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture); }
        RuntimeErrorChecks.Run();
        foreach(string model in new[]{"BrickApartments","MilitaryTent","Warehouse","ShopShelf","ShopFridge"})
        {
            string path="Assets/Resources/Models/LevelSites/"+model+".fbx";
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            var meshes=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToArray();
            if(!importer.isReadable || meshes.Length==0 || meshes.Any(m=>!m.isReadable))
                throw new InvalidOperationException("Android runtime collision data missing: "+model);
            Check(true,"Player collision mesh data retained for "+model);
        }
        AndroidBuildValidation.Build();
    }
    static void Check(bool passed,string message)
    {
        File.AppendAllText("Logs/AndroidLoadingFixChecks.txt",(passed?"PASS: ":"FAIL: ")+message+"\n");
        if(!passed)throw new InvalidOperationException(message);
    }
}
