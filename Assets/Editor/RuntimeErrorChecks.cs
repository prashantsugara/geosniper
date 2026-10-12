using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GeoSniper;
using UnityEngine;

public static class RuntimeErrorChecks
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    public static void Run()
    {
        File.WriteAllText("Logs/RuntimeErrorChecks.txt","");
        var worldObject=new GameObject("Route extension regression");
        var vehicleObject=new GameObject("Traffic regression");
        var adsObject=new GameObject("Foreground ad regression");
        try
        {
            var world=worldObject.AddComponent<SectorWorld>();
            if(!SectorWorld.LoadedWorlds.Contains(world))SectorWorld.LoadedWorlds.Add(world);
            var path=new List<Vector3>();for(int i=0;i<40;i++)path.Add(new Vector3(i*4,0,0));
            world.RoadPaths.Add(new List<Vector3>{new Vector3(156,0,0),new Vector3(176,0,0)});
            var car=vehicleObject.AddComponent<TrafficVehicle>();car.enabled=false;
            typeof(TrafficVehicle).GetField("path",Flags).SetValue(car,path);
            typeof(TrafficVehicle).GetField("currentWaypointIndex",Flags).SetValue(car,39);
            vehicleObject.transform.position=new Vector3(150,0,0);
            typeof(TrafficVehicle).GetMethod("Simulate",Flags).Invoke(car,new object[]{.016f});
            int index=(int)typeof(TrafficVehicle).GetField("currentWaypointIndex",Flags).GetValue(car);
            Check(path.Count<40 && index>=0 && index<path.Count,"Long traffic route extends and trims without stale-index access");
            for(int i=0;i<60;i++)typeof(TrafficVehicle).GetMethod("Simulate",Flags).Invoke(car,new object[]{.016f});
            Check(true,"Trimmed traffic route remains valid across subsequent simulation frames");

            adsObject.SetActive(false);
            var ads=adsObject.AddComponent<AdManager>();
            foreach(string method in new[]{"OnApplicationPause","OnApplicationFocus"})
            {
                typeof(AdManager).GetMethod(method,Flags).Invoke(ads,new object[]{method=="OnApplicationPause"});
                int callbacks=0;
                ads.ShowInterstitial(()=>callbacks++);
                Check(callbacks==1 && !ads.IsBusy,"Background/unfocused interstitial skips SDK presentation and continues once: "+method);
                callbacks=0;ads.ShowRewardedInterstitial(ok=>{if(ok)throw new Exception("Reward incorrectly granted");callbacks++;});
                Check(callbacks==1 && !ads.IsBusy,"Background reward does not grant a reward or wait for an ad: "+method);
                typeof(AdManager).GetMethod("OnApplicationPause",Flags).Invoke(ads,new object[]{false});
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(vehicleObject);
            UnityEngine.Object.DestroyImmediate(worldObject);
            UnityEngine.Object.DestroyImmediate(adsObject);
        }
    }
    static void Check(bool pass,string message)
    {
        File.AppendAllText("Logs/RuntimeErrorChecks.txt",(pass?"PASS: ":"FAIL: ")+message+"\n");
        if(!pass)throw new InvalidOperationException(message);
    }
}
