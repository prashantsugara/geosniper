using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GeoSniper;
using UnityEditor;
using UnityEngine;

public static class MapDetailChecks
{
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in the isolated review project.");
        Directory.CreateDirectory("Logs");
        var level = new MapFeature { Kind = "road", Name = "Main Street", Points = new List<Vector2> {
            new Vector2(-40, 0), new Vector2(0, 0), new Vector2(40, 0) } };
        var cross = new MapFeature { Kind = "road", Name = "Cross Street", Points = new List<Vector2> {
            new Vector2(0, 0), new Vector2(0, 32) } };
        var far = new MapFeature { Kind = "road", Name = "Other Street", Points = new List<Vector2> {
            new Vector2(60, 60), new Vector2(60, 92) } };
        var withoutCrossing = StreetSurfaceDetails.Build(new List<MapFeature> { level, far }, (x, z) => x * .01f, true);
        var withCrossing = StreetSurfaceDetails.Build(new List<MapFeature> { level, cross }, (x, z) => x * .01f, true);
        Require(withoutCrossing != null && withCrossing != null, "Road detail meshes missing");
        Require(withCrossing.subMeshCount == 2 && withCrossing.GetTriangles(0).Length > 0,
            "Yellow center markings missing");
        Require(withCrossing.GetTriangles(1).Length > withoutCrossing.GetTriangles(1).Length,
            "Junction did not add white crossing stripes");
        foreach (var vertex in withCrossing.vertices)
            Require(float.IsFinite(vertex.x) && float.IsFinite(vertex.y) && float.IsFinite(vertex.z) &&
                    Mathf.Abs(vertex.y - (vertex.x * .01f + .20f)) < .001f,
                "Road marking does not follow the sampled terrain");
        var straightTurn=new MapFeature { Kind="road",Points=new List<Vector2>{
            new Vector2(0,0),new Vector2(30,0),new Vector2(60,0)}};
        var rightBend=new MapFeature { Kind="road",Points=new List<Vector2>{
            new Vector2(0,0),new Vector2(30,0),new Vector2(30,30)}};
        var straightPaint=StreetSurfaceDetails.Build(new List<MapFeature>{straightTurn},(x,z)=>0,false);
        var bendPaint=StreetSurfaceDetails.Build(new List<MapFeature>{rightBend},(x,z)=>0,false);
        Require(straightPaint!=null && bendPaint!=null &&
            bendPaint.GetTriangles(1).Length==straightPaint.GetTriangles(1).Length,
            "A bend in one mapped road produced a false pedestrian crossing");
        UnityEngine.Object.DestroyImmediate(straightPaint);
        UnityEngine.Object.DestroyImmediate(bendPaint);

        var narrowSide=new MapFeature { Kind="road",Points=new List<Vector2>{
            new Vector2(-4,0),new Vector2(-4,10)},
            Details=new Newtonsoft.Json.Linq.JObject{["width"]="1"}};
        var narrowMask=new RoadJunctionMask(new List<MapFeature>{straightTurn,narrowSide});
        Require(!narrowMask.Occupied(new Vector2(0,3),straightTurn,Vector2.right) &&
            narrowMask.OccupiedAlong(new Vector2(-4,3),new Vector2(4,3),straightTurn,Vector2.right),
            "Narrow side street was missed at the end of a curb or paint span");

        var shallowBranch = new MapFeature { Kind = "road", Points = new List<Vector2> {
            new Vector2(0, 0), new Vector2(30, 4) } };
        var shallowMask = new RoadJunctionMask(new List<MapFeature> { level, shallowBranch });
        Require(shallowMask.Occupied(new Vector2(10, 1), shallowBranch, new Vector2(30, 4)),
            "Shallow-angle branch paint overlaps the through road");
        Require(!shallowMask.Occupied(new Vector2(10, 18), shallowBranch, new Vector2(30, 4)),
            "Distant road was mistaken for a junction");

