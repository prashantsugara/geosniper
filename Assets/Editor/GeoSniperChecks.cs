using System;
using System.Collections.Generic;
using System.IO;
using GeoSniper;
using UnityEditor;
using UnityEngine;

public static class GeoSniperChecks
{
    [MenuItem("Geo Sniper/Run Map Checks")]
    public static void Run()
    {
        int checks=0;
        Action<bool,string> check=(ok,label)=> { if(!ok) throw new Exception(label); checks++; };
        check(CountryCapitalDatabase.Find("IN").Label.Contains("NEW DELHI"),"India capital");
        check(CountryCapitalDatabase.Find("JP").Label.Contains("TOKYO"),"Japan capital");
        check(CountryCapitalDatabase.Find("invalid")==null,"Unknown country");
        check(LocationResolver.Valid(GameLocation.Default.Latitude,GameLocation.Default.Longitude),"Default coordinates");
        check(!LocationResolver.Valid(double.NaN,0),"Invalid GPS");
        check(SectorMap.Offline().Count>10,"Offline map");
        var polygon=new List<Vector2>{new Vector2(0,0),new Vector2(4,0),new Vector2(4,4),new Vector2(2,2),new Vector2(0,4)};
        check(SectorWorld.Triangulate(polygon).Count==9,"Concave footprint");
        polygon.Reverse();
        check(SectorWorld.Triangulate(polygon).Count==9,"Reverse footprint");
        check(SectorMap.Parse("{\"elements\":[]}",GameLocation.Default).Count==0,"Empty OSM");
        var start=new Vector2(-500,0); var end=new Vector2(500,0);
        check(SectorMap.ClipRoad(ref start,ref end) && Mathf.Approximately(start.x,-320) && Mathf.Approximately(end.x,320),"Crossing road clipped");
        start=new Vector2(-500,400); end=new Vector2(500,400);
        check(!SectorMap.ClipRoad(ref start,ref end),"Outside road rejected");
        var elements=new Newtonsoft.Json.Linq.JArray();
        Func<float,float,Newtonsoft.Json.Linq.JObject> point=(x,z)=>new Newtonsoft.Json.Linq.JObject { ["lat"]=z/111320.0,["lon"]=x/111320.0 };
        // Roads arriving first must never exhaust the separate building budget.
        for(int i=0;i<130;i++) elements.Add(new Newtonsoft.Json.Linq.JObject {
            ["tags"]=new Newtonsoft.Json.Linq.JObject { ["highway"]="residential",["name"]="Test Road" },
            ["geometry"]=new Newtonsoft.Json.Linq.JArray(point(-50,i),point(0,i),point(50,i)) });
        elements.Add(new Newtonsoft.Json.Linq.JObject {
            ["tags"]=new Newtonsoft.Json.Linq.JObject { ["building"]="yes" },
            ["geometry"]=new Newtonsoft.Json.Linq.JArray(point(0,0),point(10,0),point(10,10),point(0,10),point(0,0)) });
        var parsed=SectorMap.Parse(new Newtonsoft.Json.Linq.JObject { ["elements"]=elements }.ToString(),new GameLocation { Latitude=0,Longitude=0 });
        check(parsed.FindAll(f=>f.Kind=="building").Count==1,"Buildings survive dense road data");
        check(parsed.FindAll(f=>f.Kind=="road").Count==120,"Road budget is bounded");
        check(parsed[0].Points.Count==3 && parsed[0].Name=="Test Road","Road names and continuous geometry preserved");
        check(MapServiceClient.Decode(new Newtonsoft.Json.Linq.JObject {
            ["version"]=1,["features"]=new Newtonsoft.Json.Linq.JArray(new Newtonsoft.Json.Linq.JObject {
                ["kind"]="road",["name"]="Test Road",["points"]=new Newtonsoft.Json.Linq.JArray(new Newtonsoft.Json.Linq.JArray(0,0),new Newtonsoft.Json.Linq.JArray(10,0)) }) }.ToString())[0].Name=="Test Road","Road names survive cache decoding");
        check(SectorMap.CoverageLabel(parsed,"LIVE OSM").Contains("Sparse"),"Sparse buildings are disclosed");
        var sceneryFeatures=new List<MapFeature> {
            new MapFeature { Kind="road",Points=new List<Vector2>{new Vector2(-20,0),new Vector2(20,0)} },
            new MapFeature { Kind="building",Points=new List<Vector2>{new Vector2(30,30),new Vector2(40,30),new Vector2(40,40),new Vector2(30,40)} }
        };
        check(!SectorScenery.CanPlant(Vector2.zero,sceneryFeatures,out _),"Grass excludes roads");
        check(!SectorScenery.CanPlant(new Vector2(35,35),sceneryFeatures,out _),"Grass excludes buildings");
        sceneryFeatures[1].Kind="water";
        check(!SectorScenery.CanPlant(new Vector2(35,35),sceneryFeatures,out _),"Grass excludes water");
        sceneryFeatures[1].Kind="park";
        check(SectorScenery.CanPlant(new Vector2(35,35),sceneryFeatures,out float parkHeight) && parkHeight>.15f,"Park grass sits above park mesh");
        check(SectorScenery.CanPlant(new Vector2(-80,-80),sceneryFeatures,out _),"Open grass allowed");
        check(Math.Abs(SectorElevation.Decode(new Color32(137,219,68,255))-2523.265625f)<.001f,"Terrarium height decoding");
        check(SectorElevation.Decode(new Color32(127,255,0,255))==-1,"Below sea level decoding");
        var terrain=new SectorElevation();
        for(int z=0;z<SectorElevation.Size;z++) for(int x=0;x<SectorElevation.Size;x++)
            terrain.Heights[z*SectorElevation.Size+x]=(x*50-1600)*.1f+(z*50-1600)*.2f;
        check(Math.Abs(terrain.Sample(25,10)-4.5f)<.001f,"Terrain triangle interpolation");
        check(Math.Abs(terrain.Sample(-20,40)-6f)<.001f,"Terrain opposite triangle interpolation");
        check(Math.Abs(terrain.Sample(2000,2000)-480f)<.001f,"Terrain edge clamp");
        check(SectorElevation.Pixel(1,0).y<SectorElevation.Pixel(0,0).y,"North is top of elevation tiles");
        File.WriteAllText("Logs/GeoSniperChecks.txt",checks+" map checks passed");
        Debug.Log("Geo Sniper map checks passed: "+checks);
    }
}
