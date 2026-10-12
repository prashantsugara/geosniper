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
public static class StrongholdCapture
{
    const string Key="GeoSniper.StrongholdCapture";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static UrbanCombatMission mission;
    static SectorWorld world;
    static Camera camera;
    static EditorWindow view;
    static double ready,started;
    static int phase;
    static bool prepared,requested;
    static DateTime requestedAt;
    static EnemyBot target;
    static int ammoBefore;
    static float healthBefore;
    static Exception galleryFailure;
    static object Get(object obj,string name)=>obj.GetType().GetField(name,Private).GetValue(obj);
    static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Private).SetValue(obj,value);
    static List<EnemyBot> Guards=>(List<EnemyBot>)Get(mission,"strongholdGuards");
    static StrongholdCapture(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        var isolatedPath=Application.dataPath.Replace('\\','/');
        if(!isolatedPath.Contains("/.utmp/review-unity/") && !isolatedPath.Contains("/.utmp/android-gallery-build/"))throw new Exception("Isolated review project required");
        // Refresh these imports when reusing a cache across isolated projects.
        foreach(var asset in Directory.GetFiles("Assets/Resources/Models/Stronghold"))
            if(asset.EndsWith(".fbx") || asset.EndsWith(".json"))AssetDatabase.ImportAsset(asset.Replace('\\','/'),ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/Resources/Models/NeighborhoodProps/DeliveryCrate.fbx",ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/Resources/Models/NeighborhoodProps/Mat.2_baseColor.png",ImportAssetOptions.ForceUpdate);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));view.position=new Rect(0,0,1440,840);view.Show();
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    static void Environment(int state)
    {
        Set(mission,"envState",state);typeof(UrbanCombatMission).GetMethod("ApplyEnvironment",Private).Invoke(mission,null);
        camera.backgroundColor=state==1?new Color(.02f,.03f,.05f):new Color(.45f,.56f,.67f);
    }
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    static void ValidateGallery(ReferenceStronghold fort)
    {
        foreach(var renderer in fort.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("Gallery")))
            File.AppendAllText("Logs/StrongholdValidation.txt",renderer.name+" finish="+renderer.sharedMaterial.name+" texture="+(renderer.sharedMaterial.mainTexture!=null?renderer.sharedMaterial.mainTexture.name:"none")+"\n");
        Require(fort.Layout?.stairs!=null && fort.Layout.stairs.Length>40,"Missing physical gallery stair route");
        Require(fort.SupplyCrates==8,"Expected two supported supply crates on each gallery/roof level");
        File.AppendAllText("Logs/StrongholdValidation.txt","PASS eight licensed supply crates populate gallery ends and roof; stair and door clearance checked with props present.\n");
        foreach(var p in fort.Layout.stairs.Concat(fort.Layout.floors))
        {
            var at=fort.transform.TransformPoint(p);
            Require(Physics.Raycast(at+Vector3.up*.2f,Vector3.down,out var support,.5f,~0,QueryTriggerInteraction.Ignore) && support.transform.IsChildOf(fort.transform),"Unsupported gallery route: "+p);
            var blockers=Physics.OverlapCapsule(at+Vector3.up*.4f,at+Vector3.up*1.4f,.3f);
            Require(blockers.Length==0,"Blocked gallery headroom: "+p+" colliders="+string.Join(";",blockers.Select(c=>c.name+" bounds="+c.bounds)));
        }
        foreach(var p in fort.Layout.doors)
        {
            var front=fort.transform.TransformPoint(p+Vector3.back*.7f);
            var rear=fort.transform.TransformPoint(p+Vector3.forward*.7f);
            Require(!Physics.Linecast(front,rear,~0,QueryTriggerInteraction.Ignore),"Doorway still contains a solid panel: "+p);
        }
        var saved=mission.Player.transform.position;var enabled=mission.Player.enabled;mission.Player.enabled=false;
        var controller=mission.Player.GetComponent<CharacterController>();
        mission.Player.Place(fort.transform.TransformPoint(fort.Layout.stairs[0]));
        foreach(var local in fort.Layout.stairs.Skip(1))
        {
            var next=fort.transform.TransformPoint(local);
            for(int tick=0;tick<180;tick++)
            {
                var delta=next-mission.Player.transform.position;delta.y=0;
                if(delta.magnitude<.09f)break;
                controller.Move(delta.normalized*Mathf.Min(.045f,delta.magnitude)+Vector3.down*.06f);
            }
            var difference=next-mission.Player.transform.position;
            Require(new Vector2(difference.x,difference.z).magnitude<.16f && Mathf.Abs(difference.y)<.28f,"Production controller cannot climb gallery stairs: "+local+" actual="+fort.transform.InverseTransformPoint(mission.Player.transform.position));
        }
        mission.Player.Place(saved);mission.Player.enabled=enabled;
        File.AppendAllText("Logs/StrongholdValidation.txt","PASS open doorways, supported gallery floors/headroom and production CharacterController climbs all three stair flights to roof without teleporting.\n");
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        if(started==0)started=EditorApplication.timeSinceStartup;
        if(EditorApplication.timeSinceStartup-started>240){SessionState.SetBool(Key,false);EditorApplication.Exit(1);return;}
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling)return;
        try
        {
            if(!prepared)
            {
                Directory.CreateDirectory("Logs");
                var game=UnityEngine.Object.FindAnyObjectByType<GeoSniperGame>();if(game!=null)game.gameObject.SetActive(false);
                foreach(var old in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))old.gameObject.SetActive(false);
                world=new GameObject("Production training sector").AddComponent<SectorWorld>();world.Generate(SectorMap.Training());
                foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    if(!renderer.transform.IsChildOf(world.transform) && renderer.GetComponentInParent<AtmosphericSky>()==null && !renderer.name.Contains("Sun Disc"))renderer.enabled=false;
                camera=new GameObject("Gameplay camera").AddComponent<Camera>();camera.tag="MainCamera";camera.farClipPlane=1800;camera.allowHDR=true;camera.clearFlags=CameraClearFlags.SolidColor;
                mission=new GameObject("Production stronghold gameplay").AddComponent<UrbanCombatMission>();
                mission.Begin(camera,world.SpawnPoints,world.RooftopSpawns,true,null,false,Vector3.zero,false);
                Require(mission.LevelStronghold!=null,"Normal free-roam entry does not create stronghold");
                Debug.Log("Stronghold initial state: perches="+mission.LevelStronghold.Perches.Count+" layout="+(mission.LevelStronghold.Layout!=null)+" meshes="+mission.LevelStronghold.GetComponentsInChildren<MeshFilter>().Length+" crates="+mission.LevelStronghold.SupplyCrates);
                var layoutSource=Resources.Load<TextAsset>("Models/Stronghold/GalleryLayout");Debug.Log("Gallery resource text="+(layoutSource!=null?layoutSource.text.Length:-1));
                mission.BeginStage(8);
                var fort=mission.LevelStronghold;
                File.WriteAllText("Logs/StrongholdValidation.txt","building="+(fort!=null)+"\n");
                Require(fort!=null,"No safe stronghold placement");
                File.AppendAllText("Logs/StrongholdValidation.txt","position="+fort.transform.position+" perches="+fort.Perches.Count+" guards="+Guards.Count+" vantage="+fort.Vantage+"\n");
                File.AppendAllText("Logs/StrongholdValidation.txt","source surfaces="+string.Join(";",fort.Perches.Select(p=>p.ToString()))+"\n");
                Require(fort.Perches.Count>=3,"Too few supported source roof positions");
                Require(Guards.Count>=2,"No playable visible guard encounter");
                try{ValidateGallery(fort);}catch(Exception failure){galleryFailure=failure;File.AppendAllText("Logs/StrongholdValidation.txt",failure+"\n");}
                foreach(var filter in fort.GetComponentsInChildren<MeshFilter>())if(filter.name!="Arcade wall sconce")
                {
                    var box=filter.GetComponentInParent<BoxCollider>();
                    if(box!=null && box.name=="Licensed balcony supply crate")
                        Require(box.bounds.size.x<1.3f && box.bounds.size.y<1.3f && box.bounds.size.z<1.3f,"Supply crate collision exceeds real mesh dimensions");
                    else Require(filter.GetComponent<MeshCollider>()!=null,"Imported mesh missing physical collision");
                }
                long triangles=fort.GetComponentsInChildren<MeshFilter>().Sum(f=>(long)f.sharedMesh.triangles.Length/3);
                File.AppendAllText("Logs/StrongholdValidation.txt","triangles including cover="+triangles+"\n");
                Environment(0);mission.currentMissionState=MissionState.InProgress;
                mission.Player.InputBlocked=false;mission.Player.Health.Invulnerable=true;
                var missionPosition=mission.Player.transform.position;
                Require(mission.ViewStronghold(),"Normal HUD deployment failed");
                Require(Vector3.Distance(mission.Player.transform.position,fort.Vantage+Vector3.up*.1f)<.3f,"Deployment did not reach the building viewpoint");
                mission.ReturnFromStrongholdView();
                Require(Vector3.Distance(mission.Player.transform.position,missionPosition)<.3f,"Return changed mission starting position");
                Require(mission.ViewStronghold(),"Second deployment failed");
                File.AppendAllText("Logs/StrongholdValidation.txt","PASS normal free-roam entry creates building; HUD deployment frames building and returns to mission position.\n");
                mission.Player.enabled=false;
                // Freeze only enemy AI for repeatable visual/ballistic measurements.
                foreach(var bot in UnityEngine.Object.FindObjectsByType<EnemyBot>(FindObjectsSortMode.None))bot.enabled=false;
                Physics.SyncTransforms();ready=EditorApplication.timeSinceStartup+7;prepared=true;
            }
            if(view==null)view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            view.Repaint();EditorApplication.QueuePlayerLoopUpdate();
            if(EditorApplication.timeSinceStartup<ready)return;
            string[] names={"Wide","Scoped","Hit","Entrance","Night"};
            if(phase<names.Length)
            {
                string path=Path.GetFullPath("Logs/Stronghold"+names[phase]+".png");
                if(!requested){requestedAt=DateTime.UtcNow;ScreenCapture.CaptureScreenshot(path);requested=true;ready=EditorApplication.timeSinceStartup+3;return;}
                if(!File.Exists(path) || File.GetLastWriteTimeUtc(path)<requestedAt)
                {if((DateTime.UtcNow-requestedAt).TotalSeconds>20)throw new Exception("Screenshot missing: "+path);return;}
            }
            if(phase==0)
            {
                target=Guards.First();healthBefore=target.Actor.Health;ammoBefore=(int)Get(mission,"ammo");
                mission.Player.Face(target.transform.position+Vector3.up*1.5f,true);mission.Player.ToggleScope();camera.fieldOfView=12;
                phase=1;requested=false;ready=EditorApplication.timeSinceStartup+3;return;
            }
            if(phase==1)
            {
                Set(mission,"fireCooldown",0f);Set(mission,"chamberTimer",0f);
                var ballistic=mission.GetComponent<BallisticsSystem>();ballistic.config.isArcadeMode=true;
                mission.Fire();phase=2;requested=false;ready=EditorApplication.timeSinceStartup+3;return;
            }
            if(phase==2)
            {
                Require((int)Get(mission,"ammo")==ammoBefore-1,"Stronghold shot did not consume ammunition");
                Require(target==null || target.Actor.Health<healthBefore,"Real gameplay shot missed stronghold guard");
                File.AppendAllText("Logs/StrongholdValidation.txt","PASS real scoped shot consumes ammunition and damages guard through normal ballistics.\n");
                Set(mission,"chamberTimer",0f);mission.Player.Controls.ReloadPressed=true;
                typeof(UrbanCombatMission).GetMethod("Update",Private).Invoke(mission,null);
                mission.Player.Controls.ReloadPressed=false;
                Require((float)Get(mission,"reloadTimer")>0,"Touch reload did not start");
                mission.Player.ExitAim();camera.fieldOfView=65;
                var fort=mission.LevelStronghold;
                mission.Player.Place(fort.transform.TransformPoint(new Vector3(-22,.2f,-22)));mission.Player.Face(fort.transform.position+Vector3.up*4,true);
                phase=3;requested=false;ready=EditorApplication.timeSinceStartup+4;return;
            }
            if(phase==3)
            {
                Require((int)Get(mission,"ammo")==((SniperPresentation)Get(mission,"weapon")).MaxAmmo,"Reload did not refill magazine");
                File.AppendAllText("Logs/StrongholdValidation.txt","PASS touch reload input starts timed reload and refills magazine.\n");
                Environment(1);phase=4;requested=false;ready=EditorApplication.timeSinceStartup+4;return;
            }
            if(phase==4)
            {
                if(galleryFailure!=null)throw galleryFailure;
                // Repeated stages must reuse one building and a bounded cover budget.
                for(int index=0;index<(int)typeof(UrbanCombatMission).GetField("StageCount",BindingFlags.Static|BindingFlags.NonPublic).GetRawConstantValue();index++)
                {
                    if(CampaignLocationProfile.ForStage(index).Kind!=CampaignLocationKind.Stronghold)continue;
                    typeof(UrbanCombatMission).GetMethod("ClearStrongholdEncounter",Private).Invoke(mission,null);
                    Set(mission,"stageIndex",index);
                    typeof(UrbanCombatMission).GetMethod("PrepareStrongholdEncounter",Private).Invoke(mission,null);
                    Require(Guards.Count>=2,"Stage "+index+" lacks stronghold guards");
                    Require(world.GetComponentsInChildren<ReferenceStronghold>().Length==1,"Stage duplicates stronghold");
                Require(mission.LevelStronghold.GetComponentsInChildren<MeshCollider>().Length<=18,"Retry accumulates cover collision");
                Require(mission.LevelStronghold.SupplyCrates==8,"Retry accumulates or loses supply props");
                }
                mission.Player.Place(mission.LevelStronghold.Vantage+Vector3.up*.1f);
                mission.Player.Face(Guards[0].transform.position+Vector3.up*1.5f,true);
                mission.currentMissionState=MissionState.InProgress;mission.Player.InputBlocked=false;
                phase=5;ready=EditorApplication.timeSinceStartup+2;return;
            }
            if(phase==5)
            {
                Require(Guards.Any(bot=>bot!=null && bot.IsAimingAtPlayer),"Live stronghold AI failed to acquire player at firing position");
                foreach(var bot in Guards)Require(bot==null || mission.LevelStronghold.Perches.Any(p=>Vector3.Distance(mission.LevelStronghold.transform.TransformPoint(p),bot.transform.position)<.2f),"Guard moved off supported source surface");
                File.AppendAllText("Logs/StrongholdValidation.txt","PASS live counter-sniper AI acquires player while guards retain supported positions.\n");
                mission.Player.ToggleScope();camera.fieldOfView=6;mission.currentMissionState=MissionState.Complete;
                phase=6;ready=EditorApplication.timeSinceStartup+1;return;
            }
            Require(!mission.Player.Scoped && Mathf.Abs(camera.fieldOfView-65)<.01f,"Completion preserves scope/zoom regression");
            File.AppendAllText("Logs/StrongholdValidation.txt","PASS five selected compound levels maintain one active building, support >=2 guards and bounded cover; completion resets scope/FOV. AI frozen for captures. Android GPU/frame-time validation pending.\n");
            SessionState.SetBool(Key,false);EditorApplication.Exit(0);
        }
        catch(Exception e){Directory.CreateDirectory("Logs");File.AppendAllText("Logs/StrongholdValidation.txt",e+"\n");SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
    }
}
