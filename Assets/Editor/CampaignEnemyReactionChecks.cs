using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using GeoSniper;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CampaignEnemyReactionChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly StringBuilder Report = new StringBuilder();
    static readonly Vector3 Base = new Vector3(20000, 0, 20000);
    static void Set(object owner, string name, object value)
    {
        var field = owner.GetType().GetField(name, Private) ?? owner.GetType().GetField("<"+name+">k__BackingField", Private);
        if (field == null) throw new Exception("Missing field: " + name);
        field.SetValue(owner, value);
    }
    static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
    static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Private).Invoke(owner, args);
    static void Check(bool okay, string message)
    {
        if (!okay) throw new Exception(message);
        Report.AppendLine("PASS: " + message);
    }
    static void Ready(EnemyBot bot, bool visible = false)
    {
        bot.Actor.Initialize(100); bot.currentState=AIState.Combat; bot.currentRole=CombatRole.Assault;
        bot.transform.rotation=Quaternion.identity; bot.suppressionTimer=0; bot.isCounterSniper=false;
        Set(bot,"playerVisible",visible); Set(bot,"hadVisualContact",false); Set(bot,"aimKnowledgeReady",false);
        Set(bot,"staggerTimer",0f); Set(bot,"acquisitionRemaining",0f); Set(bot,"nextThreatReaction",-1f);
        Set(bot,"lastSeenPosition",Base+Vector3.forward*20); Set(bot,"fireCooldown",100f);
        Set(bot,"combatPause",.5f); Set(bot,"defensiveMoveCooldown",0f); Set(bot,"magazineRounds",18);
        Set(bot,"enemyReloadRemaining",0f); Call(bot,"ResetPlayerTactics");
    }
    static bool Bullet(EnemyBot bot, Vector3 origin, Vector3 aim, out Vector3 impact)
    {
        object[] args={origin,(aim-origin).normalized,100f,10f,Vector3.zero};
        bool hit=(bool)Call(bot,"ResolveEnemyBullet",args); impact=(Vector3)args[4]; return hit;
    }

    [MenuItem("Geo Sniper/Validate Campaign Enemy Reactions")]
    public static void RunAll()
    {
        RunPerception();CampaignAnimationChecks.Run();DuelVisualChecks.RunAll();
    }
    public static void RunPerception()
    {
        string project=Application.dataPath.Replace('\\','/');
        if (!project.Contains("/.utmp/duel-unity/") && !project.Contains("/.utmp/review-unity/"))
            throw new Exception("Use the isolated enemy verification project");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EnemyBot.AllBots.Clear();
        typeof(EnemyBot).GetField("lastRadioChirpTime",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,Time.time);
        Report.Clear(); Directory.CreateDirectory("Logs");
        var root=new GameObject("Campaign reaction verification");
        try
        {
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.SetParent(root.transform);
            floor.transform.position=Base+Vector3.down*.5f; floor.transform.localScale=new Vector3(100,1,100);
            var player=new GameObject("Player");player.transform.SetParent(root.transform);player.transform.position=Base+Vector3.forward*30;
            var health=player.AddComponent<CombatActor>();health.Initialize(100);
            var capsule=player.AddComponent<CapsuleCollider>();capsule.height=1.8f;capsule.center=Vector3.up*.9f;capsule.radius=.32f;
            var owner=new GameObject("Guard");owner.transform.SetParent(root.transform);owner.transform.position=Base;
            var bot=owner.AddComponent<EnemyBot>();bot.Initialize(player.transform,0);Ready(bot);
            // Normal MonoBehaviour OnEnable does not run in this edit-mode physics fixture.
            if(!EnemyBot.AllBots.Contains(bot))EnemyBot.AllBots.Add(bot);
            Physics.SyncTransforms();
            var muzzle=Base+Vector3.up*1.35f+Vector3.forward*.45f;
            var chest=player.transform.position+Vector3.up*1.1f;
            Check(Bullet(bot,muzzle,chest,out var impact) && health.Health==90,"A bullet through the player collider causes damage");
            Check(Vector3.Distance(impact,chest)<.5f,"Tracer impact ends at the struck player");
            health.Initialize(100);
            Check(!Bullet(bot,muzzle,chest+Vector3.right*4,out impact) && health.Health==100,"A missed ray cannot apply random health damage");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform);
            wall.transform.position=Base+new Vector3(0,1.5f,15);wall.transform.localScale=new Vector3(10,3,1);
            Physics.SyncTransforms();
            Check(!Bullet(bot,muzzle,chest,out impact) && health.Health==100,"Intervening cover blocks enemy bullets");
            Check(Mathf.Abs(impact.z-(Base.z+14.5f))<.02f,"A blocked tracer ends on cover");
            Ready(bot,true);Set(bot,"nextVisionCheck",0f);Call(bot,"Update");
            Check(!Get<bool>(bot,"playerVisible"),"Solid cover blocks enemy vision despite stale visible state");
            var remembered=Get<Vector3>(bot,"lastSeenPosition");
            player.transform.position=Base+new Vector3(4,0,32);Set(bot,"nextVisionCheck",0f);Call(bot,"Update");
            Check(Get<Vector3>(bot,"lastSeenPosition")==remembered,"Moving behind cover does not update the enemy's remembered position");
            Set(bot,"playerVisible",true);Set(bot,"lastSeenPosition",remembered);
            var source=Base+new Vector3(-8,1.55f,25);
            DamageSystem.ProcessHit(owner.GetComponent<CharacterController>(),8,Base+Vector3.up*1.1f,sourcePosition:source);
            Check(bot.Actor.LastDamageSourcePos==source,"Campaign damage records the shot source instead of the victim hit point");
            Check(Get<Vector3>(bot,"lastSeenPosition")!=player.transform.position && Vector3.Distance(Get<Vector3>(bot,"lastSeenPosition"),source)<6,"Damage behind cover supplies rough threat information");
            Check(bot.HitReactionStrength>0 && bot.StaggerProgress>0,"Taking damage produces a visible, bounded hit reaction");
            Check(!Get<bool>(bot,"playerVisible") && bot.currentState==AIState.TakeCover,"Damage refreshes stale visibility before choosing cover behind a wall");
            Check(!(bool)Call(bot,"CanShootObservedTarget",12f),"A staggered guard cannot fire immediately after being hit");
            wall.SetActive(false);player.transform.position=Base+Vector3.forward*30;Physics.SyncTransforms();
            Ready(bot);player.transform.position=Base+Vector3.forward*6;bot.currentState=AIState.Patrol;bot.ObservationPatrol=true;bot.detectionProgress=1;
            Physics.SyncTransforms();Call(bot,"Update");
            Check(!bot.ObservationPatrol && bot.currentState==AIState.Combat && Get<bool>(bot,"playerVisible"),"Meeting guard abandons observation patrol for an exposed close player");
            Check(Get<float>(bot,"fireCooldown")<1.2f,"Close detection discards the long spawn firing cooldown");
            Ready(bot,true);bot.Actor.Damage(5,player.transform.position);
            Check(bot.currentState==AIState.Combat && bot.StaggerProgress>0,"Healthy exposed guard flinches and changes angle instead of always running to cover");
            Ready(bot,true);bot.Actor.Damage(55,player.transform.position);
            Check(bot.currentState==AIState.TakeCover,"Seriously wounded guard prioritises cover");
            Ready(bot);bot.speed=0;bot.currentState=AIState.Combat;
            float responsiveSpeed=(float)typeof(EnemyBot).GetProperty("ResponsiveMoveSpeed",Private).GetValue(bot);
            Check(responsiveSpeed>=2.6f,"Stationary mission sentry has a responsive combat movement speed");
            Set(bot,"isHobbled",true);bot.speed=.8f;
            Check((float)typeof(EnemyBot).GetProperty("ResponsiveMoveSpeed",Private).GetValue(bot)==.8f,"Combat speed preserves the slowdown from a leg wound");
            Set(bot,"isHobbled",false);bot.speed=2.5f;
            wall.SetActive(true);wall.transform.position=Base+new Vector3(0,1.5f,3);Physics.SyncTransforms();
            Ready(bot);bot.currentState=AIState.Patrol;bot.ObservationPatrol=true;bot.detectionProgress=0;
            Call(bot,"Update");
            Check(bot.ObservationPatrol && !(bool)Call(bot,"ClosePlayerVisible",12f),"Close proximity behind a wall does not expose the player");
            wall.SetActive(false);Physics.SyncTransforms();Ready(bot);bot.ObservationPatrol=true;bot.PassiveTrainingTarget=true;
            Call(bot,"Update");Check(bot.ObservationPatrol && bot.currentState==AIState.Patrol,"Firing range targets retain their passive training behaviour");
            bot.ObservationPatrol=false;bot.PassiveTrainingTarget=false;player.transform.position=Base+Vector3.forward*30;Physics.SyncTransforms();
            Ready(bot);Call(bot,"UpdateAimKnowledge",true,player.transform.position+Vector3.up*1.55f,30f,.016f);Set(bot,"playerVisible",true);
            Check(Get<float>(bot,"acquisitionRemaining")>0,"Fresh visual contact requires aiming time");
            Call(bot,"TickReaction",2f);
            Check((bool)Call(bot,"CanShootObservedTarget",12f),"A settled guard can fire after acquiring a visible target");
            Ready(bot);EnemyBot.NotifyMissedShot(Base+new Vector3(14,1.1f,-10),Base+new Vector3(14,1.1f,10),false);
            Check(bot.suppressionTimer==0,"Distant bullet flights do not suppress unrelated enemies");
            EnemyBot.NotifyMissedShot(Base+new Vector3(1,1.1f,-10),Base+new Vector3(1,1.1f,10),false);
            Check(bot.suppressionTimer>0 && Get<float>(bot,"acquisitionRemaining")>0,"A real nearby bullet flight interrupts aim and prompts defense");
            Ready(bot);EnemyBot.NotifyMissedShot(Base+new Vector3(1,1.1f,5),Base+new Vector3(1,1.1f,15),false);
            Check(bot.suppressionTimer==0,"The extension behind a bullet's finite flight cannot suppress a guard");
            wall.SetActive(true);wall.transform.position=Base+new Vector3(.5f,1.5f,0);wall.transform.localScale=new Vector3(.2f,3,40);Physics.SyncTransforms();
            Ready(bot);EnemyBot.NotifyMissedShot(Base+new Vector3(1,1.1f,-10),Base+new Vector3(1,1.1f,10),false);
            Check(bot.suppressionTimer==0,"A wall shields a guard from a near pass on its other side");
            Ready(bot);EnemyBot.NotifyMissedShot(Base+new Vector3(-10,1.1f,0),Base+new Vector3(.4f,1.1f,0));
            Check(bot.suppressionTimer>0,"An exposed nearby wall impact triggers a defensive reaction");
            Ready(bot);EnemyBot.NotifyMissedShot(Base+new Vector3(10,1.1f,0),Base+new Vector3(.6f,1.1f,0));
            Check(bot.suppressionTimer==0,"A nearby impact on the far face remains blocked by the wall");
            wall.SetActive(false);Physics.SyncTransforms();Ready(bot,true);
            Set(bot,"staggerTimer",.2f);var burst=(IEnumerator)Call(bot,"ExecuteBurst",chest,30f);
            Check(!burst.MoveNext() && Get<int>(bot,"magazineRounds")==18,"An interrupted burst cannot consume ammunition or fire");
            (burst as IDisposable)?.Dispose();Ready(bot,true);bot.suppressionTimer=1;
            burst=(IEnumerator)Call(bot,"ExecuteBurst",chest,30f);
            Check(!burst.MoveNext() && health.Health==100,"Suppression stops burst damage");(burst as IDisposable)?.Dispose();
            Ready(bot,true);health.Damage(100);burst=(IEnumerator)Call(bot,"ExecuteBurst",chest,30f);
            Check(!burst.MoveNext() && Get<int>(bot,"magazineRounds")==18,"Enemy bursts stop when their target is already dead");
            (burst as IDisposable)?.Dispose();health.Initialize(100);
            Ready(bot);var warning=new GameObject("Sniper warning");warning.transform.SetParent(owner.transform);
            Set(bot,"sniperLaser",warning.AddComponent<LineRenderer>());Get<LineRenderer>(bot,"sniperLaser").enabled=true;
            Set(bot,"sniperAimTimer",1.5f);bot.Actor.Damage(5,source);
            Check(!Get<LineRenderer>(bot,"sniperLaser").enabled && bot.CounterSniperLockProgress==0,"A hit immediately cancels the sniper warning and lock");
            Ready(bot,true);bot.isCounterSniper=true;Set(bot,"aimKnowledgeReady",true);
            // Reuse a collider-free warning prop; runtime creates it with deferred Destroy.
            var glint=new GameObject("Sniper glint");glint.transform.SetParent(owner.transform);Set(bot,"scopeGlintObj",glint);
            Set(bot,"sniperAimTimer",2.05f);Set(bot,"fireCooldown",1.2f);Set(bot,"sniperAimCommitted",false);
            Call(bot,"UpdateCounterSniper",true,Vector3.forward*30,30f,Base+Vector3.up*1.55f,chest);
            Check(!Get<bool>(bot,"sniperAimCommitted"),"A sniper keeps tracking while its firing cooldown exceeds the final dodge window");
            Set(bot,"fireCooldown",.15f);
            Call(bot,"UpdateCounterSniper",true,Vector3.forward*30,30f,Base+Vector3.up*1.55f,chest);
            Check(Get<bool>(bot,"sniperAimCommitted"),"A sniper commits its shot only inside the final dodge window");
            Set(bot,"sniperAimCommitted",false);
            capsule.height=1f;capsule.center=Vector3.up*.5f;Physics.SyncTransforms();
            Set(bot,"observedAim",player.transform.position+Vector3.up*.85f);Call(bot,"ExecuteCounterSniperShot",chest,30f);
            Check(health.Health<100,"Crouching before a sniper aims does not grant permanent immunity");
            health.Initialize(100);Set(bot,"sniperAimCommitted",true);Set(bot,"committedSniperAim",player.transform.position+Vector3.up*1.55f);
            Call(bot,"ExecuteCounterSniperShot",chest,30f);
            Check(health.Health==100,"Ducking beneath an already committed shot avoids its physical bullet ray");
            Ready(bot,true);capsule.height=1.8f;capsule.center=Vector3.up*.9f;Physics.SyncTransforms();
            Check((bool)Call(bot,"IsTacticalDestinationSafe",Base+Vector3.right*3,2.5f),"A supported clear tactical step is accepted");
            Check(!(bool)Call(bot,"IsTacticalDestinationSafe",Base+Vector3.right*60,2.5f),"Tactical movement refuses unsupported ground");
            Check((bool)Call(bot,"TryChoosePlayerCombatStep",30f),"Visible combat selects a short safe movement lane");
            Set(bot,"underFireUntil",-1f);Set(bot,"combatPause",1.5f);
            Call(bot,"UpdatePlayerTactics",true,30f,.1f);
            Check(Get<float>(bot,"combatPause")>1f,"Unpressured guard holds a firing angle instead of continuously strafing");
            Set(bot,"underFireUntil",Time.time+3f);Set(bot,"combatPause",1.5f);
            Call(bot,"UpdatePlayerTactics",true,30f,.1f);
            Check(Get<float>(bot,"combatPause")<=.45f,"Incoming fire shortens the firing-angle dwell");
            Call(bot,"UpdatePlayerTactics",false,30f,.016f);
            Check(!Get<bool>(bot,"combatTargetReady") && !bot.IsFlanking,"Lost visual contact cancels movement toward a live target");
            var feedbackObject=new GameObject("Feedback verification");feedbackObject.transform.SetParent(root.transform);
            var presentation=feedbackObject.AddComponent<SniperPresentation>();
            presentation.ConfirmKill();presentation.ConfirmHit();
            Check(Get<bool>(presentation,"hitMarkerKill"),"Same-frame damage callback preserves a lethal hit's gold marker");
            Set(presentation,"lastKillFrame",-1);presentation.ConfirmHit();
            Check(!Get<bool>(presentation,"hitMarkerKill"),"A later body hit is white even while the previous kill banner remains");
            var feedbackCamera=new GameObject("Impact camera");feedbackCamera.transform.SetParent(root.transform);feedbackCamera.tag="MainCamera";feedbackCamera.AddComponent<Camera>();
            CombatImpactAudio.Play(false);var impactFeedback=feedbackCamera.GetComponent<CombatImpactAudio>();
            var bodyClip=Get<AudioClip>(impactFeedback,"body");var headClip=Get<AudioClip>(impactFeedback,"head");
            CombatImpactAudio.Play(true);CombatImpactAudio.Play(false);
            Check(Get<AudioClip>(impactFeedback,"body")==bodyClip && Get<AudioClip>(impactFeedback,"head")==headClip && feedbackCamera.GetComponents<AudioSource>().Length==1,"Repeated hits reuse two owned clips and one isolated impact source");
            Ready(bot,true);
            var rifle=GameObject.CreatePrimitive(PrimitiveType.Cube);rifle.name="Enemy tactical rifle";rifle.transform.SetParent(owner.transform,false);rifle.transform.localPosition=new Vector3(.2f,1.1f,.3f);rifle.transform.localScale=new Vector3(.12f,.15f,.9f);
            var originalParent=rifle.transform.parent;var originalPosition=rifle.transform.localPosition;
            Set(bot,"gun",rifle.transform);bot.Disarm();
            Check(bot.isDisarmed && !bot.IsAiming && rifle.transform.parent==null,"Disarm removes rifle and immediately stops aiming");
            Check(!(bool)Call(bot,"CanShootObservedTarget",12f),"An unarmed guard cannot fire");
            Set(bot,"underFireUntil",Time.time+3);Set(bot,"disarmedAt",Time.time-5);Call(bot,"UpdateDisarmed",2f);
            Check(bot.isDisarmed && !bot.RecoveringWeapon,"Incoming fire prevents a weapon pickup");
            Call(bot,"RecoverDroppedWeapon");
            Check(!bot.isDisarmed && rifle.transform.parent==originalParent && rifle.transform.localPosition==originalPosition,"Recovered rifle restores its original attachment instead of its ground pose");
            Check(Get<float>(bot,"acquisitionRemaining")>0 && Get<float>(bot,"fireCooldown")>=.65f,"Weapon recovery requires settling before firing");
            Check((AudioClip)typeof(EnemyBot).GetMethod("BuildEnemyShotClip",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null)==Resources.Load<AudioClip>("Audio/HeavyRifleShot"),"Enemy gunfire uses the recorded rifle report");
            Call(bot,"PlayEnemyReport",true);var reportSource=Get<AudioSource>(bot,"enemyAudio");
            Check(reportSource.dopplerLevel==0 && reportSource.pitch>.9f && reportSource.pitch<1 && reportSource.rolloffMode==AudioRolloffMode.Logarithmic,"Sniper report retains its intended pitch and natural distance rolloff");
            Call(bot,"PlayEnemyReport",false);Check(reportSource.pitch>=.98f && reportSource.pitch<=1.02f,"Normal enemy report has bounded pitch variation");
            Ready(bot,true);
            var sharedAlerts=bot.Alerts;
            for(int i=1;i<=3;i++)
            {
                var mate=new GameObject("Squadmate "+i);mate.transform.SetParent(root.transform);mate.transform.position=Base+Vector3.right*(i*8);
                var teammate=mate.AddComponent<EnemyBot>();teammate.Initialize(player.transform,i);Ready(teammate,true);teammate.Alerts=sharedAlerts;
                if(!EnemyBot.AllBots.Contains(teammate))EnemyBot.AllBots.Add(teammate);
            }
            Call(bot,"UpdateSquadRole",2f);int supports=0,flankers=0;
            foreach(var member in EnemyBot.AllBots){if(member.currentRole==CombatRole.Suppressor)supports++;if(member.currentRole==CombatRole.Flanker)flankers++;}
            Check(supports==1 && flankers==1,"A squad assigns one covering guard and one flanker");
            var ally=EnemyBot.AllBots[1];
            Check(!(bool)Call(bot,"IsTacticalDestinationSafe",ally.transform.position,2.5f),"Tactical lanes avoid crowding another guard");
            Ready(bot);bot.currentState=AIState.TakeCover;bot.coverSubState=CoverSubState.Sprinting;
            bot.coverTargetPos=Base+Vector3.left*4;
            DamageSystem.ProcessHit(owner.GetComponent<CharacterController>(),40,Base+Vector3.up*.5f,sourcePosition:source);
            Check(bot.isHobbled && bot.coverSubState==CoverSubState.Sprinting && bot.coverTargetPos==Base+Vector3.left*4,
                "A leg wound slows the guard while preserving its sprint toward cover");
            Ready(bot);bot.currentState=AIState.TakeCover;bot.coverSubState=CoverSubState.Crouching;
            bot.Damage(5);
            Check(bot.currentState==AIState.TakeCover && bot.HitReactionStrength>0,"Direct damage preserves the defensive state established by its hit reaction");
            Ready(bot,true);bot.enabled=false;burst=(IEnumerator)Call(bot,"ExecuteBurst",chest,30f);
            Check(!burst.MoveNext() && Get<int>(bot,"magazineRounds")==18,"A disabled enemy component cannot execute a scheduled shot");
            (burst as IDisposable)?.Dispose();bot.enabled=true;Ready(bot,true);Physics.SyncTransforms();
            burst=(IEnumerator)Call(bot,"ExecuteBurst",chest,30f);
            Check(burst.MoveNext(),"A ready guard starts an actual physical bullet burst");
            int remaining=Get<int>(bot,"magazineRounds");Call(bot,"OnDisable");
            Check(!burst.MoveNext() && Get<int>(bot,"magazineRounds")==remaining,"Disabling and reactivating an enemy cancels the old pending burst");
            (burst as IDisposable)?.Dispose();
        }
        catch(Exception error)
        {
            Report.AppendLine("FAIL: "+error);throw;
        }
        finally
        {
            Object.DestroyImmediate(root);EnemyBot.AllBots.Clear();File.WriteAllText("Logs/CampaignEnemyReactionChecks.txt",Report.ToString());
        }
        Debug.Log("Campaign enemy reaction checks passed");
    }
}