        string vegetationQuery=SectorMap.BuildQuery(new GameLocation { Latitude=29.38,Longitude=79.46 });
        Require(vegetationQuery.Contains("way[landuse~") && vegetationQuery.Contains("way[natural~"),
            "Map request omits woodland or scrub areas");
        var woodsJson=new Newtonsoft.Json.Linq.JObject { ["elements"]=new Newtonsoft.Json.Linq.JArray {
            new Newtonsoft.Json.Linq.JObject {
                ["tags"]=new Newtonsoft.Json.Linq.JObject { ["landuse"]="forest" },
                ["geometry"]=new Newtonsoft.Json.Linq.JArray {
                    new Newtonsoft.Json.Linq.JObject { ["lat"]=-100d/111320,["lon"]=-500d/111320 },
                    new Newtonsoft.Json.Linq.JObject { ["lat"]=-100d/111320,["lon"]=100d/111320 },
                    new Newtonsoft.Json.Linq.JObject { ["lat"]=100d/111320,["lon"]=100d/111320 },
                    new Newtonsoft.Json.Linq.JObject { ["lat"]=100d/111320,["lon"]=-500d/111320 },
                    new Newtonsoft.Json.Linq.JObject { ["lat"]=-100d/111320,["lon"]=-500d/111320 } } } } };
        var mappedWoods=SectorMap.Parse(woodsJson.ToString(),new GameLocation { Latitude=0,Longitude=0 });
        Require(mappedWoods.Count==1 && mappedWoods[0].Kind=="park" &&
                mappedWoods[0].Points.TrueForAll(p=>Mathf.Abs(p.x)<=320.01f && Mathf.Abs(p.y)<=320.01f),
            "Mapped woodland crossing the sector boundary was dropped or left unclipped");
        Require((string)MapFeatureStyle.ReadDetails(new Newtonsoft.Json.Linq.JObject { ["landuse"]="forest",["natural"]="wood" })["landuse"]=="forest",
            "Cached landscape classification was discarded");
        var forest=new MapFeature { Kind="park",Points=new List<Vector2> {
            new Vector2(10,10),new Vector2(30,10),new Vector2(30,30),new Vector2(10,30) },
            Details=new Newtonsoft.Json.Linq.JObject { ["landuse"]="forest" } };
        Require(SectorScenery.CanPlant(new Vector2(20,20),new List<MapFeature>{forest},out _,out var forestKind) &&
            forestKind==SectorScenery.PlantingKind.Woodland,"Mapped forest did not select woodland planting");
        forest.Details=new Newtonsoft.Json.Linq.JObject { ["natural"]="scrub" };
        Require(SectorScenery.CanPlant(new Vector2(20,20),new List<MapFeature>{forest},out _,out var scrubKind) &&
            scrubKind==SectorScenery.PlantingKind.Scrub,"Mapped scrub did not select scrub planting");
        forest.Details=new Newtonsoft.Json.Linq.JObject { ["subtype"]="pitch" };
        Require(SectorScenery.CanPlant(new Vector2(20,20),new List<MapFeature>{forest},out _,out var pitchKind) &&
            pitchKind==SectorScenery.PlantingKind.Sports,"Sports ground was not excluded from planting");

        SectorWorld.ResetWaterLevels();
        float lakeA=SectorWorld.ResolveWaterLevel("","Naini Lake",new Rect(0,0,320,300),12f);
        float lakeB=SectorWorld.ResolveWaterLevel("","Naini Lake",new Rect(320,0,320,300),19f);
        float lakeC=SectorWorld.ResolveWaterLevel("","Naini Lake",new Rect(640,0,320,300),27f);
        float separateLake=SectorWorld.ResolveWaterLevel("","Naini Lake",new Rect(2000,0,320,300),45f);
        Require(lakeA==12f && lakeB==lakeA && lakeC==lakeA && separateLake==45f,
            "Unnamed-ID water level did not propagate across touching sector bounds");
        SectorWorld.ResetWaterLevels();
        CheckHillyLakePlacement();

