using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using GeoSniper;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class ReviewRegressionChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly StringBuilder report=new StringBuilder();
    static int failures;
    static void StreetBarriers()
    {
        var root=new GameObject("Street asset check");
        var world=root.AddComponent<SectorWorld>();
        var method=typeof(SectorWorld).GetMethod("RoadBarrier",Private);
        for(int i=0;i<2;i++)
        {
            method.Invoke(world,new object[]{new Vector3(i*1.8f,0,0),Vector3.forward,i==1,null});
            var barrier=root.transform.GetChild(i);
            var renderers=barrier.GetComponentsInChildren<Renderer>();
            Require(renderers.Length==1,"Barrier needs one renderer");
            Require(renderers[0].sharedMaterial.mainTexture!=null,"Missing barrier albedo");
            Require(renderers[0].sharedMaterial.GetTexture("_BumpMap")!=null,"Missing barrier normal map");
            var bounds=renderers[0].bounds;
            Require(Mathf.Abs(bounds.min.y)<.01f && Vector3.Distance(bounds.size,new Vector3(.6f,.95f,2.4f))<.02f,"Incorrect barrier dimensions or ground contact");
            Require(barrier.GetComponent<BoxCollider>()!=null,"Barrier lost its cover collider");
            var mesh=barrier.GetComponentInChildren<MeshFilter>().sharedMesh;
            Require(mesh.GetIndexCount(0)/3<=500,"Barrier exceeds mobile mesh budget");
        }
        var cameraObject=new GameObject("Barrier preview");var camera=cameraObject.AddComponent<Camera>();
        camera.transform.position=new Vector3(4,3,-5);camera.transform.LookAt(new Vector3(.9f,.4f,0));
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.15f,.18f);
        var lamp=new GameObject("Barrier preview light");var light=lamp.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;lamp.transform.rotation=Quaternion.Euler(45,-30,0);
        var target=new RenderTexture(960,640,24);camera.targetTexture=target;camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(960,640,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,640),0,0);image.Apply();
        Directory.CreateDirectory("Logs");File.WriteAllBytes("Logs/StreetBarrierPreview.png",image.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;target.Release();
        Object.DestroyImmediate(target);Object.DestroyImmediate(image);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(lamp);Object.DestroyImmediate(root);
    }
    static void Require(bool condition,string message) {if(!condition) throw new Exception(message);}
    static void Check(string name,Action test)
    {
        try {test();report.AppendLine("PASS: "+name);}
        catch(Exception e) {failures++;report.AppendLine("FAIL: "+name+"\n"+e);}
    }
    [MenuItem("Geo Sniper/Validate Review Fixes")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Run in Edit mode.");
        var previous=SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,Application.isBatchMode?NewSceneMode.Single:NewSceneMode.Additive);
        report.Clear();failures=0;report.AppendLine(DateTime.UtcNow.ToString("O"));
        try
        {
            SceneManager.SetActiveScene(scene);
            Check("ad test IDs, reward completion, concurrent requests and unavailable transitions",AdFlowChecks.Run);
            Check("campaign graph, unlocks, save preservation and explicit mission types",CampaignProgressionChecks.Graph);
            Check("easy-to-hard profiles across all branches and user settings",CampaignProgressionChecks.Profiles);
            Check("connected courier routes and invalid-route rejection",CampaignProgressionChecks.Routes);
            Check("single campaign payout and standalone reward isolation",CampaignProgressionChecks.Rewards);
            Check("scaled and rotated exact hits, range, misses and cover",ScaledShots);
            Check("skinned hit distances under scaled parents",ScaledSkinnedShots);
            Check("civilian shots block enemies and death notifies once",CivilianShots);
            Check("lethal damage is dead before damage listeners; invalid damage ignored",DeathOrdering);
            Check("difficulty health, acoustic radius and isolated alerts",DifficultyAndNoise);
            Check("missed rifle shot provokes a prompt long-range response",MissedShotResponse);
            Check("investigation probe stays on reachable supported ground",InvestigationProbe);
            Check("cover peeks from a reachable firing lane without shooting through cover",CoverPeekLane);
            Check("squad members avoid a reserved strafe lane",SquadStrafe);
            Check("coordinated flank needs covering fire and supported ground",SquadFlank);
            Check("three extraction waves respect cap and final arrival hold",ExtractionWaves);
            Check("opposite traffic lanes",TrafficLanes);
            Check("Sketchfab barriers: grounded bounds, textures and mobile mesh budget",StreetBarriers);
            Check("tank health respects post-AddComponent configuration",PropHealth);
            Check("all shipped character placements and vehicle orientation bounds",Assets);
            Check("fallback animation preserves placement",AnimationRest);
            Check("enemy reload lowers rifle, blocks aiming and restores magazine on both shipped rigs",CharacterReload);
            Check("disarmed enemies cannot fire at player or VIP, including an active burst",DisarmedFire);
            Check("all imported gameplay mesh bounds and inventory",GameplayGeometryAudit.Run);
            Check("three sniper models use metre-scale grip calibration",WeaponShapes);
            Check("existing rooftop occlusion checks",()=>typeof(CombatHitChecks).GetMethod("CheckRooftopShots",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{report}));
            Check("existing animated skin checks",()=>typeof(CombatHitChecks).GetMethod("CheckVisibleSkin",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{report}));
        }
        finally
        {
            if(!Application.isBatchMode) {EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);}
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/ReviewRegressionChecks.txt",report.ToString());
        }
        Debug.Log(report.ToString());
        if(failures>0) throw new Exception(failures+" review regression groups failed");
    }

    static void WeaponShapes()
    {
        string[] names={"Barrett50","M24Tactical","MK12SPR"};
        for(int i=0;i<names.Length;i++)
        {
            var root=new GameObject("Weapon calibration");
            try
            {
                var asset=Resources.Load<GameObject>("Models/Weapons/"+names[i]);
                Require(asset!=null,"Missing weapon "+names[i]);
                var model=Object.Instantiate(asset,root.transform,false);
                Require(WeaponGeometry.Configure(model.transform,root.transform,i),"Weapon calibration failed");
                var bounds=ImportedVisual.LocalBounds(root.transform);
                report.AppendLine(names[i]+" calibrated "+bounds);
                Require(Mathf.Abs(bounds.size.z-WeaponGeometry.Length(i))<.01f,"Weapon length changed with source units");
                Require(bounds.size.z>bounds.size.x*2 && bounds.size.z>bounds.size.y*2,"Weapon barrel is not longitudinal +Z");
                foreach(var collider in model.GetComponentsInChildren<Collider>(true)) Require(!collider.enabled,"Weapon can intercept its own bullets");
                RenderAsset(root,names[i],2f);
            }
            finally {Object.DestroyImmediate(root);}
        }
        Require(!WeaponGeometry.Usable(new Bounds(Vector3.zero,Vector3.zero)),"Empty model accepted");
        Require(!WeaponGeometry.Usable(new Bounds(new Vector3(float.NaN,0,0),Vector3.one)),"Nonfinite model accepted");
    }

    static void InvestigationProbe()
    {
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
        var root=new GameObject("Investigation bot");
        var target=new GameObject("Investigation target");
        try
        {
            Vector3 origin=new Vector3(24000,0,24000);
            floor.transform.position=origin+Vector3.down*.5f;
            floor.transform.localScale=new Vector3(16,1,16);
            root.transform.position=origin;
            target.transform.position=origin+Vector3.forward*20f;
            var bot=root.AddComponent<EnemyBot>();bot.Initialize(target.transform,0);
            typeof(EnemyBot).GetField("searchAnchor",Private).SetValue(bot,origin+Vector3.forward);
            Physics.SyncTransforms();
            var choose=typeof(EnemyBot).GetMethod("TryChooseSearchProbe",Private);
            var args=new object[]{Vector3.zero};
            Require((bool)choose.Invoke(bot,args),"Open supported ground did not produce a search probe");
            var probe=(Vector3)args[0];
            Require(Vector3.Distance(probe,origin)>2f && Vector3.Distance(probe,origin)<4f,"Search probe outside bounded area");
            Object.DestroyImmediate(floor);
            Physics.SyncTransforms();
            Require(!(bool)choose.Invoke(bot,args),"Investigation accepted unsupported ground");
        }
        finally {Object.DestroyImmediate(root);Object.DestroyImmediate(target);if(floor!=null)Object.DestroyImmediate(floor);}
    }

    static void CoverPeekLane()
    {
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
        var cover=GameObject.CreatePrimitive(PrimitiveType.Cube);
        var root=new GameObject("Peek guard");
        var target=new GameObject("Peek target");
        try
        {
            Vector3 origin=new Vector3(25000,0,25000);
            floor.transform.position=origin+Vector3.down*.5f;floor.transform.localScale=new Vector3(20,1,20);
            cover.transform.position=origin+new Vector3(0,.85f,3f);cover.transform.localScale=new Vector3(4,1.7f,.5f);
            root.transform.position=origin;target.transform.position=origin+Vector3.forward*10f;
            var bot=root.AddComponent<EnemyBot>();bot.Initialize(target.transform,0);
            typeof(EnemyBot).GetField("lastSeenPosition",Private).SetValue(bot,target.transform.position);
            Physics.SyncTransforms();
            var hits=new RaycastHit[16];var targetEye=target.transform.position+Vector3.up*1.55f;
            Require(!EnemyCoverGeometry.ClearShot(origin+Vector3.up*1.65f,targetEye,root.transform,target.transform,hits),"Guard could fire through solid cover");
            var choose=typeof(EnemyBot).GetMethod("TryChooseCoverPeek",Private);
            var args=new object[]{Vector3.zero};
            Require((bool)choose.Invoke(bot,args),"Reachable side firing lane was missed");
            var peek=(Vector3)args[0];
            Require(Mathf.Abs(peek.x-origin.x)>2f && EnemyCoverGeometry.Reachable(origin,peek,root.transform,hits),"Peek did not step around cover safely");
            Require(EnemyCoverGeometry.ClearShot(peek+Vector3.up*1.35f,targetEye,root.transform,target.transform,hits),"Peek muzzle still blocked");
            floor.transform.localScale=new Vector3(1.5f,1f,20f);Physics.SyncTransforms();
            Require(!(bool)choose.Invoke(bot,args),"Peek accepted unsupported side step");
        }
        finally {Object.DestroyImmediate(root);Object.DestroyImmediate(target);Object.DestroyImmediate(cover);Object.DestroyImmediate(floor);}
    }

    static void SquadStrafe()
    {
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
        var first=new GameObject("First squad guard");var second=new GameObject("Second squad guard");
        var target=new GameObject("Squad target");
        try
        {
            Vector3 origin=new Vector3(26000,0,26000);
            floor.transform.position=origin+Vector3.down*.5f;floor.transform.localScale=new Vector3(30,1,30);
            first.transform.position=origin+Vector3.back*8f;second.transform.position=origin;
            target.transform.position=origin+Vector3.forward*20f;
            var alerts=new MissionAlertState();
            var ally=first.AddComponent<EnemyBot>();ally.Alerts=alerts;ally.Initialize(target.transform,0);
            var bot=second.AddComponent<EnemyBot>();bot.Alerts=alerts;bot.Initialize(target.transform,0);
            if(!EnemyBot.AllBots.Contains(ally))EnemyBot.AllBots.Add(ally);
            if(!EnemyBot.AllBots.Contains(bot))EnemyBot.AllBots.Add(bot);
            ally.currentState=AIState.Combat;
            typeof(EnemyBot).GetField("combatTargetReady",Private).SetValue(ally,true);
            typeof(EnemyBot).GetField("combatTarget",Private).SetValue(ally,origin+Vector3.right*5.5f);
            Physics.SyncTransforms();
            typeof(EnemyBot).GetMethod("ChooseCombatStrafeTarget",Private).Invoke(bot,new object[]{target.transform.position-origin});
            var chosen=(Vector3)typeof(EnemyBot).GetField("combatTarget",Private).GetValue(bot);
            Require(chosen.x<origin.x-3f,"Second guard reused the squad's reserved flank: chosen="+chosen+" origin="+origin+" allBots="+EnemyBot.AllBots.Count);
        }
        finally {EnemyBot.AllBots.Remove(first.GetComponent<EnemyBot>());EnemyBot.AllBots.Remove(second.GetComponent<EnemyBot>());Object.DestroyImmediate(first);Object.DestroyImmediate(second);Object.DestroyImmediate(target);Object.DestroyImmediate(floor);}
    }

    static void ExtractionWaves()
    {
        var plan=new ExtractionWavePlan(30,99);
        Require(plan.ActiveCap==8 && plan.Wave==1,"Android cap or initial wave incorrect");
        plan.Tick(10);
        Require(!plan.StartNextWave(8),"Full squad admitted reinforcements");
        Require(plan.StartNextWave(7) && plan.Wave==2,"Due second wave did not start");
        Require(plan.CanSpawn(7) && !plan.CanSpawn(8),"Per-actor capacity gate failed");
        plan.Tick(100);
        Require(!plan.ReadyForExtraction && !plan.StartNextWave(0),"Timer skipped an undelivered wave");
        while(plan.Pending>0)plan.RecordSpawn();
        Require(!plan.StartNextWave(0),"Recovery interval was skipped");
        plan.Tick(6);
        Require(plan.StartNextWave(0) && plan.Wave==3,"Final wave was not scheduled");
        while(plan.Pending>0)plan.RecordSpawn();
        Require(!plan.ReadyForExtraction,"Extraction opened at final spawn");
        plan.Tick(5.9f);Require(!plan.ReadyForExtraction,"Final defense window too short");
        plan.Tick(.2f);Require(plan.ReadyForExtraction && !plan.StartNextWave(0),"Wave plan never completed or added a fourth wave");
        var retry=new ExtractionWavePlan(30,4);
        Require(retry.Elapsed==0 && retry.Wave==1 && retry.Pending==0,"Retry inherited wave state");
    }

    static void SquadFlank()
    {
        var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
        var first=new GameObject("Covering guard");var second=new GameObject("Flanking guard");
        var target=new GameObject("Flank target");
        EnemyBot support=null,flanker=null;
        try
        {
            Vector3 origin=new Vector3(28000,0,28000);
            floor.transform.position=origin+Vector3.down*.5f;floor.transform.localScale=new Vector3(80,1,80);
            first.transform.position=origin+Vector3.back*6;second.transform.position=origin;
            target.transform.position=origin+Vector3.forward*50;
            var alerts=new MissionAlertState();
            support=first.AddComponent<EnemyBot>();support.Alerts=alerts;support.Initialize(target.transform,0);
            flanker=second.AddComponent<EnemyBot>();flanker.Alerts=alerts;flanker.Initialize(target.transform,1);
            if(!EnemyBot.AllBots.Contains(support))EnemyBot.AllBots.Add(support);
            if(!EnemyBot.AllBots.Contains(flanker))EnemyBot.AllBots.Add(flanker);
            support.BeginAssault(target.transform.position);flanker.BeginAssault(target.transform.position);
            typeof(EnemyBot).GetMethod("UpdateSquadRole",Private).Invoke(support,new object[]{.1f});
            Require(support.currentRole==CombatRole.Suppressor && flanker.currentRole==CombatRole.Flanker,"Sight/radio assault did not assign distinct roles");
            typeof(EnemyBot).GetField("playerVisible",Private).SetValue(support,true);
            Physics.SyncTransforms();
            var choose=typeof(EnemyBot).GetMethod("TryFlankDestination",Private);
            var args=new object[]{target.transform.position,Vector3.zero};
            Require((bool)choose.Invoke(flanker,args) && Mathf.Abs(((Vector3)args[1]).x-origin.x)>=8,"Flanker did not choose a meaningful side approach");
            typeof(EnemyBot).GetMethod("UpdateFlank",Private).Invoke(flanker,new object[]{true,.1f});
            Require(flanker.IsFlanking,"Supported flank failed to start");
            support.Suspended=true;
            typeof(EnemyBot).GetMethod("UpdateFlank",Private).Invoke(flanker,new object[]{false,.1f});
            Require(!flanker.IsFlanking,"Flanker continued after losing covering teammate");
            floor.transform.localScale=new Vector3(2,1,80);Physics.SyncTransforms();
            Require(!(bool)choose.Invoke(flanker,args),"Flanker accepted an unsupported side route");
        }
        finally
        {
            EnemyBot.AllBots.Remove(support);EnemyBot.AllBots.Remove(flanker);
            Object.DestroyImmediate(first);Object.DestroyImmediate(second);Object.DestroyImmediate(target);Object.DestroyImmediate(floor);
        }
    }

    static EnemyBot CubeEnemy(Vector3 position,out GameObject root)
    {
        root=new GameObject("Ray target");root.transform.position=position;
        var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.transform.SetParent(root.transform,false);
        cube.transform.localPosition=Vector3.up;Object.DestroyImmediate(cube.GetComponent<Collider>());
        var enemy=root.AddComponent<EnemyBot>();enemy.Initialize(null,0);
        root.AddComponent<EnemyHitboxes>().Initialize(cube.transform);return enemy;
    }
    static void ScaledShots()
    {
        var bot=CubeEnemy(new Vector3(17000,10,17000),out var root);
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            foreach(var scale in new[]{Vector3.one,Vector3.one*.1f,new Vector3(2,.5f,3)})
            foreach(float yaw in new[]{0f,90f,180f,270f})
            {
                wall.SetActive(false);root.transform.localScale=scale;root.transform.rotation=Quaternion.Euler(0,yaw,0);
                var point=root.transform.TransformPoint(new Vector3(0,1,-.5f));
                var direction=root.transform.forward;
                var ray=new Ray(point-direction*20,direction);
                Require(SniperHitQuery.TryCast(ray,new[]{bot},null,null,out var hit,30),"Scaled hit missing");
                Require(Mathf.Abs(hit.Distance-20)<.015f,"World distance changed with transform: "+hit.Distance);
                Require(!SniperHitQuery.TryCast(ray,new[]{bot},null,null,out _,19),"Hit exceeded range");
                // This passes through the controller/fallback but outside the cube.
                var miss=new Ray(root.transform.TransformPoint(new Vector3(.51f,1,-10)),direction);
                Require(!SniperHitQuery.TryCast(miss,new[]{bot},null,null,out _,100),"Invisible volume registered a miss as a hit");
                wall.SetActive(true);wall.transform.position=ray.GetPoint(10);
                Require(SniperHitQuery.Cast(ray,new[]{bot},null,null,30)==wall.GetComponent<Collider>(),"Cover penetrated");
                Require(!root.GetComponentsInChildren<Collider>().Any(c=>c.isTrigger && c.enabled),"Hitbox leaked into physics");
            }
        }
        finally {Object.DestroyImmediate(root);Object.DestroyImmediate(wall);}
    }
    static void CivilianShots()
    {
        var enemy=CubeEnemy(new Vector3(18000,0,18020),out var root);
        var civObject=GameObject.CreatePrimitive(PrimitiveType.Cube);civObject.transform.position=new Vector3(18000,1,18000);
        Object.DestroyImmediate(civObject.GetComponent<Collider>());
        var civ=civObject.AddComponent<CivilianBot>();civ.Initialize(0);
        civObject.AddComponent<EnemyHitboxes>().Initialize(civObject.transform);
        int deaths=0;Action<CivilianBot> observer=c=>{if(c==civ) deaths++;};DamageSystem.OnCivilianKilled+=observer;
        try
        {
            var ray=new Ray(new Vector3(18000,1,17980),Vector3.forward);
            Require(SniperHitQuery.TryCast(ray,new[]{enemy},null,null,out var hit,80),"Civilian not hit");
            Require(hit.Collider.GetComponentInParent<CivilianBot>()==civ,"Shot passed through civilian");
            DamageSystem.ProcessHit(hit.Collider,100,hit.Point);
            DamageSystem.ProcessHit(hit.Collider,100,hit.Point);
            Require(civ.Actor.IsDead && deaths==1,"Civilian death count: "+deaths);
        }
        finally {DamageSystem.OnCivilianKilled-=observer;Object.DestroyImmediate(civObject);Object.DestroyImmediate(root);}
    }
    static void ScaledSkinnedShots()
    {
        var root=new GameObject("Scaled skin");var mesh=new Mesh();
        try
        {
            root.transform.position=new Vector3(23000,5,23000);
            var visual=new GameObject("Mesh");visual.transform.SetParent(root.transform,false);
            var bone=new GameObject("Bone");bone.transform.SetParent(visual.transform,false);
            mesh.vertices=new[]{new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0),new Vector3(0,1.5f,0)};
            mesh.triangles=new[]{0,1,2};mesh.boneWeights=Enumerable.Repeat(new BoneWeight{boneIndex0=0,weight0=1},3).ToArray();
            mesh.bindposes=new[]{Matrix4x4.identity};mesh.RecalculateBounds();
            var skin=visual.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.bones=new[]{bone.transform};skin.rootBone=bone.transform;
            var bot=root.AddComponent<EnemyBot>();bot.Initialize(null,0);root.AddComponent<EnemyHitboxes>().Initialize(visual.transform);
            foreach(var scale in new[]{Vector3.one*.5f,Vector3.one*2,new Vector3(2,.5f,3)})
            {
                root.transform.localScale=scale;root.transform.rotation=Quaternion.Euler(0,90,0);
                var point=root.transform.TransformPoint(Vector3.up);
                var ray=new Ray(point-root.transform.forward*20,root.transform.forward);
                Require(SniperHitQuery.TryCast(ray,new[]{bot},null,null,out var hit,30),"Scaled skinned triangle missed");
                Require(Vector3.Distance(hit.Point,point)<.015f,"Skinned impact shifted by parent scale");
            }
        }
        finally {Object.DestroyImmediate(root);Object.DestroyImmediate(mesh);}
    }
    static void DeathOrdering()
    {
        var obj=new GameObject("Damage order");var actor=obj.AddComponent<CombatActor>();actor.Initialize(60);
        bool deadWhenDamaged=false;int deaths=0;
        actor.OnDamaged+=d=>deadWhenDamaged=actor.IsDead;actor.OnDeath+=a=>deaths++;
        actor.Damage(float.NaN);actor.Damage(-20);Require(actor.Health==60,"Invalid damage changed health");
        actor.Damage(100);actor.Damage(10);
        Require(deadWhenDamaged && deaths==1 && actor.Health==0,"Death ordering/clamping failed");Object.DestroyImmediate(obj);
    }
    static void DifficultyAndNoise()
    {
        foreach(DifficultyLevel level in Enum.GetValues(typeof(DifficultyLevel)))
        {
            var profile=DifficultyProfile.Create(level);var obj=new GameObject("Noise bot");
            var otherObj=new GameObject("Other mission");
            try
            {
                obj.transform.position=new Vector3(19000,0,19000);otherObj.transform.position=obj.transform.position;
                var bot=obj.AddComponent<EnemyBot>();bot.Difficulty=profile;bot.Initialize(null,0);
                var other=otherObj.AddComponent<EnemyBot>();other.Initialize(null,0);
                bot.SetHealth(100);Require(Mathf.Abs(bot.Actor.Health-100*profile.enemyHealth)<.01f,"Role health bypasses difficulty");
                var hear=typeof(EnemyBot).GetMethod("HearGunshot",Private);
                hear.Invoke(bot,new object[]{obj.transform.position+Vector3.forward*40,25f});
                Require(bot.currentState==AIState.Patrol,"Distant suppressed shot heard");
                hear.Invoke(bot,new object[]{obj.transform.position+Vector3.forward*40,130f});
                Require(bot.currentState==AIState.TakeCover && bot.detectionProgress>=.9f,"Nearby unsuppressed shot did not trigger cover response: state="+bot.currentState+" detection="+bot.detectionProgress+" suspended="+bot.Suspended+" health="+bot.Actor.Health);
                bot.BroadcastRadioAlert(obj.transform.position,AIState.Combat);
                Require(other.Alerts.Level==0 && other.currentState==AIState.Patrol,"Alert leaked between missions");
                bot.Alerts.Reset();Require(bot.Alerts.Level==0,"Alert reset failed");
                Require(profile.duelLockSeconds>0 && profile.stealthAlertLimit>=1,"Invalid profile");
            }
            finally {Object.DestroyImmediate(obj);Object.DestroyImmediate(otherObj);Object.DestroyImmediate(profile);}
        }
    }
    static void MissedShotResponse()
    {
        var shooter=new GameObject("Test shooter");
        var root=new GameObject("Test defender");
        try
        {
            shooter.transform.position=new Vector3(30000,0,30000);
            root.transform.position=shooter.transform.position+Vector3.forward*100;
            var bot=root.AddComponent<EnemyBot>();bot.Initialize(shooter.transform,0);
            bot.currentState=AIState.Patrol;
            // Edit-mode AddComponent does not consistently run MonoBehaviour.OnEnable.
            if(!EnemyBot.AllBots.Contains(bot)) EnemyBot.AllBots.Add(bot);
            EnemyBot.NotifyMissedShot(shooter.transform.position,root.transform.position+Vector3.right);
            Require(bot.currentState==AIState.TakeCover && bot.detectionProgress>=1f,
                "A close miss failed to identify the shooter: state="+bot.currentState+" detection="+bot.detectionProgress+" suspended="+bot.Suspended+" health="+bot.Actor.Health);
            Require(bot.coverTimer<=1.8f && (float)typeof(EnemyBot).GetField("fireCooldown",Private).GetValue(bot)<=1.2f,
                "A close miss left the defender waiting too long to retaliate");
            Require((float)typeof(EnemyBot).GetField("provokedUntil",Private).GetValue(bot)>Time.time,
                "Long-range threat visibility was not enabled");
        }
        finally {EnemyBot.AllBots.Remove(root.GetComponent<EnemyBot>());Object.DestroyImmediate(root);Object.DestroyImmediate(shooter);}
    }
    static void TrafficLanes()
    {
        var path=new List<Vector3>{new Vector3(20000,0,20000),new Vector3(20000,0,20050)};
        var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Local terrain";
        ground.transform.position=new Vector3(20000,-.5f,20025);ground.transform.localScale=new Vector3(100,1,100);
        Physics.SyncTransforms();
        var a=new GameObject("Outbound");var b=new GameObject("Inbound");
        a.AddComponent<TrafficVehicle>().Initialize(path,0,1);b.AddComponent<TrafficVehicle>().Initialize(path,1,-1);
        Require((a.transform.position.x-20000)*(b.transform.position.x-20000)<0,"Opposing cars share lane");
        Require(Vector3.Dot(a.transform.forward,b.transform.forward)<-.99f,"Car facing mismatch");
        Object.DestroyImmediate(a);Object.DestroyImmediate(b);Object.DestroyImmediate(ground);
    }
    static void PropHealth()
    {
        var obj=new GameObject("Tank health");var prop=obj.AddComponent<ExplosiveProp>();prop.maxHealth=150;
        prop.TakeDamage(100,Vector3.zero);Require(!prop.isDetonated,"Tank retained 50 HP from Awake");Object.DestroyImmediate(obj);
    }
    static void Assets()
    {
        var errors=new List<string>();
        foreach(string category in new[]{"Enemies","Civilians"})
        foreach(var asset in ModelLibrary.Load(category))
        {
            Require(AssetCalibration.TryGet(asset.name,out _),"Missing calibration "+asset.name);
            var root=new GameObject("Calibration test");root.transform.position=new Vector3(21000,20,21000);
            try
            {
                if(asset.name=="civilian_tf2c")
                {
                    var probe=new GameObject("Scale probe");probe.transform.SetParent(root.transform,false);
                    Object.Instantiate(asset,probe.transform,false);
                    foreach(var skin in probe.GetComponentsInChildren<SkinnedMeshRenderer>()) report.AppendLine("tf2 skin "+skin.name+" bones="+skin.bones.Length+" root="+skin.rootBone+" localScale="+skin.transform.localScale+" lossy="+skin.transform.lossyScale);
                    report.AppendLine("tf2 probe unit "+ImportedVisual.PosedBounds(probe.transform));
                    probe.transform.localScale=Vector3.one*.5f;
                    report.AppendLine("tf2 probe half in parent "+ImportedVisual.PosedBounds(root.transform));
                    foreach(var skin in probe.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var mesh=new Mesh();skin.BakeMesh(mesh,false);var a=mesh.bounds;
                        skin.BakeMesh(mesh,true);report.AppendLine("tf2 half bake false="+a+" true="+mesh.bounds);
                        Object.DestroyImmediate(mesh);
                    }
                    Object.DestroyImmediate(probe);
                }
                var visual=ImportedVisual.CreateEnemy(asset,root.transform);
                report.AppendLine("placement "+asset.name+" scale="+visual.transform.GetChild(0).localScale);
                foreach(float yaw in new[]{0f,90f,180f,270f})
                {
                    root.transform.rotation=Quaternion.Euler(0,yaw,0);
                    var bounds=ImportedVisual.PosedBounds(root.transform);
                    report.AppendLine(asset.name+" yaw="+yaw+" "+bounds);
                    if(Mathf.Abs(bounds.size.y-1.85f)>=.03f || Mathf.Abs(bounds.min.y)>=.03f)
                        errors.Add("Bad normalized body "+asset.name+" yaw="+yaw+": "+bounds);
                }
                root.transform.rotation=Quaternion.identity;
                if(category=="Civilians")
                {
                    var idle=visual.GetComponentInChildren<Animation>(); if(idle!=null) idle.enabled=false;
                    var civilian=root.AddComponent<CivilianBot>();
                    var gait=root.AddComponent<EnemyWalkAnimator>();gait.Initialize(civilian,visual.transform);
                    var posed=ImportedVisual.PosedBounds(root.transform);
                    report.AppendLine(asset.name+" relaxed bounds="+posed+" valid legs="+gait.HasValidLegs);
                    Require(gait.HasValidLegs,"Moving civilian has no usable locomotion rig");
                    Require(posed.size.y>1.7f && posed.size.y<2f,"Relaxed pose distorted body height");
                }
                RenderAsset(root,asset.name,3f);
            }
            finally {Object.DestroyImmediate(root);}
        }
        var vehicles=new List<GameObject>(ModelLibrary.Load("Cars"));vehicles.Add(Resources.Load<GameObject>("Models/Tank"));
        foreach(var asset in vehicles)
        {
            Require(asset!=null,"Missing vehicle");
            Require(AssetCalibration.TryGet(asset.name,out _),"Missing vehicle calibration");
            var root=new GameObject("Vehicle calibration");root.transform.position=new Vector3(22000,0,22000);
            try
            {
                var visual=Object.Instantiate(asset,root.transform,false);
                report.AppendLine(asset.name+" imported root rotation="+visual.transform.localEulerAngles+" bounds="+ImportedVisual.LocalBounds(root.transform));
                var bounds=ImportedVisual.AlignVehicle(root,visual,asset.name=="Tank",asset.name=="PoliceCar");
                if(!(bounds.size.z>bounds.size.x && bounds.size.z>bounds.size.y)) errors.Add("Vehicle not longitudinal +Z: "+asset.name+" "+bounds);
                visual.transform.localScale*=4.5f/bounds.size.z;
                bounds=ImportedVisual.LocalBounds(root.transform);visual.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                var col=root.AddComponent<BoxCollider>();bounds=ImportedVisual.LocalBounds(root.transform);col.center=bounds.center;col.size=bounds.size;
                foreach(float yaw in new[]{0f,90f,180f,270f})
                {
                    root.transform.rotation=Quaternion.Euler(0,yaw,0);
                    Require(Vector3.Distance(ImportedVisual.LocalBounds(root.transform).size,bounds.size)<.03f,"Collider changed with root yaw");
                }
                root.transform.rotation=Quaternion.identity;RenderAsset(root,asset.name,6f);
            }
            finally {Object.DestroyImmediate(root);}
        }
        Require(errors.Count==0,string.Join("\n",errors));
    }
    static void AnimationRest()
    {
        var root=new GameObject("Animation root");var visual=new GameObject("Placement");visual.transform.SetParent(root.transform,false);
        visual.transform.localPosition=new Vector3(.2f,.3f,.4f);visual.transform.localRotation=Quaternion.Euler(0,90,0);
        var saved=visual.transform.localPosition;var rotation=visual.transform.localRotation;
        var civ=root.AddComponent<CivilianBot>();var walk=root.AddComponent<EnemyWalkAnimator>();walk.Initialize(civ,visual.transform);
        typeof(EnemyWalkAnimator).GetMethod("Update",Private).Invoke(walk,null);
        Require(Vector3.Distance(saved,visual.transform.localPosition)<.001f && Quaternion.Angle(rotation,visual.transform.localRotation)<.001f,"Animation overwrote calibrated root");
        Object.DestroyImmediate(root);
    }
    static void CharacterReload()
    {
        foreach(var prefab in ModelLibrary.Load("Enemies"))
        {
            var root=new GameObject("Reload pose "+prefab.name);
            var target=new GameObject("Reload target");
            try
            {
                target.transform.position=root.transform.position+Vector3.forward*30;
                var visual=ImportedVisual.CreateEnemy(prefab,root.transform);
                var bot=root.AddComponent<EnemyBot>();bot.Initialize(target.transform,0);
                var driver=root.AddComponent<ArmyAnimation>();
                Require(driver.Initialize(bot,visual.transform),"Missing enemy clip rig: "+prefab.name);
                var mission=root.AddComponent<UrbanCombatMission>();
                typeof(UrbanCombatMission).GetMethod("BuildEnemyWeapon",Private).Invoke(mission,new object[]{root,visual.transform,bot});
                var anchor=root.GetComponentInChildren<EnemyWeaponAnchor>();
                Require(anchor!=null,"Missing weapon grip anchor: "+prefab.name);
                bot.currentState=AIState.Combat;
                typeof(EnemyWeaponAnchor).GetMethod("LateUpdate",Private).Invoke(anchor,null);
                Quaternion ready=anchor.transform.rotation;
                typeof(EnemyBot).GetMethod("StartReload",Private).Invoke(bot,null);
                Require(bot.IsReloading && !bot.IsAiming,"Reload failed to interrupt aim: "+prefab.name);
                typeof(EnemyBot).GetField("enemyReloadRemaining",Private).SetValue(bot,1f);
                typeof(EnemyWeaponAnchor).GetMethod("LateUpdate",Private).Invoke(anchor,null);
                Require(Quaternion.Angle(ready,anchor.transform.rotation)>5f,"Rifle did not lower for reload: "+prefab.name);
                typeof(GameplayFixChecks).GetMethod("Render",BindingFlags.Static|BindingFlags.NonPublic)
                    .Invoke(null,new object[]{root,"Reload-"+prefab.name});
                var support=anchor.transform.Find("WeaponSupport");
                Require(support!=null && anchor.leftHand!=null,"Reload lacks support hand: "+prefab.name);
                typeof(EnemyBot).GetMethod("TickReload",Private).Invoke(bot,new object[]{1.1f});
                Require(!bot.IsReloading && (int)typeof(EnemyBot).GetField("magazineRounds",Private).GetValue(bot)==18,
                    "Reload did not restore ammunition: "+prefab.name);
            }
            finally {Object.DestroyImmediate(root);Object.DestroyImmediate(target);}
        }
    }
    static void DisarmedFire()
    {
        var root=new GameObject("Disarmed guard");
        var player=new GameObject("Unarmed target");
        var vipRoot=new GameObject("VIP target");
        var rifle=GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            player.transform.position=new Vector3(0,0,12);
            vipRoot.transform.position=new Vector3(0,0,10);
            rifle.name="Enemy tactical rifle";rifle.transform.SetParent(root.transform,false);
            var bot=root.AddComponent<EnemyBot>();bot.Initialize(player.transform,0);
            var vip=vipRoot.AddComponent<CivilianBot>();vip.Initialize(0);
            bot.RegisterWeapon(rifle.transform,null);
            bot.currentState=AIState.Combat;
            Require(bot.IsAiming,"Armed guard did not aim before disarm");
            bot.Disarm();
            Require(bot.isDisarmed && !bot.IsAiming && rifle.transform.parent==null,"Dropped rifle remains attached or guard still aims");
            var normal=(System.Collections.IEnumerator)typeof(EnemyBot).GetMethod("ExecuteBurst",Private)
                .Invoke(bot,new object[]{player.transform.position,12f});
            var escort=(System.Collections.IEnumerator)typeof(EnemyBot).GetMethod("ExecuteBurstAtVIP",Private)
                .Invoke(bot,new object[]{vip,10f});
            Require(!normal.MoveNext() && !escort.MoveNext(),"A dropped rifle still started a firing burst");
            var line=root.GetComponent<LineRenderer>();
            Require(line!=null && !line.enabled,"Disarm left a live shot tracer");
        }
        finally {Object.DestroyImmediate(root);Object.DestroyImmediate(player);Object.DestroyImmediate(vipRoot);Object.DestroyImmediate(rifle);}
    }
    static void RenderAsset(GameObject root,string name,float distance)
    {
        var cameraObject=new GameObject("Review camera");var lamp=new GameObject("Review light");var arrow=GameObject.CreatePrimitive(PrimitiveType.Cube);
        var texture=new Texture2D(480,360,TextureFormat.RGB24,false);var target=new RenderTexture(480,360,24);
        var old=RenderTexture.active;
        try
        {
            var camera=cameraObject.AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.18f,.23f,.29f);
            camera.transform.position=root.transform.position+new Vector3(distance*.7f,distance*.45f,distance);
            camera.transform.LookAt(root.transform.position+Vector3.up*.9f);camera.targetTexture=target;camera.farClipPlane=30;
            var light=lamp.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(40,-25,0);
            arrow.transform.position=root.transform.position+Vector3.forward*(distance*.4f)+Vector3.up*.03f;
            arrow.transform.localScale=new Vector3(.12f,.06f,distance*.3f);
            var material=new Material(Shader.Find("Standard"));material.color=Color.green;arrow.GetComponent<Renderer>().sharedMaterial=material;
            camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,480,360),0,0);texture.Apply();
            Directory.CreateDirectory("Logs/AssetReview");File.WriteAllBytes("Logs/AssetReview/"+name+".png",texture.EncodeToPNG());
            Object.DestroyImmediate(material);
        }
        finally {RenderTexture.active=old;Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(lamp);Object.DestroyImmediate(arrow);Object.DestroyImmediate(texture);Object.DestroyImmediate(target);}
    }
}
