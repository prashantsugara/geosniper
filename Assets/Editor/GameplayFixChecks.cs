using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using GeoSniper;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class GameplayFixChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly StringBuilder report=new StringBuilder();
    static void Require(bool condition,string message) {if(!condition) throw new Exception(message);}
    static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Private).SetValue(obj,value);
    static T Get<T>(object obj,string name)=>(T)obj.GetType().GetField(name,Private).GetValue(obj);
    static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Private).Invoke(obj,args);
    public static void Run()
    {
        if(!Application.isBatchMode) throw new Exception("Run in the isolated review project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        Directory.CreateDirectory("Logs");report.Clear();int failures=0;
        foreach(var check in new Action[]{ArmyAnimationChecks.Run,Loadout,Occlusion,Deployment,Stance,Knife,Poses,LobbyAndFirstPerson})
        {
            try {check();report.AppendLine("PASS "+check.Method.Name);}
            catch(Exception ex) {failures++;report.AppendLine("FAIL "+check.Method.Name+": "+ex);}
        }
        File.WriteAllText("Logs/GameplayFixChecks.txt",report.ToString());
        if(failures>0) throw new Exception(failures+" regression groups failed; see GameplayFixChecks.txt");
    }
    static void Loadout()
    {
        var root=new GameObject("Loadout checks");
        var mission=root.AddComponent<UrbanCombatMission>();
        var weapon=root.AddComponent<SniperPresentation>();
        var ballistic=root.AddComponent<BallisticsSystem>();
        weapon.Config=ScriptableObject.CreateInstance<WeaponConfig>();
        ballistic.config=ScriptableObject.CreateInstance<BallisticsConfig>();
        Set(mission,"weapon",weapon);Set(mission,"ballistics",ballistic);
        weapon.CurrentWeaponIndex=1;Set(mission,"ammo",7);
        Call(mission,"SelectWeapon",1);
        Require(Get<int>(mission,"ammo")==7,"Reselect refilled magazine");
        Call(mission,"SelectWeapon",2);
        float expected=45f*(1+Mathf.Clamp(PlayerPrefs.GetInt("GeoSniper.WpnDmgLvl_2",0),0,2)*.2f);
        Require(Mathf.Abs(ballistic.config.baseDamage-expected)<.01f && ballistic.config.noiseRadius==25,"Weapon stats did not switch");
        Set(mission,"ammo",4);Call(mission,"SelectWeapon",1);
        Require(Get<int>(mission,"ammo")==7,"Switch discarded prior magazine");
        Call(mission,"SelectWeapon",2);Require(Get<int>(mission,"ammo")==4,"Switch refilled second magazine");
        Set(mission,"reloadTimer",1f);Call(mission,"SelectWeapon",0);
        Require(weapon.CurrentWeaponIndex==2,"Switch bypassed reload");
        Set(mission,"weapon",null);Set(mission,"ballistics",null);
        Object.DestroyImmediate(weapon.Config);Object.DestroyImmediate(ballistic.config);Object.DestroyImmediate(root);
    }
    static void Occlusion()
    {
        var root=new GameObject("Cover target");root.transform.position=new Vector3(12000,0,12000);
        var enemy=root.AddComponent<EnemyBot>();enemy.Initialize(null,0);
        root.AddComponent<EnemyHitboxes>().Initialize(root.transform);
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name="Concrete panel";wall.transform.localScale=new Vector3(3,3,.1f);
        wall.transform.position=root.transform.position+new Vector3(0,1,-.5f);
        var ray=new Ray(root.transform.position+new Vector3(0,1,-10),Vector3.forward);
        Require(SniperHitQuery.TryCastAimAssist(ray,new[]{enemy},null,null,out var hit,20) && hit.Collider==wall.GetComponent<Collider>(),"Thin solid cover was bypassed");
        Require(!SniperHitQuery.TryPenetrate(hit,Vector3.forward,out _),"Concrete panel penetrable");
        wall.name="Window glass";
        Require(SniperHitQuery.TryCast(ray,new[]{enemy},null,null,out hit,20) && hit.Collider==wall.GetComponent<Collider>(),"Glazing skipped before damage attenuation");
        Require(SniperHitQuery.TryPenetrate(hit,Vector3.forward,out var exit) && exit.z>hit.Point.z,"Thin glass did not yield measured exit");
        wall.transform.localScale=new Vector3(3,3,1);Physics.SyncTransforms();
        SniperHitQuery.TryCast(ray,new[]{enemy},null,null,out hit,20);
        Require(!SniperHitQuery.TryPenetrate(hit,Vector3.forward,out _),"Thick glazing bypassed thickness budget");
        Require(!root.GetComponentsInChildren<Collider>().Any(c=>c.isTrigger && c.enabled),"Query left hit volumes active");
        Object.DestroyImmediate(wall);Object.DestroyImmediate(root);
    }
    static void Deployment()
    {
        var root=new GameObject("Deployment check");root.SetActive(false);
        var game=root.AddComponent<GeoSniperGame>();
        Set(game,"isPvPDuel",true);Set(game,"stageIndexToStart",9);Set(game,"campaignNodeToStart",8);
        Call(game,"PrepareDeployment",2);
        Require(!Get<bool>(game,"isPvPDuel") && Get<int>(game,"stageIndexToStart")==-1 && Get<int>(game,"campaignNodeToStart")==-1 && Get<bool>(game,"rangeToStart"),"GPS range inherited mission state");
        Call(game,"PrepareDeployment",1);Require(!Get<bool>(game,"rangeToStart"),"Range leaked to world sector");
        Object.DestroyImmediate(root);
    }
    static void Stance()
    {
        var root=new GameObject("Stance check");var camera=new GameObject("Stance camera").AddComponent<Camera>();
        var player=root.AddComponent<UrbanPlayer>();player.Initialize(camera);
        player.SetCrouching(true);Require(player.IsCrouching && player.Controls.IsCrouching,"Crouch not synchronized");
        player.SetCrouching(false);Require(!player.IsCrouching && !player.Controls.IsCrouching,"Stand not synchronized");
        player.Controls.ShowHoldBreath=true;
        for(int i=0;i<3;i++) Require(!player.Controls.CanSelectWeaponSlot(i),"Hidden scoped weapon slot remains active");
        Object.DestroyImmediate(root);
    }
    static void Poses()
    {
        foreach(var prefab in ModelLibrary.Load("Enemies"))
        {
            var root=new GameObject("Pose "+prefab.name);
            var player=new GameObject("Aim target");player.transform.position=new Vector3(0,0,30);
            var visual=ImportedVisual.CreateEnemy(prefab,root.transform);
            var bot=root.AddComponent<EnemyBot>();bot.Initialize(player.transform,0);
            var driver=root.AddComponent<ArmyAnimation>();Require(driver.Initialize(bot,visual.transform),"Animation binding failed");
            var mission=root.AddComponent<UrbanCombatMission>();
            Call(mission,"BuildEnemyWeapon",root,visual.transform,bot);
            var anchor=root.GetComponentInChildren<EnemyWeaponAnchor>();Require(anchor!=null,"Missing weapon anchor");
            var animation=visual.GetComponentInChildren<Animation>();
            float worstRight=0,worstLeft=0;
            foreach(string clip in new[]{"idle","walk","run","crouch_idle","fire","hit"})
            foreach(float fraction in new[]{0f,.3f,.7f})
            foreach(float elevation in new[]{-10f,0f,10f})
            {
                animation.Play(clip);animation[clip].time=animation[clip].length*fraction;animation.Sample();
                player.transform.position=new Vector3(0,elevation,30);
                bot.currentState=clip=="idle" || clip=="walk" || clip=="run" ? AIState.Patrol : AIState.Combat;
                Call(driver,"LateUpdate");Call(bot,"LateUpdate");Call(anchor,"LateUpdate");
                float right=Vector3.Distance(anchor.transform.position,anchor.rightHand.TransformPoint(WeaponGripPose.PalmLocal(anchor.rightHand)));
                float left=Vector3.Distance(anchor.transform.Find("WeaponSupport").position,anchor.leftHand.TransformPoint(WeaponGripPose.PalmLocal(anchor.leftHand)));
                worstRight=Mathf.Max(worstRight,right);worstLeft=Mathf.Max(worstLeft,left);
                if(elevation==0 && fraction==.3f) report.AppendLine(prefab.name+" "+clip+" gaps="+right+","+left+" hand="+anchor.rightHand.name+" upper="+Vector3.Distance(anchor.rightHand.parent.parent.position,anchor.rightHand.parent.position)+" lower="+Vector3.Distance(anchor.rightHand.parent.position,anchor.rightHand.position)+" reach="+Vector3.Distance(anchor.rightHand.parent.parent.position,anchor.transform.position)+" palm="+WeaponGripPose.PalmLocal(anchor.rightHand)+" handscale="+anchor.rightHand.lossyScale);
                if(fraction==.3f && elevation==0) Render(root,prefab.name+"-"+clip);
            }
            report.AppendLine(prefab.name+" worst grip gaps right="+worstRight.ToString("F4")+" left="+worstLeft.ToString("F4"));
            bot.Disarm();Require(!anchor.enabled && anchor.transform.parent==null,"Disarmed gun retains pose ownership");
            Object.DestroyImmediate(anchor.gameObject);
            Object.DestroyImmediate(root);Object.DestroyImmediate(player);
            Require(worstRight<.025f && worstLeft<.05f,"Hand contact exceeds tolerance for "+prefab.name);
        }
    }
    static void Knife()
    {
        var root=new GameObject("Knife check");var mission=root.AddComponent<UrbanCombatMission>();
        var camera=new GameObject("Knife camera").AddComponent<Camera>();
        var player=new GameObject("Knife player").AddComponent<UrbanPlayer>();player.Initialize(camera);player.Place(new Vector3(15000,0,15000));player.Face(player.transform.position+Vector3.forward);
        Set(mission,"player",player);Set(mission,"cameraView",camera);
        var target=new GameObject("Knife target");target.transform.position=player.transform.position+Vector3.forward*2;
        var bot=target.AddComponent<EnemyBot>();bot.Initialize(player.transform,0);
        Physics.SyncTransforms();Require(mission.CanKnifeKill(bot),"Clear reachable target rejected");
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=player.transform.position+new Vector3(0,1,1);wall.transform.localScale=new Vector3(3,3,.1f);
        Physics.SyncTransforms();Require(!mission.CanKnifeKill(bot),"Knife target reachable through solid wall");
        Object.DestroyImmediate(wall);target.transform.position=player.transform.position-Vector3.forward*2;
        Physics.SyncTransforms();Require(!mission.CanKnifeKill(bot),"Knife target behind player accepted");
        Set(mission,"player",null);Set(mission,"cameraView",null);Object.DestroyImmediate(player.gameObject);Object.DestroyImmediate(target);Object.DestroyImmediate(root);
    }
    static void LobbyAndFirstPerson()
    {
        string[] names={"Barrett50","M24Tactical","MK12SPR"};
        for(int index=0;index<3;index++)
        {
            var root=new GameObject("Lobby grip check");
            var soldier=ImportedVisual.CreateEnemy(Resources.Load<GameObject>("Models/Enemies/swat"),root.transform);
            var holder=new GameObject("Lobby rifle");holder.transform.SetParent(root.transform,false);
            var model=Object.Instantiate(Resources.Load<GameObject>("Models/Weapons/"+names[index]),holder.transform,false);
            Require(WeaponGeometry.Configure(model.transform,holder.transform,index),"Invalid lobby weapon geometry");
            var pose=soldier.AddComponent<LobbySniperPose>();pose.Initialize(holder);
            var anchor=holder.GetComponent<EnemyWeaponAnchor>();var animation=soldier.GetComponentInChildren<Animation>();
            float worst=0;
            foreach(float yaw in new[]{0f,90f,180f,270f})
            {
                soldier.transform.rotation=Quaternion.Euler(0,yaw,0);animation.Sample();Call(anchor,"LateUpdate");
                worst=Mathf.Max(worst,Vector3.Distance(anchor.leftHand.TransformPoint(WeaponGripPose.PalmLocal(anchor.leftHand)),holder.transform.Find("WeaponSupport").position));
            }
            report.AppendLine(names[index]+" lobby support gap="+worst.ToString("F4"));
            Require(worst<.05f,"Lobby support hand out of reach");
            Object.DestroyImmediate(root);

            var camera=new GameObject("FPS check camera").AddComponent<Camera>();
            var controller=new GameObject("FPS check presentation");var presentation=controller.AddComponent<SniperPresentation>();presentation.CurrentWeaponIndex=index;presentation.Initialize(camera);
            var rifle=Get<Transform>(presentation,"rifle");
            var firstPerson=rifle.GetComponentInChildren<FirstPersonGripPose>();Require(firstPerson!=null,"Missing first-person pose");firstPerson.Apply();
            var left=WeaponGripPose.Bone(firstPerson.transform,"lwrist03");var right=WeaponGripPose.Bone(firstPerson.transform,"rwrist027");
            Require(left!=null && right!=null && left.lossyScale.sqrMagnitude>.00001f,"First-person support arm hidden or missing");
            float gap=Vector3.Distance(left.TransformPoint(WeaponGripPose.PalmLocal(left)),rifle.Find("WeaponSupport").position);
            float rightGap=Vector3.Distance(right.TransformPoint(WeaponGripPose.PalmLocal(right)),rifle.position);
            report.AppendLine(names[index]+" FPS grip gaps="+rightGap.ToString("F4")+","+gap.ToString("F4"));
            Require(gap<.05f && rightGap<.025f,"First-person hands not aligned");
            Object.DestroyImmediate(controller);Object.DestroyImmediate(camera.gameObject);
        }
    }
    static void Render(GameObject root,string name)
    {
        var cam=new GameObject("Pose capture").AddComponent<Camera>();cam.transform.position=new Vector3(2.5f,1.65f,3.3f);cam.transform.LookAt(new Vector3(0,1.1f,0));
        cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.15f,.17f,.2f);cam.fieldOfView=33;
        var light=new GameObject("Pose light").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(35,-40,0);light.intensity=1.6f;
        RenderSettings.ambientLight=Color.gray;
        var rt=new RenderTexture(700,700,24);cam.targetTexture=rt;cam.Render();
        var previous=RenderTexture.active;RenderTexture.active=rt;
        var image=new Texture2D(700,700,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,700,700),0,0);image.Apply();
        File.WriteAllBytes("Logs/Pose-"+name+".png",image.EncodeToPNG());RenderTexture.active=previous;
        Object.DestroyImmediate(image);cam.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(cam.gameObject);Object.DestroyImmediate(light.gameObject);
    }
}
