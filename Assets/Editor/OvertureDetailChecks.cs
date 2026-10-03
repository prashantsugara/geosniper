using System;
using System.Collections.Generic;
using System.IO;
using GeoSniper;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class OvertureDetailChecks
{
    [MenuItem("Geo Sniper/Validate Overture Details")]
    public static void Run()
    {
        var road=new MapFeature{Kind="road",Name="Footpath",Points=new List<Vector2>{Vector2.zero,new Vector2(10,0)},Details=new JObject{["class"]="footway",["surface"]="gravel"}};
        Check(MapFeatureStyle.RoadWidth(road)==2 && MapFeatureStyle.Path(road),"Footpath width");
        road.Details["class"]="primary";
        Check(MapFeatureStyle.RoadWidth(road)==12 && !MapFeatureStyle.Path(road),"Primary road width");
        road.Details["width"]="8.5";
        Check(MapFeatureStyle.RoadWidth(road)==8.5f,"Mapped width");
        road.Details["width"]="NaN";
        Check(MapFeatureStyle.RoadWidth(road)==12,"Invalid width fallback");
        var gym=new MapFeature{Kind="place",Details=new JObject{["category"]="fitness_center"}};
        Check(MapFeatureStyle.Category(gym)=="GYM","Place category");
        var building=new MapFeature{Details=new JObject{["facade_color"]="#ff0000",["num_floors"]="3",["roof_shape"]="gabled"}};
        Check(MapFeatureStyle.BuildingColor(building,Color.white)==Color.red,"Mapped facade colour");
        Check(MapFeatureStyle.ReadDetails(new JObject{["width"]=new JObject(),["address"]="ignored"}).Count==0,"Invalid/private details excluded");
        var partDetails=MapFeatureStyle.ReadDetails(new JObject{["id"]="part-1",["building_id"]="parent-1",["is_part"]="true",["min_height"]="12"});
        Check((string)partDetails["building_id"]=="parent-1" && WorldDetailPlanner.IsBuildingPart(new MapFeature{Details=partDetails}),
            "Overture building-part relationship survives detail filtering");
        string path=Path.Combine("Temp","OvertureDetailChecks-"+Guid.NewGuid().ToString("N"));
        try
        {
            var cache=new SectorMapCache(path); var location=new GameLocation{Latitude=0,Longitude=0};
            cache.Save(new List<MapFeature>{road},location);
            var restored=cache.Read(location,TimeSpan.FromDays(1));
            Check(restored!=null && MapFeatureStyle.Text(restored[0],"class")=="primary" && MapFeatureStyle.Text(restored[0],"surface")=="gravel","Details survive disk cache");
        }
        finally
        {
            if(Directory.Exists(path)) { foreach(var file in Directory.GetFiles(path)) File.Delete(file); Directory.Delete(path); }
        }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/OvertureDetailChecks.txt","PASS: 8 Overture detail checks\n");
        Debug.Log("PASS: 8 Overture detail checks");
    }
    static void Check(bool value,string message) { if(!value) throw new Exception(message); }
}
