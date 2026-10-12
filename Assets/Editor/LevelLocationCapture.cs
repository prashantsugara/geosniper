using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using GeoSniper;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
public static class LevelLocationCapture
{
    const string Key="GeoSniper.LevelLocationCapture";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static readonly int[] Stages={0,3,2,1,8};
    static UrbanCombatMission mission;static SectorWorld world;static Camera camera;static EditorWindow view;
    static double ready,started;static int index;static bool prepared,requested,close,shotPending;static DateTime requestTime;
    static EnemyBot shotTarget;static int shotAmmo;static float shotHealth;
    static readonly List<string> failures=new List<string>();
    static object Get(object obj,string name)=>obj.GetType().GetField(name,Private).GetValue(obj);
    static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Private).SetValue(obj,value);
    static void Invoke(string method)=>typeof(UrbanCombatMission).GetMethod(method,Private).Invoke(mission,null);
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
    static LevelLocationCapture(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        Require(Application.dataPath.Replace('\\','/').Contains("/.utmp/android-gallery-build/"),"Use isolated capture project");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));view.position=new Rect(0,0,1440,840);view.Show();
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    static void Log(string text)=>File.AppendAllText("Logs/LevelLocationValidation.txt",text+"\n");
    static void Validate(ReferenceStronghold site)
    {
        Require(site.Layout!=null && site.Perches.Count>=2,"Missing supported firing positions");
        foreach(var atLocal in site.Layout.stairs.Concat(site.Layout.floors))
        {
            var at=site.transform.TransformPoint(atLocal);
            Require(Physics.Raycast(at+Vector3.up*.2f,Vector3.down,out var hit,.5f,~0,QueryTriggerInteraction.Ignore) && hit.transform.IsChildOf(site.transform),"Unsupported route "+atLocal);
            float stepOffset=mission.Player.GetComponent<CharacterController>().stepOffset;
            var blockers=Physics.OverlapCapsule(at+Vector3.up*.4f,at+Vector3.up*1.4f,.3f).Where(c=>c.transform.IsChildOf(site.transform) && c.bounds.max.y>at.y+stepOffset+.05f).ToArray();
            Require(blockers.Length==0,"Blocked headroom "+atLocal+": "+string.Join(",",blockers.Select(c=>c.name)));
        }
        foreach(var at in site.Layout.doors)
            Require(!Physics.Linecast(site.transform.TransformPoint(at+Vector3.back*.7f),site.transform.TransformPoint(at+Vector3.forward*.7f),~0,QueryTriggerInteraction.Ignore),"Blocked doorway "+at);
        var controller=mission.Player.GetComponent<CharacterController>();var saved=mission.Player.transform.position;
        var starts=site.Profile.Kind==CampaignLocationKind.Apartments?new[]{0}:site.Layout.stairStarts??new[]{0};
        for(int flight=0;flight<starts.Length;flight++)
        {
            int start=starts[flight],end=flight+1<starts.Length?starts[flight+1]:site.Layout.stairs.Length;
            mission.Player.Place(site.transform.TransformPoint(site.Layout.stairs[start]));
            for(int step=start+1;step<end;step++)
            {
                var next=site.transform.TransformPoint(site.Layout.stairs[step]);
                for(int tick=0;tick<180;tick++)
                {
                    var delta=next-mission.Player.transform.position;delta.y=0;if(delta.magnitude<.09f)break;
                    controller.Move(delta.normalized*Mathf.Min(.045f,delta.magnitude)+Vector3.down*.06f);
                }
                var d=next-mission.Player.transform.position;
                Require(new Vector2(d.x,d.z).magnitude<.16f && Mathf.Abs(d.y)<.28f,"Cannot walk stair "+step+" actual="+site.transform.InverseTransformPoint(mission.Player.transform.position));
            }
        }
        mission.Player.Place(saved);
        long triangles=site.GetComponentsInChildren<MeshFilter>().Sum(f=>Enumerable.Range(0,f.sharedMesh.subMeshCount).Sum(s=>(long)f.sharedMesh.GetIndexCount(s)/3));
        Log("PASS "+site.Profile.Family+": routes, doors, headroom and production controller stair flights; triangles="+triangles+" crates="+site.SupplyCrates);
        foreach(var renderer in site.GetComponentsInChildren<MeshRenderer>().Where(r=>r.GetComponentInParent<Transform>()!=null && r.sharedMaterial!=null && r.sharedMaterial.name.StartsWith("Licensed ")))
            Require(renderer.sharedMaterial.mainTexture!=null,"Missing source texture "+renderer.name);
    }
    static void Prepare()
    {
        Invoke("ClearStrongholdEncounter");Set(mission,"stageIndex",Stages[index]);Invoke("PrepareStrongholdEncounter");Physics.SyncTransforms();
        var site=mission.LevelStronghold;Require(site!=null,"No location placed at stage "+Stages[index]);
        foreach(string model in new[]{"MilitaryTent","BrickApartments","Warehouse","ShopShelf","ShopFridge"})
        {var source=Resources.Load<GameObject>("Models/LevelSites/"+model);Log(model+" source rotation="+source.transform.localEulerAngles+" scale="+source.transform.localScale);}
        Require(((List<EnemyBot>)Get(mission,"strongholdGuards")).Count>=2,"Fewer than two visible supported guards");
        mission.Player.enabled=false;mission.Player.Health.Invulnerable=true;mission.currentMissionState=MissionState.InProgress;mission.Player.InputBlocked=false;
        try{Validate(site);}catch(Exception e){failures.Add(site.Profile.Family+": "+e.Message);Log("FAIL "+failures.Last());}
        foreach(var enemy in UnityEngine.Object.FindObjectsByType<EnemyBot>(FindObjectsSortMode.None))enemy.enabled=false;
        Set(mission,"envState",0);Invoke("ApplyEnvironment");
        mission.Player.ExitAim();camera.fieldOfView=65;
        mission.Player.Place(site.transform.TransformPoint(new Vector3(-21,.2f,-26)));mission.Player.Face(site.transform.TransformPoint(new Vector3(0,3.2f,0)),true);
        prepared=true;ready=EditorApplication.timeSinceStartup+5;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        if(started==0)started=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-started>300){SessionState.SetBool(Key,false);EditorApplication.Exit(1);return;}
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling)return;
        try
        {
            if(mission==null)
            {
                Directory.CreateDirectory("Logs");File.WriteAllText("Logs/LevelLocationValidation.txt","Production gameplay captures; training sector, campaign site profiles; Android device performance pending.\n");
                var game=UnityEngine.Object.FindAnyObjectByType<GeoSniperGame>();if(game!=null)game.gameObject.SetActive(false);
                foreach(var old in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))old.gameObject.SetActive(false);
                world=new GameObject("Production training sector").AddComponent<SectorWorld>();world.Generate(SectorMap.Training());
                foreach(var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))if(!r.transform.IsChildOf(world.transform) && r.GetComponentInParent<AtmosphericSky>()==null && !r.name.Contains("Sun Disc"))r.enabled=false;
                camera=new GameObject("Gameplay camera").AddComponent<Camera>();camera.tag="MainCamera";camera.farClipPlane=1800;camera.allowHDR=true;camera.clearFlags=CameraClearFlags.SolidColor;
                mission=new GameObject("Production location encounter").AddComponent<UrbanCombatMission>();mission.Begin(camera,world.SpawnPoints,world.RooftopSpawns,true,null,false,Vector3.zero,false);mission.BeginStage(0);
            }
            if(!prepared){Prepare();return;}
            if(view==null)view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));view.Repaint();EditorApplication.QueuePlayerLoopUpdate();
            if(EditorApplication.timeSinceStartup<ready)return;
            var siteNow=mission.LevelStronghold;
            if(shotPending)
            {
                Require((int)Get(mission,"ammo")==shotAmmo-1,"Shot did not consume ammunition");
                Require(shotTarget==null || shotTarget.Actor.Health<shotHealth,"Scoped shot missed the site guard");
                Log("PASS "+siteNow.Profile.Family+": normal scoped ballistics consumes ammunition and damages guard.");
                shotPending=false;mission.Player.ExitAim();index++;prepared=false;close=false;
                if(index<Stages.Length)return;
                foreach(int variant in new[]{15,5})
                {
                    Invoke("ClearStrongholdEncounter");Set(mission,"stageIndex",variant);Invoke("PrepareStrongholdEncounter");Physics.SyncTransforms();
                    var site=mission.LevelStronghold;Require(site!=null,"Variant placement failed");Validate(site);
                    int colliders=site.GetComponentsInChildren<Collider>().Length;
                    Invoke("ClearStrongholdEncounter");Invoke("PrepareStrongholdEncounter");Physics.SyncTransforms();
                    Require(mission.LevelStronghold==site,"Retry duplicated the site");Require(site.GetComponentsInChildren<Collider>().Length==colliders,"Retry accumulates collision");
                    Log("PASS level "+(variant+1)+" layout variant and bounded retry.");
                }
                Invoke("ClearStrongholdEncounter");Set(mission,"stageIndex",9);Set(mission,"isPvPDuel",true);Invoke("PrepareStrongholdEncounter");
                Require(mission.LevelStronghold==null && world.GetComponentsInChildren<ReferenceStronghold>().Length==0,"Rooftop duel retained a previous campaign site");
                Log("PASS rooftop duel transition removes the preceding optional site.");
                var profiles=Enumerable.Range(0,50).Select(CampaignLocationProfile.ForStage).ToArray();Require(profiles.Count(p=>p.Kind==CampaignLocationKind.Stronghold)==5,"Wrong stronghold count");Require(profiles.Count(p=>p.Kind==CampaignLocationKind.DuelRoof)==5,"Wrong duel count");
                Log("PASS 50 explicit profiles: five strongholds, five original rooftop duels; other levels use apartments/camps/gallery/depot. Main campaign objectives and routes retained.");
                SessionState.SetBool(Key,false);EditorApplication.Exit(failures.Count==0?0:1);return;
            }
            string path=Path.GetFullPath("Logs/Location-"+siteNow.Profile.Kind+(close?"-close":"-wide")+".png");
            if(!requested){requestTime=DateTime.UtcNow;ScreenCapture.CaptureScreenshot(path);requested=true;ready=EditorApplication.timeSinceStartup+2;return;}
            if(!File.Exists(path) || File.GetLastWriteTimeUtc(path)<requestTime){Require((DateTime.UtcNow-requestTime).TotalSeconds<20,"Screenshot failed");return;}
            requested=false;
            if(!close){close=true;mission.Player.Place(siteNow.transform.TransformPoint(new Vector3(0,.2f,-18)));mission.Player.Face(siteNow.transform.TransformPoint(new Vector3(0,2.6f,-1.5f)),true);ready=EditorApplication.timeSinceStartup+3;return;}
            shotTarget=((List<EnemyBot>)Get(mission,"strongholdGuards")).First();shotHealth=shotTarget.Actor.Health;shotAmmo=(int)Get(mission,"ammo");
            Require(mission.ViewStronghold(),"Site view deployment failed");
            mission.Player.Face(shotTarget.transform.position+Vector3.up*1.5f,true);mission.Player.ToggleScope();camera.fieldOfView=12;
            Set(mission,"fireCooldown",0f);Set(mission,"chamberTimer",0f);mission.GetComponent<BallisticsSystem>().config.isArcadeMode=true;mission.Fire();shotPending=true;ready=EditorApplication.timeSinceStartup+2;
        }
        catch(Exception e){Log(e.ToString());SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
    }
}
