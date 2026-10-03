using System;
using System.IO;
using GeoSniper;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SpawnFallbackChecks
{
//  [InitializeOnLoadMethod]
    static void QueueChecks()
    {
        if(SessionState.GetBool("GeoSniper.SpawnDiagnosis.2",false)) return;
        SessionState.SetBool("GeoSniper.SpawnDiagnosis.2",true);
        EditorApplication.delayCall+=()=> { if(!EditorApplication.isPlayingOrWillChangePlaymode) MapWorldChecks.Run(); };
    }
    [MenuItem("Geo Sniper/Validate Spawn Fallback")]
    public static void Run()
    {
        var previous=SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var root=new GameObject("Spawn test");
            var world=root.AddComponent<SectorWorld>();
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name="Local terrain";
            ground.transform.SetParent(root.transform);
            ground.transform.position=new Vector3(0,-.5f,0);
            ground.transform.localScale=new Vector3(640,1,640);
            if(!world.TryFindGeographicSpawn(Vector3.zero,out var exact) || new Vector2(exact.x,exact.z).magnitude>.01f)
                throw new Exception("Clear exact coordinate rejected");
            var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.SetParent(root.transform);
            obstacle.transform.position=new Vector3(0,2,0);
            obstacle.transform.localScale=new Vector3(10,4,10);
            if(!world.TryFindGeographicSpawn(Vector3.zero,out var fallback) || new Vector2(fallback.x,fallback.z).magnitude<=3)
                throw new Exception("Obstruction wider than old search prevents spawn");
            ground.SetActive(false);
            if(world.TryFindGeographicSpawn(Vector3.zero,out _))
                throw new Exception("Spawn allowed without supported terrain");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/SpawnFallbackChecks.txt","PASS: exact position, large obstruction fallback, unsupported terrain rejected");
        }
        finally
        {
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene,true);
        }
    }
}