        var trafficA=new MapFeature { Kind="road",Details=new Newtonsoft.Json.Linq.JObject { ["class"]="residential" } };
        var trafficB=new MapFeature { Kind="road",Details=new Newtonsoft.Json.Linq.JObject { ["class"]="residential" } };
        var aRoad=new TrafficRoadRoutes.Road { Feature=trafficA,Points=new List<Vector3> { Vector3.zero,new Vector3(20,0,0) } };
        var bRoad=new TrafficRoadRoutes.Road { Feature=trafficB,Points=new List<Vector3> { new Vector3(20,0,0),new Vector3(40,0,0) } };
        var planned=new TrafficRoadRoutes(new List<TrafficRoadRoutes.Road>{aRoad}).Build(aRoad,0,1);
        Require(new TrafficRoadRoutes(new List<TrafficRoadRoutes.Road>{aRoad,bRoad}).TryAppend(planned) &&
                planned[planned.Count-1].x>39f,"Vehicle route did not continue onto a newly loaded road");
        trafficB.Details["oneway"]="-1";
        var restricted=new TrafficRoadRoutes(new List<TrafficRoadRoutes.Road>{aRoad}).Build(aRoad,0,1);
        Require(!new TrafficRoadRoutes(new List<TrafficRoadRoutes.Road>{aRoad,bRoad}).TryAppend(restricted),
            "Vehicle route continued against a one-way restriction");
        trafficB.Details["oneway"]="no";
        var roadWorld=new GameObject("Traffic test sector").AddComponent<SectorWorld>();
        var roadFloor=GameObject.CreatePrimitive(PrimitiveType.Cube);roadFloor.name="Local terrain";
        roadFloor.transform.position=new Vector3(20,-.5f,0);roadFloor.transform.localScale=new Vector3(100,1,30);
        var car=new GameObject("Traffic test car").AddComponent<TrafficVehicle>();
        try
        {
            roadWorld.RoadPaths.Add(aRoad.Points);roadWorld.RoadPaths.Add(bRoad.Points);
            roadWorld.RoadPathFeatures[aRoad.Points]=trafficA;roadWorld.RoadPathFeatures[bRoad.Points]=trafficB;
            SectorWorld.LoadedWorlds.Add(roadWorld);Physics.SyncTransforms();
            var shortRoute=new TrafficRoadRoutes(new List<TrafficRoadRoutes.Road>{aRoad}).Build(aRoad,0,1);
            car.Initialize(shortRoute,0,1,0);
            typeof(TrafficVehicle).GetMethod("Simulate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(car,new object[]{.02f});
            var liveRoute=(List<Vector3>)typeof(TrafficVehicle).GetField("path",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(car);
            Require(liveRoute.Count>2 && liveRoute[liveRoute.Count-1].x>39f,
                "Driving car did not refresh its route from the loaded sector");
        }
        finally {SectorWorld.LoadedWorlds.Remove(roadWorld);UnityEngine.Object.DestroyImmediate(car.gameObject);
                 UnityEngine.Object.DestroyImmediate(roadFloor);UnityEngine.Object.DestroyImmediate(roadWorld.gameObject);}

        var labelPoint = typeof(UrbanCombatMission).GetMethod("MapLabelPoint", BindingFlags.Static | BindingFlags.NonPublic);
        Require(labelPoint != null, "Road label placement missing");
        var bent = new MapFeature { Kind = "road", Name = "Bent Road", Points = new List<Vector2> {
            new Vector2(0, 0), new Vector2(30, 0), new Vector2(30, 10) } };
        var point = (Vector2)labelPoint.Invoke(null, new object[] { bent });
        Require(Vector2.Distance(point, new Vector2(20, 0)) < .001f,
            "Road label must follow path length, not the polygon centroid");
        CapturePreview(new List<MapFeature> { level, cross });
        File.WriteAllText("Logs/MapDetailChecks.txt", "PASS: lane lines, terrain following, bend/crossing separation, curb-span junction clearance, mapped planting, hilly lake beds, dry landing footprints, streamed traffic routes and road labels\n");
        UnityEngine.Object.DestroyImmediate(withoutCrossing);
        UnityEngine.Object.DestroyImmediate(withCrossing);
        Debug.Log("Map detail checks passed");
    }

    static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }

