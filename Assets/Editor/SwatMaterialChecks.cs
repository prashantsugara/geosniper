using System;
using System.IO;
using System.Reflection;
using GeoSniper;
using UnityEditor;
using UnityEngine;

public static class SwatMaterialChecks
{
    [MenuItem("Geo Sniper/Validate SWAT Skin Materials")]
    public static void Run()
    {
        var prefab=Resources.Load<GameObject>("Models/Enemies/swat");
        var bodyTexture=Resources.Load<Texture2D>("Models/Enemies/Textures/Soldier_Body_diffuse");
        var faceTexture=Resources.Load<Texture2D>("Models/Enemies/Textures/Soldier_head_diffuse");
        if(prefab==null || bodyTexture==null || faceTexture==null) throw new Exception("SWAT model or textures missing");
        var lobbyRoot=UnityEngine.Object.Instantiate(prefab);
        var gameObject=new GameObject("SWAT material check");
        try
        {
            var game=gameObject.AddComponent<GeoSniperGame>();
            typeof(GeoSniperGame).GetMethod("SetupLobbyCharacterMaterials",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(game,new object[]{lobbyRoot});
            Check(lobbyRoot,bodyTexture,faceTexture,"Lobby");

            var missionRoot=UnityEngine.Object.Instantiate(prefab);
            try
            {
                var mission=gameObject.AddComponent<UrbanCombatMission>();
                typeof(UrbanCombatMission).GetMethod("ApplyEnemyMaterials",BindingFlags.Instance|BindingFlags.NonPublic)
                    .Invoke(mission,new object[]{missionRoot.transform});
                Check(missionRoot,bodyTexture,faceTexture,"Gameplay");
            }
            finally { UnityEngine.Object.DestroyImmediate(missionRoot); }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/SwatMaterialChecks.txt","PASS: helmet and face use distinct correct textures in lobby and gameplay\n");
            Debug.Log("PASS: SWAT helmet and face material assignments");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(lobbyRoot);
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    static void Check(GameObject root,Texture2D body,Texture2D face,string context)
    {
        SkinnedMeshRenderer head=null;
        foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if(skin.name=="Soldier_head") head=skin;
        if(head==null || head.sharedMesh==null || head.sharedMesh.subMeshCount!=2)
            throw new Exception(context+": SWAT head mesh changed");
        var materials=head.sharedMaterials;
        if(materials.Length!=2 || materials[0].mainTexture!=body || materials[1].mainTexture!=face)
            throw new Exception(context+": helmet and face textures are assigned to the wrong submeshes");
    }
}
