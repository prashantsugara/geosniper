using System;
using System.IO;
using GeoSniper;
using UnityEditor;
using UnityEngine;

public static class MapPinChecks
{
    [MenuItem("Geo Sniper/Validate Map Pin Surfaces")]
    public static void Run()
    {
        var root=new GameObject("Pin surface regression");root.transform.position=new Vector3(60000,0,60000);
        var world=root.AddComponent<SectorWorld>();SectorWorld.LoadedWorlds.Add(world);
        GameObject Surface(string name,Vector3 position,Vector3 size)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(root.transform,false);
            obj.transform.localPosition=position;obj.transform.localScale=size;return obj;
        }
        try
        {
            Surface("Local terrain",Vector3.zero,new Vector3(200,2,200));
            Surface("building",new Vector3(20,6,0),new Vector3(10,12,10));
            foreach(float cameraHeight in new[]{0f,50f})
                if(!UrbanCombatMission.TryMapSurface(root.transform.position+new Vector3(20,cameraHeight,0),out var pin) || Mathf.Abs(pin.y-12.12f)>.01f)
                    throw new Exception("Roof pin copied the player's altitude");
            if(!UrbanCombatMission.TryMapSurface(root.transform.position+new Vector3(-20,50,0),out var street) || Mathf.Abs(street.y-1.12f)>.01f)
                throw new Exception("Street pin height is incorrect");
            if(UrbanCombatMission.TryMapSurface(root.transform.position+Vector3.right*700,out _)) throw new Exception("Pin accepted outside loaded area");
            Surface("water",new Vector3(-10,2,30),new Vector3(10,1,10));
            if(UrbanCombatMission.TryMapSurface(root.transform.position+new Vector3(-10,0,30),out _)) throw new Exception("Pin accepted in water");
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/MapPinChecks.txt","PASS: rooftop and street heights, camera altitude independence, unloaded area and water rejection");
        }
        finally {UnityEngine.Object.DestroyImmediate(root);}
    }
}
