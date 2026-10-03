using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using GeoSniper;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MapLoadingChecks
{
    [MenuItem("Geo Sniper/Validate Map Loading")]
    public static void Run()
    {
        Directory.CreateDirectory("Logs");
        string folder=Path.Combine(Path.GetFullPath("Logs"),"MapLoadingChecks-"+Guid.NewGuid().ToString("N"));
        var report=new StringBuilder();
        var previous=SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var origin=new GameLocation{Latitude=0,Longitude=0,Label="TEST"};
            var neighbour=new GameLocation{Latitude=0,Longitude=640.0/111320};
            var road=new MapFeature{Kind="road",Name="Test Road",Height=3,Points=new List<Vector2>{new Vector2(-100,0),new Vector2(100,0)}};
            var features=new List<MapFeature>{road};
            var cache=new SectorMapCache(folder);
            cache.Save(features,origin); cache.Save(features,neighbour);
            Check(cache.Read(origin,TimeSpan.FromDays(1))!=null && cache.Read(neighbour,TimeSpan.FromDays(1))!=null,"Neighbour downloads do not overwrite the original sector",report);
            cache=new SectorMapCache(folder);
            Check(cache.Read(origin,TimeSpan.FromDays(1))!=null,"Sector cache survives restart",report);
            var drift=new GameLocation{Latitude=0,Longitude=5.0/111320};
            var shifted=cache.Read(drift,TimeSpan.FromDays(1));
            Check(shifted!=null && Mathf.Abs(shifted[0].Points[0].x+105)<.001f,"GPS drift reprojects cached buildings and roads rather than shifting the map",report);
            Check(Mathf.Abs(cache.Read(origin,TimeSpan.FromDays(1))[0].Points[0].x+100)<.001f,"Reading a shifted cache does not mutate the saved geometry",report);
            Check(cache.Read(new GameLocation{Latitude=0,Longitude=30.0/111320},TimeSpan.FromDays(1))==null,"Cache never substitutes a distant sector",report);
            string path=Path.Combine(folder,"sector-"+SectorMapCache.Key(origin)+".json");
            File.SetLastWriteTimeUtc(path,DateTime.UtcNow.AddDays(-2));
            Check(cache.Read(origin,TimeSpan.FromDays(1))==null && cache.Read(origin,TimeSpan.FromDays(7))!=null,"Stale cache is fallback only",report);
            File.SetLastWriteTimeUtc(path,DateTime.UtcNow.AddDays(-8));
            Check(cache.Read(origin,TimeSpan.FromDays(7))==null,"Expired cache is rejected",report);
            File.WriteAllText(path,"{broken");
            Check(cache.Read(origin,TimeSpan.FromDays(7))==null,"Corrupt cache does not break loading",report);
            for(int i=1;i<=34;i++) cache.Save(features,new GameLocation{Latitude=0,Longitude=i*.01});
            Check(Directory.GetFiles(folder,"sector-*.json").Length==32,"Disk cache is bounded to 32 sectors",report);

            const string roadJson="{\"type\":\"way\",\"tags\":{\"highway\":\"residential\"},\"geometry\":[{\"lat\":0,\"lon\":-.001},{\"lat\":0,\"lon\":.001}]}";
            var map=SectorMap.Parse("{\"elements\":[{\"type\":\"node\",\"tags\":{\"shop\":\"yes\",\"name\":\"Broken\"}},"+roadJson+"]}",origin);
            Check(map.Exists(f=>f.Kind=="road"),"One malformed feature does not discard a valid map",report);
            bool partialRejected=false;
            try { SectorMap.Parse("{\"remark\":\"runtime timeout\",\"elements\":["+roadJson+"]}",origin); }
            catch(FormatException) { partialRejected=true; }
            Check(partialRejected,"Overpass partial/timeout responses are not accepted as complete maps",report);
            Check(SectorMap.BuildQuery(origin).Contains("[timeout:30]") && SectorMap.LoadTimeoutSeconds>100+2*SectorMap.RequestTimeoutSeconds+34,"Startup budget covers service, failover and rate-limit cooldown",report);

            var game=new GameObject("Loading failure fixture").AddComponent<GeoSniperGame>();
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var type=typeof(GeoSniperGame);
            type.GetField("activeLocation",flags).SetValue(game,origin);
            type.GetField("startupStage",flags).SetValue(game,"MAP DOWNLOAD");
            type.GetField("loading",flags).SetValue(game,true);
            type.GetField("loadingDeadline",flags).SetValue(game,-1f);
            type.GetMethod("Update",flags).Invoke(game,null);
            Check((bool)type.GetField("loadFailed",flags).GetValue(game) && !(bool)type.GetField("loading",flags).GetValue(game) && type.GetField("world",flags).GetValue(game)==null,"Watchdog shows a retryable error without creating an offline grid",report);
            Check(ReferenceEquals(type.GetField("activeLocation",flags).GetValue(game),origin),"Retry retains the exact selected location",report);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene,true);
            if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            File.WriteAllText("Logs/MapLoadingChecks.txt",report.ToString());
            if(Directory.Exists(folder) && folder.StartsWith(Path.GetFullPath("Logs")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) Directory.Delete(folder,true);
        }
        Debug.Log("Map loading checks passed. See Logs/MapLoadingChecks.txt.");
    }

    static void Check(bool passed,string message,StringBuilder report)
    {
        report.AppendLine((passed?"PASS: ":"FAIL: ")+message);
        if(!passed) throw new InvalidOperationException(message);
    }
}