    static void CheckHillyLakePlacement()
    {
        var world=new GameObject("Hilly lake validation").AddComponent<SectorWorld>();
        try
        {
            world.BuildNavigation=false;
            world.Elevation.Available=true;
            for(int z=0;z<SectorElevation.Size;z++)for(int x=0;x<SectorElevation.Size;x++)
            {
                float east=(x-32)*SectorElevation.Step,north=(z-32)*SectorElevation.Step;
                world.Elevation.Heights[z*SectorElevation.Size+x]=Mathf.Abs(east)*.025f+Mathf.Abs(north)*.018f;
            }
            var lake=new MapFeature {Kind="water",Name="Hilly test lake",Points=new List<Vector2>{
                new Vector2(-45,-45),new Vector2(45,-45),new Vector2(45,45),new Vector2(-45,45)}};
            world.Generate(new List<MapFeature>{lake});
            Physics.SyncTransforms();
            var water=Array.Find(world.GetComponentsInChildren<MeshFilter>(),f=>f.name=="water");
            Require(water!=null && water.sharedMesh!=null,"Hilly test lake mesh missing");
            float surface=water.sharedMesh.vertices[0].y;
            Require(world.Ground(0,0)<surface-.1f,"Lake bed rose above the water surface");
            Require(world.Ground(50,0)>surface,"Dry bank fell below the water surface");
            Require(!SectorWorld.DryFootprint(new Vector3(47,0,0),3.5f),"Shoreline landing disc crossed the water");
            Require(world.TryFindGeographicSpawn(Vector3.zero,out var spawn) && SectorWorld.DryFootprint(spawn,.5f),
                "Water-center player spawn did not move to supported dry terrain");
            var footprint=typeof(UrbanCombatMission).GetMethod("ValidExtractionFootprint",BindingFlags.Static|BindingFlags.NonPublic);
            Require(footprint!=null && !(bool)footprint.Invoke(null,new object[]{new Vector3(47,world.Ground(47,0),0),3.5f}),
                "Extraction accepted the water edge");
            Require((bool)footprint.Invoke(null,new object[]{new Vector3(80,world.Ground(80,0),0),3.5f}),
                "Extraction rejected a broad supported dry bank");
        }
        finally {UnityEngine.Object.DestroyImmediate(world.gameObject);SectorWorld.ResetWaterLevels();}
    }

    static void CapturePreview(List<MapFeature> streets)
    {
        var asphalt = new Material(Shader.Find("Standard")) { color = new Color(.10f, .12f, .14f) };
        var pale = new Material(Shader.Find("Standard")) { color = new Color(.91f, .92f, .88f) };
        var yellow = new Material(Shader.Find("Standard")) { color = new Color(.95f, .79f, .23f) };
        var grass = new Material(Shader.Find("Standard")) { color = new Color(.29f, .38f, .25f) };
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = new Vector3(0, -.15f, 0);
        floor.transform.localScale = new Vector3(95, .2f, 80);
        floor.GetComponent<Renderer>().sharedMaterial = grass;
        var horizontal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        horizontal.transform.position = new Vector3(0, .08f, 0);
        horizontal.transform.localScale = new Vector3(80, .16f, 6);
        horizontal.GetComponent<Renderer>().sharedMaterial = asphalt;
        var vertical = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vertical.transform.position = new Vector3(0, .08f, 16);
        vertical.transform.localScale = new Vector3(6, .16f, 32);
        vertical.GetComponent<Renderer>().sharedMaterial = asphalt;
        var roadMarks = new GameObject("Road details");
        var previewMesh = StreetSurfaceDetails.Build(streets, (x, z) => 0, false);
        roadMarks.AddComponent<MeshFilter>().sharedMesh = previewMesh;
        roadMarks.AddComponent<MeshRenderer>().sharedMaterials = new[] { yellow, pale };
        var light = new GameObject("Sun").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);
        var camera = new GameObject("Preview camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(26, 55, -32);
        camera.transform.LookAt(new Vector3(0, 0, 8));
        camera.orthographic = true;
        camera.orthographicSize = 31;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.12f, .16f, .18f);
        var target = new RenderTexture(960, 640, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(960, 640, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 960, 640), 0, 0);
        image.Apply();
        File.WriteAllBytes("Logs/MapDetailPreview.png", image.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        foreach (var obj in new UnityEngine.Object[] { image, target, previewMesh, asphalt, pale, yellow, grass,
            floor, horizontal, vertical, roadMarks, light.gameObject, camera.gameObject })
            UnityEngine.Object.DestroyImmediate(obj);
    }
}
