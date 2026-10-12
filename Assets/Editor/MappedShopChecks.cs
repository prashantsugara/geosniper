using System;
using System.IO;
using System.Linq;
using GeoSniper;
using Newtonsoft.Json.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class MappedShopChecks
{
    static JObject Node(string shop,string name,float x,float z)=>new JObject{["type"]="node",["lat"]=z/111320.0,["lon"]=x/111320.0,["tags"]=new JObject{["shop"]=shop,["name"]=name}};
    static JObject Way(string shop,string name,float x,float z)
    {
        var points=new JArray();foreach(var p in new[]{new Vector2(x,z),new Vector2(x+12,z),new Vector2(x+12,z+12),new Vector2(x,z+12),new Vector2(x,z)})points.Add(new JObject{["lat"]=p.y/111320.0,["lon"]=p.x/111320.0});
        var tags=new JObject{["building"]="yes",["height"]="8",["name"]=name};if(shop!=null)tags["shop"]=shop;
        return new JObject{["type"]="way",["tags"]=tags,["geometry"]=points};
    }
    public static void Run()
    {
        var source=new JObject{["elements"]=new JArray{Way("hardware","Mapped Hardware",-6,12),Way(null,"Retail Block",22,12),Node("pharmacy","Mapped Pharmacy",26,17),Node("convenience","",-26,16),Node("vacant","Empty Unit",60,15),Node("no","Non-shop",80,15)}};
        var features=SectorMap.Parse(source.ToString(),new GameLocation{Latitude=0,Longitude=0});
        Check(features.Count(SectorSignage.IsShop)==3,"Shop tags, unnamed shops or closed-shop filtering failed");
        Check(features.Any(f=>f.Name=="Mapped Hardware" && MapFeatureStyle.Text(f,"shop")=="hardware"),"Shop subtype lost during OSM parsing");
        features.Add(new MapFeature{Kind="road",Name="Shop Street",Details=new JObject{["class"]="residential"},Points={new Vector2(-90,0),new Vector2(100,0)}});
        var rows=new JArray();foreach(var feature in features)
        {var points=new JArray();foreach(var p in feature.Points)points.Add(new JArray(p.x,p.y));rows.Add(new JObject{["kind"]=feature.Kind,["name"]=feature.Name,["landmark"]=feature.Landmark,["height"]=feature.Height,["points"]=points,["details"]=feature.Details});}
        features=MapServiceClient.Decode(new JObject{["version"]=2,["features"]=rows}.ToString());
        Check(features.Count(SectorSignage.IsShop)==3 && features.Any(f=>MapFeatureStyle.Text(f,"shop")=="pharmacy"),"Shop tags lost in cache decoding");
        var scene=EditorSceneManager.NewPreviewScene();var root=new GameObject("Mapped shops fixture");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
        root.transform.position=new Vector3(10000,0,10000);var world=root.AddComponent<SectorWorld>();world.BuildNavigation=false;world.DetailFocus=root.transform.position;
        try
        {
            world.Generate(features);
            Check(world.MappedShopFronts==2,"Shop building and hosted pharmacy must get storefronts");
            Check(world.MappedShopKiosks==1,"Unhosted unnamed convenience shop must get an exact-location kiosk");
            Check(root.GetComponentsInChildren<WorldSign>().Any(s=>s.Caption.Contains("Mapped Pharmacy") && s.Caption.Contains("PHARMACY")),"Mapped shop name/category signage missing");
            var kiosk=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Mapped shop kiosk");
            Check(Vector2.Distance(new Vector2(kiosk.localPosition.x,kiosk.localPosition.z),new Vector2(-26,16))<.05f,"Kiosk moved away from mapped coordinates");
            var blocked=new MapFeature{Kind="place",Name="Road shop",Landmark="shop",Details=new JObject{["shop"]="bakery"},Points={new Vector2(45,-1),new Vector2(47,-1),new Vector2(47,1),new Vector2(45,1)}};
            int count=world.MappedShopKiosks;world.BuildMappedShopKiosk(blocked,world.SignMaterial(Color.blue),world.SignMaterial(Color.gray));Check(world.MappedShopKiosks==count,"Kiosk blocks mapped road");
            var camera=new GameObject("Shop camera").AddComponent<Camera>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject,scene);camera.scene=scene;camera.backgroundColor=new Color(.65f,.75f,.85f);camera.clearFlags=CameraClearFlags.SolidColor;
            camera.transform.position=root.transform.position+new Vector3(-22,9,-25);camera.transform.LookAt(root.transform.position+new Vector3(0,2,18));
            var light=new GameObject("Shop daylight").AddComponent<Light>();UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject,scene);light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(45,-25,0);
            var target=RenderTexture.GetTemporary(1280,720,24);var previous=RenderTexture.active;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/MappedShops.png",texture.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(texture);}
            File.WriteAllText("Logs/MappedShopChecks.txt","PASS: OSM shop subtypes, unnamed shops, inactive-shop filtering, cache persistence.\nPASS: shop-way storefront, hosted shop-node storefront, exact-location kiosk, road rejection and mapped names/categories.\n");
        }
        finally{EditorSceneManager.ClosePreviewScene(scene);}
    }
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
}
