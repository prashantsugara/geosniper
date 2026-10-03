using System;
using System.IO;
using GeoSniper;
using UnityEditor;
using UnityEngine;

public static class TrainingMissionChecks
{
    [MenuItem("Geo Sniper/Test Mission In Play Mode")]
    public static void Run()
    {
        if(!Application.isPlaying) throw new Exception("Enter Play mode first");
        var root=new GameObject("Mission test");
        var cameraObject=new GameObject("Test camera");
        var camera=cameraObject.AddComponent<Camera>(); camera.enabled=false;
        var mission=root.AddComponent<TrainingMission>();
        try
        {
            mission.Begin(camera);
            camera.transform.LookAt(root.transform.Find("Training Board 1"));
            mission.Fire();
            if(mission.Hits!=1 || mission.Ammo!=4) throw new Exception("Center ray must hit beacon and consume one round");
            mission.Fire();
            if(mission.Hits!=1 || mission.Ammo!=4) throw new Exception("Cooldown must prevent duplicate shots");
            mission.Begin(camera);
            if(mission.Hits!=0 || mission.Ammo!=5 || mission.Finished) throw new Exception("Retry must reset mission");
            File.WriteAllText("Logs/TrainingMissionChecks.txt","PASS: hit detection, ammo consumption, shot cooldown, retry reset");
        }
        finally { UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(cameraObject); }
    }
}
