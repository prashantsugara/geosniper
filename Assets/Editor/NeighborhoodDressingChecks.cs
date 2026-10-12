using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeoSniper;
using Newtonsoft.Json.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class NeighborhoodDressingChecks
{
    public static void Run()
    {
        var scene=EditorSceneManager.NewPreviewScene();
        var root=new GameObject("Neighborhood validation");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
        var world=root.AddComponent<SectorWorld>();world.BuildNavigation=false;
        root.transform.position=new Vector3(10000,0,10000);world.DetailFocus=root.transform.position;
        var features=new List<MapFeature>();
        foreach(float z in new[]{-70f,0f,70f})features.Add(new MapFeature{Kind="road",Name="Neighborhood avenue",Details=new JObject{["class"]="residential"},Points=new List<Vector2>{new Vector2(-240,z),new Vector2(240,z)}});
        for(int i=0;i<6;i++)features.Add(new MapFeature{Kind="building",Height=8+i%3*3,Points=new List<Vector2>{new Vector2(-100+i*40,24),new Vector2(-80+i*40,24),new Vector2(-80+i*40,44),new Vector2(-100+i*40,44)}});
        try
        {
            world.Generate(features);Physics.SyncTransforms();
            Check(world.NeighborhoodSites>=8,"Too few neighborhood scenes generated");
            Check(world.NeighborhoodVehicles>=2,"Additional vehicle parking missing");
            var downloaded=root.GetComponentsInChildren<DownloadedStreetPropMarker>();
            Check(downloaded.Length>=8,"Downloaded street furniture missing");
            Check(downloaded.Select(p=>p.name).Distinct().Count()==10,"Downloaded prop variety missing");
            foreach(var prop in downloaded)foreach(var renderer in prop.GetComponentsInChildren<Renderer>())
            {
                Check(renderer.bounds.size.magnitude<5,"Imported prop has incorrect scale");
                Check(renderer.sharedMaterials.All(m=>m!=null && m.mainTexture!=null),"Imported prop atlas missing");
            }
            var sites=root.GetComponentsInChildren<Transform>().Where(t=>t.parent==root.transform && (t.name.StartsWith("Neighborhood parking") || t.name=="Market stall" || t.name=="Bus shelter" || t.name=="Delivery stacks" || t.name=="Utility work zone" || t.name=="Garden seating")).ToArray();
            Check(sites.Select(s=>s.name).Distinct().Count()>=4,"Roadside scene variety missing");
            foreach(var site in sites)
            {
                Check(new Vector2(site.localPosition.x,site.localPosition.z).magnitude>=12,"Entry area obstructed");
                Check(site.GetComponentsInChildren<Renderer>().All(r=>r.gameObject.activeSelf),"Batching left inactive geometry");
                foreach(var collider in site.GetComponentsInChildren<Collider>())
                {
                    Check(collider.bounds.size.x<8 && collider.bounds.size.z<8,"Oversized dressing collider");
                    foreach(float roadZ in new[]{-70f,0f,70f})
                        Check(collider.bounds.min.z>root.transform.position.z+roadZ+MapFeatureStyle.RoadWidth(features[0])*.5f || collider.bounds.max.z<root.transform.position.z+roadZ-MapFeatureStyle.RoadWidth(features[0])*.5f,"Dressing blocks a travel lane");
                }
            }
            Check(world.TryFindGeographicSpawn(root.transform.position,out _),"Dressing invalidates player entry spawn");
            var camera=new GameObject("Neighborhood camera").AddComponent<Camera>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject,scene);camera.scene=scene;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.65f,.75f,.82f);camera.farClipPlane=600;
            camera.transform.position=root.transform.position+new Vector3(48,20,-45);camera.transform.LookAt(root.transform.position+new Vector3(10,0,4));
            var light=new GameObject("Neighborhood daylight").AddComponent<Light>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject,scene);light.type=LightType.Directional;light.intensity=1.25f;light.transform.rotation=Quaternion.Euler(45,-35,0);
            var target=RenderTexture.GetTemporary(1280,720,24);var previous=RenderTexture.active;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/NeighborhoodDressing.png",texture.EncodeToPNG());
                var market=sites.Where(s=>s.name=="Market stall").OrderBy(s=>(s.position-root.transform.position).sqrMagnitude).First();
                camera.transform.position=market.TransformPoint(new Vector3(-9,5,-11));camera.transform.LookAt(market.position+Vector3.up);
                camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes("Logs/StreetBlockCloseup.png",texture.EncodeToPNG());
            }
            finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(texture);}
            File.WriteAllText("Logs/NeighborhoodDressingChecks.txt",$"PASS: {world.NeighborhoodSites} neighborhood scenes, {world.NeighborhoodVehicles} added vehicles, {sites.Select(s=>s.name).Distinct().Count()} scene types.\nPASS: lane clearance, bounded colliders, batching and player entry spawn.\n");
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}
