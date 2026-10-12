using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using GeoSniper;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One reproducible street, using the production world, player, weapon and HUD.
[InitializeOnLoad]
public static class StreetStudyCapture
{
    const string Key="GeoSniper.StreetStudy";
    static bool prepared,requested;
    static double ready;
    static DateTime started,requestedAt;
    static EditorWindow view;
    static UrbanCombatMission studyMission;
    static Camera studyCamera;
    static SectorWorld studyWorld;
    static Vector3 streetStart,streetTarget;
    static bool realStreet;
    static bool trainingStreet;
    static int phase;
    static int retries,retryPhase=-1;
    static StreetStudyCapture(){EditorApplication.update+=Tick;}
    public static void RunReal(){SessionState.SetBool(Key+".Real",true);Run();}
    public static void RunTraining(){SessionState.SetBool(Key+".Training",true);Run();}
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/.utmp/review-unity/"))throw new Exception("Isolated review project required");
        ValidateHeightFallback();
        ValidateTrainingFrontage();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        view.position=new Rect(0,0,1440,840);view.Show();
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    static void ValidateHeightFallback()
    {
        var elements=new JArray();
        for(int i=0;i<4;i++)
        {
            float size=i==1?40:10,x=i*55;
            var tags=new JObject{["building"]="yes",["name"]="Height check "+i};
            if(i==2)tags["height"]="18";if(i==3)tags["building:levels"]="5";
            Func<float,float,JObject> point=(a,b)=>new JObject{["lat"]=b/111320.0,["lon"]=a/111320.0};
            elements.Add(new JObject{["id"]=101+i,["type"]="way",["tags"]=tags,["geometry"]=new JArray(point(x,0),point(x+size,0),point(x+size,size),point(x,size),point(x,0))});
        }
        string json=new JObject{["elements"]=elements}.ToString();
        var features=SectorMap.Parse(json,new GameLocation{Latitude=0,Longitude=0});
        var shifted=SectorMap.Parse(json,new GameLocation{Latitude=.00002,Longitude=.00002});
        var small=features.Find(f=>f.Name=="Height check 0");var large=features.Find(f=>f.Name=="Height check 1");
        if(small==null || large==null || large.Height<=small.Height || large.Height>24 || small.Height<6)throw new Exception("OSM height estimates ignore footprint bounds");
        foreach(var feature in features)if(shifted.Find(f=>f.Name==feature.Name)?.Height!=feature.Height)throw new Exception("OSM height estimate changes with query centre");
        if(features.Find(f=>f.Name=="Height check 2")?.Height!=18 || features.Find(f=>f.Name=="Height check 3")?.Height!=15)throw new Exception("OSM source heights/floors lose priority");
    }
    static void ValidateTrainingFrontage()
    {
        var features=SectorMap.Training();
        var buildings=features.FindAll(f=>f.Kind=="building" && MapFeatureStyle.Text(f,"scenery_only")!="true");
        if(buildings.Count!=12)throw new Exception("Training playable-building count changed");
        var frontage=buildings.FindAll(f=>MapFeatureStyle.Text(f,"training_frontage")=="true");
        if(frontage.Count!=6)throw new Exception("Training frontage incomplete");
        foreach(var building in frontage)
        {
            if(!EnterableBuilding.Fit(building,out _,out _,out _))throw new Exception("Training frontage lacks full stairs");
            int entrance=BuildingFacadeDetails.EntranceEdge(building,features);
            var entry=(building.Points[entrance]+building.Points[(entrance+1)%building.Points.Count])*.5f;
            if(Mathf.Abs(entry.x-80)>8)throw new Exception("Training entrance faces a cross street instead of its reserved apron");
            foreach(var p in building.Points)
            {
                if(Mathf.Abs(p.x)<65 && Mathf.Abs(p.y)<65)throw new Exception("Training frontage occupies teaching arena");
                foreach(var road in features)if(road.Kind=="road")for(int i=1;i<road.Points.Count;i++)
                {
                    var a=road.Points[i-1];var delta=road.Points[i]-a;
                    var nearest=a+delta*Mathf.Clamp01(Vector2.Dot(p-a,delta)/delta.sqrMagnitude);
                    if((p-nearest).magnitude<MapFeatureStyle.RoadWidth(road)*.5f+1)throw new Exception("Training frontage encroaches road");
                }
            }
        }
        if(!SectorWorld.TryStyleRepresentativeStreet(features,Vector2.zero,out _,out _) || frontage.Exists(f=>MapFeatureStyle.Text(f,"street_study")!="true"))throw new Exception("Authored training street fails pilot selection");
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        if(started==default)started=DateTime.UtcNow;
        if((DateTime.UtcNow-started).TotalSeconds>240){SessionState.SetBool(Key,false);EditorApplication.Exit(1);return;}
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling)return;
        try
        {
            if(!prepared)
            {
                var game=UnityEngine.Object.FindAnyObjectByType<GeoSniperGame>();
                if(game!=null)game.gameObject.SetActive(false);
                foreach(var previous in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))previous.gameObject.SetActive(false);
                var features=new List<MapFeature>{new MapFeature{Kind="road",Name="Street study",Details=new JObject{["class"]="residential",["width"]="9"},Points=new List<Vector2>{new Vector2(0,-180),new Vector2(0,180)}}};
                string[] surfaces={"brick","stone","concrete","brick","metal","stone"};
                string[] colors={"#8E6954","#AEA18B","#727B7D","#65564F","#627179","#9A917E"};
                for(int side=0;side<2;side++)for(int i=0;i<6;i++)
                {
                    float x=side==0?-26:6,z=-45+i*27,depth=23+i%3;
                    features.Add(new MapFeature{Kind="building",Name=i%3==0?(side==0?"NIGHT MARKET":"CORNER CAFE"):"",Height=9+(i%4)*3,Details=new JObject{["street_study"]="true",["shop"]=i%3==0?"convenience":"",["facade_material"]=surfaces[(i+side)%6],["facade_color"]=colors[(i+side)%6]},Points=new List<Vector2>{new Vector2(x,z),new Vector2(x+20,z),new Vector2(x+20,z+depth),new Vector2(x,z+depth)}});
                }
                streetStart=new Vector3(0,.25f,-37);streetTarget=new Vector3(0,1.8f,100);
                bool training=SessionState.GetBool(Key+".Training",false);SessionState.SetBool(Key+".Training",false);
                trainingStreet=training;
                realStreet=training || SessionState.GetBool(Key+".Real",false);SessionState.SetBool(Key+".Real",false);
                if(realStreet)
                {
                    features=training?SectorMap.Training():MapServiceClient.Decode(File.ReadAllText("real-sector.json"));
                    if(!SectorWorld.TryStyleRepresentativeStreet(features,Vector2.zero,out var a,out var direction))throw new Exception("Cached representative road missing");
                    streetStart=new Vector3(a.x+direction.x*5,.25f,a.y+direction.y*5);streetTarget=streetStart+new Vector3(direction.x,.02f,direction.y)*65;
                    if(training){streetStart=new Vector3(a.x-direction.x*37,.25f,a.y-direction.y*37);streetTarget=streetStart+new Vector3(direction.x,.02f,direction.y)*65;}
                }
                var world=new GameObject("Representative street").AddComponent<SectorWorld>();world.Generate(features);studyWorld=world;
                foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    if(!renderer.transform.IsChildOf(world.transform) && renderer.GetComponentInParent<AtmosphericSky>()==null && !renderer.name.Contains("Sun Disc"))renderer.enabled=false;
                var camera=new GameObject("Gameplay camera").AddComponent<Camera>();camera.tag="MainCamera";camera.farClipPlane=1800;camera.allowHDR=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.02f,.03f,.05f);
                var mission=new GameObject("Street study gameplay").AddComponent<UrbanCombatMission>();
                studyMission=mission;studyCamera=camera;
                mission.Begin(camera,world.SpawnPoints,world.RooftopSpawns,true,null,false,streetStart,true);
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(UrbanCombatMission).GetField("envState",flags).SetValue(mission,1);
                typeof(UrbanCombatMission).GetMethod("ApplyEnvironment",flags).Invoke(mission,null);
                mission.Player.Place(streetStart);mission.Player.Face(streetTarget);
                // Keep the staged camera pose deterministic even if the desktop mouse moves.
                // Mission, weapon, sky, lighting and touch-HUD components continue updating.
                mission.Player.enabled=false;
                SectorStreetLighting.RefreshActiveLights(camera);
                Physics.SyncTransforms();
                foreach(var interior in world.GetComponentsInChildren<EnterableBuilding>())
                {
                    if(realStreet && !features.Exists(f=>MapFeatureStyle.Text(f,"street_study")=="true" && world.Buildings.TryGetValue(f,out var host) && interior.transform.IsChildOf(host.transform)))continue;
                    var entry=interior.transform.TransformPoint(interior.Entrance+Vector3.up*1.2f);
                    var outward=interior.transform.TransformDirection(interior.EntranceOutward);
                    if(Physics.Linecast(entry+outward*.55f,entry-outward*.55f))throw new Exception("Study door obstructed");
                    if(interior.OpenWindows.Count==0)throw new Exception("Study firing windows missing");
                    for(int step=-2;step<=2;step++)
                    {
                        var foot=entry-Vector3.up*1.14f+outward*(step*.2f);
                        if(Physics.CheckCapsule(foot+Vector3.up*.4f,foot+Vector3.up*1.4f,.32f,~0,QueryTriggerInteraction.Ignore))throw new Exception("Study entry blocks standing capsule");
                    }
                    foreach(var window in interior.OpenWindows)
                    {
                        var center=interior.transform.TransformPoint(window.Center+Vector3.up*1.7f);
                        var normal=interior.transform.TransformDirection(window.Outward);
                        if(Physics.Linecast(center-normal*.06f,center+normal*.45f,out var obstruction))throw new Exception("Study aperture blocks firing ray: "+obstruction.collider.name+" at "+center);
                    }
                }
                Directory.CreateDirectory("Logs");
                int styledCount=features.FindAll(f=>MapFeatureStyle.Text(f,"street_study")=="true").Count;
                if(styledCount>12)throw new Exception("Representative street exceeds twelve-building cap");
                File.WriteAllText("Logs/StreetStudy.txt","Production gameplay capture; pilot styling on "+styledCount+" street_study buildings; total enterable buildings="+world.GetComponentsInChildren<EnterableBuilding>().Length+". Android performance unmeasured.\n");
                if(trainingStreet)File.AppendAllText("Logs/StreetStudy.txt","PASS: twelve playable buildings, six authored frontage plots fit stairs, arena/road clearance and pilot selection.\n");
                long triangles=0;int renderers=0;
                foreach(var mesh in world.GetComponentsInChildren<MeshFilter>())if(mesh.sharedMesh!=null)triangles+=mesh.sharedMesh.triangles.Length/3;
                foreach(var renderer in world.GetComponentsInChildren<Renderer>())if(renderer.enabled)renderers++;
                File.AppendAllText("Logs/StreetStudy.txt","Desktop fixture geometry: "+triangles+" triangles, "+renderers+" enabled renderers. Lights retain global mobile budget of 4; draw calls/GPU timings require device profiling.\n");
                var byMesh=new Dictionary<string,long>();
                foreach(var filter in world.GetComponentsInChildren<MeshFilter>())if(filter.sharedMesh!=null)
                {
                    string name=filter.sharedMesh.name;
                    byMesh[name]=(byMesh.TryGetValue(name,out var total)?total:0)+filter.sharedMesh.triangles.Length/3;
                }
                var rows=new List<KeyValuePair<string,long>>(byMesh);rows.Sort((a,b)=>b.Value.CompareTo(a.Value));
                var lines=new List<string>();for(int i=0;i<Mathf.Min(20,rows.Count);i++)lines.Add(rows[i].Key+": "+rows[i].Value+" triangles (all instances, before frustum culling)");
                File.WriteAllLines("Logs/StreetStudyMeshBudget.txt",lines);
                ready=EditorApplication.timeSinceStartup+8;prepared=true;
            }
            if(view==null)view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            view.Repaint();EditorApplication.QueuePlayerLoopUpdate();
            if(EditorApplication.timeSinceStartup<ready)return;
            string[] shots={"Gameplay","Closeup","Rooftop","Rain","Daylight"};
            string path=Path.GetFullPath("Logs/StreetStudy"+(realStreet?"Real":"")+shots[Mathf.Min(phase,4)]+".png");
            if(retryPhase!=phase){retryPhase=phase;retries=0;}
            if(requested && phase<5 && (!File.Exists(path) || File.GetLastWriteTimeUtc(path)<requestedAt) && (DateTime.UtcNow-requestedAt).TotalSeconds>12)
            {
                if(++retries>3)throw new Exception("GameView screenshot failed after three retries: "+path);
                requested=false;ready=EditorApplication.timeSinceStartup+2;return;
            }
            if(!requested)
            {
                if(phase==0)
                {
                    var finishes=new List<string>();
                    foreach(var renderer in studyCamera.GetComponentsInChildren<Renderer>())foreach(var material in renderer.sharedMaterials)
                        if(material!=null)finishes.Add(renderer.name+" / "+material.name+" / "+material.shader.name+" / texture="+(material.mainTexture!=null?material.mainTexture.name:"none"));
                    File.WriteAllLines("Logs/StreetStudyWeaponMaterials.txt",finishes);
                }
                requestedAt=DateTime.UtcNow;ScreenCapture.CaptureScreenshot(path);requested=true;ready=EditorApplication.timeSinceStartup+3;return;
            }
            if(File.Exists(path) && File.GetLastWriteTimeUtc(path)>=requestedAt && phase==0)
            {
                studyMission.Player.Place(new Vector3(3,.25f,-38));studyMission.Player.Face(new Vector3(10,2.2f,-34.5f),true);
                if(realStreet)
                {
                    EnterableBuilding closest=null;float distance=float.MaxValue;
                    foreach(var building in studyWorld.GetComponentsInChildren<EnterableBuilding>())
                    {
                        var entry=building.transform.TransformPoint(building.Entrance);float d=(entry-streetStart).sqrMagnitude;
                        if(d<distance){distance=d;closest=building;}
                    }
                    var at=closest.transform.TransformPoint(closest.Entrance);var outward=closest.transform.TransformDirection(closest.EntranceOutward);
                    studyMission.Player.Place(at+outward*7+Vector3.up*.15f);studyMission.Player.Face(at+Vector3.up*2,true);
                }
                phase=1;requested=false;ready=EditorApplication.timeSinceStartup+4;return;
            }
            if(File.Exists(path) && File.GetLastWriteTimeUtc(path)>=requestedAt && phase==1)
            {
                if(studyWorld.RooftopSpawns.Count==0)throw new Exception("Study rooftop route missing");
                var roof=studyWorld.RooftopSpawns[0];
                if(realStreet)foreach(var candidate in studyWorld.RooftopSpawns)if((candidate-streetStart).sqrMagnitude<(roof-streetStart).sqrMagnitude)roof=candidate;
                studyMission.Player.Place(roof+Vector3.up*.1f);studyMission.Player.Face(new Vector3(19,6,38),true);
                if(realStreet)studyMission.Player.Face(streetTarget+Vector3.up*6,true);
                if(trainingStreet)studyMission.Player.Face(roof+new Vector3(220,8,320),true);
                if(!realStreet)
                {
                    Transform utility=null;float nearest=25*25;
                    foreach(var child in studyWorld.GetComponentsInChildren<Transform>())if(child.name=="Roof ventilation plinth")
                    {
                        float distance=(child.position-roof).sqrMagnitude;if(distance<nearest && distance>4){nearest=distance;utility=child;}
                    }
                    if(utility!=null){var heading=utility.position-roof;heading.y=0;studyMission.Player.Face(roof+heading.normalized*80+Vector3.up*2,true);}
                }
                SectorStreetLighting.RefreshActiveLights(studyCamera);
                phase=2;requested=false;ready=EditorApplication.timeSinceStartup+4;return;
            }
            if(File.Exists(path) && File.GetLastWriteTimeUtc(path)>=requestedAt && phase==2)
            {
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(UrbanCombatMission).GetField("envState",flags).SetValue(studyMission,3);
                typeof(UrbanCombatMission).GetMethod("ApplyEnvironment",flags).Invoke(studyMission,null);
                // Compare sustained rainy lighting, not a randomly timed lightning flash.
                if(WorldRainSystem.Instance!=null)typeof(WorldRainSystem).GetField("nextLightning",flags).SetValue(WorldRainSystem.Instance,Time.time+60);
                studyMission.Player.Place(streetStart);studyMission.Player.Face(streetTarget,true);
                phase=3;requested=false;ready=EditorApplication.timeSinceStartup+5;return;
            }
            if(File.Exists(path) && File.GetLastWriteTimeUtc(path)>=requestedAt && phase==3)
            {
                if(Shader.GetGlobalFloat("_StreetStudyWetness")<.99f)throw new Exception("Rain lacks wet surface state");
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                typeof(UrbanCombatMission).GetField("envState",flags).SetValue(studyMission,0);
                typeof(UrbanCombatMission).GetMethod("ApplyEnvironment",flags).Invoke(studyMission,null);
                phase=4;requested=false;ready=EditorApplication.timeSinceStartup+5;return;
            }
            if(File.Exists(path) && File.GetLastWriteTimeUtc(path)>=requestedAt && phase==4)
            {
                studyMission.Player.ToggleScope();studyCamera.fieldOfView=6;
                studyMission.currentMissionState=MissionState.Complete;
                ready=EditorApplication.timeSinceStartup+1;phase=5;return;
            }
            if(phase==5)
            {
                if(studyMission.Player.Scoped || Mathf.Abs(studyCamera.fieldOfView-65)>.01f)throw new Exception("Victory leaves scope/FOV active");
                if(MobilePostProcess.Instance!=null && MobilePostProcess.Instance.scopeBlend>.01f)throw new Exception("Victory leaves optical postprocess active");
                File.AppendAllText("Logs/StreetStudy.txt","PASS: clear entrance rays, firing-window records, victory scope and camera FOV reset in Play mode.\n");
                if(realStreet)
                {
                    studyMission.BeginStage(0);phase=6;ready=EditorApplication.timeSinceStartup+2;return;
                }
                SessionState.SetBool(Key,false);EditorApplication.Exit(0);
            }
            if(phase==6)
            {
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                if(!(bool)typeof(UrbanCombatMission).GetField("openingRoutesReady",flags).GetValue(studyMission))throw new Exception("Opening mission loses supported rooftop/window route");
                File.AppendAllText("Logs/StreetStudy.txt","PASS: opening campaign mission finds supported rooftop and open-window routes in the styled cached sector.\n");
                SessionState.SetBool(Key,false);EditorApplication.Exit(0);
            }
        }
        catch(Exception e){Directory.CreateDirectory("Logs");File.WriteAllText("Logs/StreetStudy.txt",e.ToString());SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
    }
}
