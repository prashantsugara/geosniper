using System;
using System.IO;
using System.Reflection;
using System.Text;
using GeoSniper;
using GeoSniper.Duel;
using UnityEditor;
using UnityEngine;
public static class DuelGameplayChecks
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Set(object obj,string name,object value)
    {
        var field=obj.GetType().GetField(name,Private) ?? obj.GetType().GetField("<"+name+">k__BackingField",Private);
        if(field==null) throw new Exception("Missing field "+name);
        field.SetValue(obj,value);
    }
    static T Get<T>(object obj,string name)
    {
        var field=obj.GetType().GetField(name,Private) ?? obj.GetType().GetField("<"+name+">k__BackingField",Private);
        if(field==null) throw new Exception("Missing field "+name);
        return (T)field.GetValue(obj);
    }
    static void Packet(SniperDuelManager manager,byte[] data)
    {
        using(var reader=new BinaryReader(new MemoryStream(data,2,data.Length-2)))
            typeof(SniperDuelManager).GetMethod("HandlePacketReceived",Private).Invoke(manager,new object[]{(DuelPacketType)data[1],reader});
    }
    static void Authority(SniperDuelManager manager, int round, Vector3 rivalPosition, bool crouched=false)
    {
        Set(manager,"roundId",round);Set(manager,"RoundSecondsRemaining",SniperDuelManager.RoundDuration);
        Set(manager,"remotePosition",rivalPosition);Set(manager,"remoteCrouched",crouched);
        typeof(SniperDuelManager).GetMethod("ResetShotAuthority",Private).Invoke(manager,null);
        var history=(DuelPoseHistory)typeof(SniperDuelManager).GetField("rivalHistory",Private).GetValue(manager);
        history.Record(Time.unscaledTime,rivalPosition,0,0,crouched);
    }
    [MenuItem("Geo Sniper/Validate Duel Gameplay")]
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/.utmp/duel-unity/")) throw new Exception("Use isolated duel verification project");
        var report=new StringBuilder();
        var root=new GameObject("Duel verification");
        try
        {
            var net=root.AddComponent<SniperDuelNetwork>();Set(net,"IsConnected",true);
            var manager=root.AddComponent<SniperDuelManager>();
            typeof(SniperDuelManager).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,manager);
            Set(manager,"network",net);Set(manager,"isHost",true);Set(manager,"roundId",1);Set(manager,"MatchState",DuelMatchState.InRound);
            var playerObj=new GameObject("Local player");playerObj.transform.SetParent(root.transform);
            var player=playerObj.AddComponent<UrbanPlayer>();var health=playerObj.AddComponent<CombatActor>();health.Initialize(100);Set(player,"Health",health);Set(manager,"localPlayer",player);
            var rivalObj=new GameObject("Rival");rivalObj.transform.SetParent(root.transform);rivalObj.transform.position=new Vector3(0,0,30);
            var rival=rivalObj.AddComponent<DuelOpponentAvatar>();
            typeof(DuelOpponentAvatar).GetMethod("BuildHitboxes",Private).Invoke(rival,null);
            Set(manager,"Opponent",rival);
            var head=rivalObj.transform.Find("Hitbox_Head").GetComponent<SphereCollider>();
            Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception(message);report.AppendLine("PASS: "+message);};
            var feature=new MapFeature {Kind="building",Name="Chosen roof",Height=22f};
            feature.Points.Add(new Vector2(-10,8));feature.Points.Add(new Vector2(12,8));feature.Points.Add(new Vector2(12,30));
            feature.WaterRings.Add(new System.Collections.Generic.List<Vector2>{new Vector2(1,2),new Vector2(3,4)});
            feature.Details["building:levels"]=7;
            var snapshot=new DuelMapSnapshot {Location=new GameLocation {Latitude=-33.8688,Longitude=151.2093,Label="HOST CHOSEN PLACE",Source="selected"},
                Source="Map cache",Features=new System.Collections.Generic.List<MapFeature>{feature},Heights=new float[4225],ElevationAvailable=true};
            snapshot.Heights[123]=34.125f;
            var decoded=DuelMapSnapshot.Decode(snapshot.Encode());
            check(decoded.Location.Latitude==snapshot.Location.Latitude && decoded.Location.Label==snapshot.Location.Label
                && decoded.Features[0].Height==22 && decoded.Features[0].Points[1].x==12 && decoded.Features[0].WaterRings[0][1].y==4
                && (int)decoded.Features[0].Details["building:levels"]==7 && decoded.Heights[123]==34.125f && decoded.ElevationAvailable,
                "Shared host location preserves map outlines, attributes, water rings and terrain heights");
            bool invalidMap=false;try {DuelMapSnapshot.Decode(new byte[]{1,2,3});}catch(System.IO.IOException){invalidMap=true;}
            check(invalidMap,"Invalid shared map rejected before world creation");
            check(rivalObj.GetComponent<CombatActor>()!=null && rivalObj.GetComponent<CapsuleCollider>()!=null && head!=null,"Production avatar hitboxes initialize without custom tags");
            Physics.SyncTransforms();
            check(SniperHitQuery.TryCast(new Ray(new Vector3(0,1.65f,0),Vector3.forward),null,player.transform,null,out var shot,100) && shot.Collider.GetComponentInParent<DuelOpponentAvatar>()==rival,"Shot query recognizes network avatar");
            var info=DamageSystem.ProcessHit(head,40,head.transform.position);
            check(info.isHeadshot && !rival.IsDead && manager.LocalScore==0,"Local collision cannot invent multiplayer damage");
            Authority(manager,1,rival.transform.position);
            manager.NotifyLocalGunshot(new Vector3(0,1.55f,0),Vector3.forward,1);
            check(rival.IsDead && manager.LocalScore==1 && manager.RivalScore==0 && manager.MatchState==DuelMatchState.RoundOver,"Host ray validates a headshot and awards one point");
            DamageSystem.ProcessHit(head,40,head.transform.position);
            check(manager.LocalScore==1,"Repeated lethal hit cannot score twice");
            rival.ApplyNetworkState(Vector3.zero,0,0,false,false,100);
            check(rival.IsDead,"Delayed healthy movement cannot resurrect a killed avatar");
            Packet(manager,DuelPacketCodec.SerializeStateSync(Vector3.one,0,0,false,false,100,0));
            check(rival.IsDead,"Movement from an earlier round cannot resurrect an avatar");
            Set(manager,"MatchState",DuelMatchState.InRound);Set(manager,"roundId",2);Set(manager,"isHost",false);Set(manager,"LocalScore",0);
            rival.Respawn(new Vector3(0,0,30),Quaternion.identity);
            DamageSystem.ProcessHit(head,40,head.transform.position);
            check(manager.LocalScore==0 && manager.MatchState==DuelMatchState.InRound,"Guest prediction cannot award a point");
            var result=DuelPacketCodec.SerializeRoundState(DuelMatchState.RoundOver,0,1,"GUEST",2);Packet(manager,result);Packet(manager,result);
            check(manager.LocalScore==1 && manager.RivalScore==0 && manager.MatchState==DuelMatchState.RoundOver,"Guest mirrors host result exactly once");
            Set(manager,"MatchState",DuelMatchState.InRound);Set(manager,"roundId",3);Set(manager,"isHost",true);Set(manager,"LocalScore",0);Set(manager,"RivalScore",0);
            health.Initialize(100);
            Packet(manager,DuelPacketCodec.SerializeHit(2,100,Vector3.zero,2));
            check(health.Health==100,"Old-round hit discarded");
            var forged=DuelPacketCodec.SerializeHit(2,100,Vector3.zero,3);Packet(manager,forged);
            check(health.Health==100,"Peer hit claims cannot damage the host");
            rival.Respawn(new Vector3(0,0,30),Quaternion.identity);
            Authority(manager,3,rival.transform.position);
            var lethal=DuelPacketCodec.SerializeFire(new Vector3(0,1.55f,30),Vector3.back,1,3,1,false,0);Packet(manager,lethal);Packet(manager,lethal);
            check(manager.LocalScore==0 && manager.RivalScore==1,"Host death awards guest exactly one point");
            health.Initialize(100);Set(manager,"MatchState",DuelMatchState.InRound);
            Packet(manager,DuelPacketCodec.SerializeHit(1,float.NaN,Vector3.zero,3));
            check(health.Health==100,"Non-finite damage discarded");
            var mission=root.AddComponent<UrbanCombatMission>();Set(mission,"player",player);
            mission.BeginPvPDuel(-1,true);
            check(mission.MultiplayerDuel && mission.DuelRivalPosition==Vector3.zero && root.GetComponentsInChildren<EnemyBot>().Length==0 && player.InputBlocked,"Multiplayer setup spawns no bot and blocks input until a real player is ready");
            var controls=playerObj.AddComponent<MobileCombatInput>();Set(player,"Controls",controls);
            var cameraObject=new GameObject("Duel view");cameraObject.transform.SetParent(player.transform);
            var camera=cameraObject.AddComponent<Camera>();
            var weapon=cameraObject.AddComponent<SniperPresentation>();
            var rifle=new GameObject("Update pose check").transform;rifle.SetParent(camera.transform,false);
            Set(weapon,"cam",camera);Set(weapon,"rifle",rifle);Set(mission,"weapon",weapon);Set(mission,"cameraView",camera);
            Set(mission,"reloadTimer",3f);Set(mission,"chamberTimer",2f);Set(mission,"fireBufferTimer",0f);
            Set(controls,"FireHeld",true);
            typeof(UrbanCombatMission).GetMethod("Update",Private).Invoke(mission,null);
            check(!player.InputBlocked && rifle.localPosition.z>.2f && rifle.localPosition.y<-.1f,
                "Live multiplayer continues weapon presentation outside the camera");
            float buffer=(float)typeof(UrbanCombatMission).GetField("fireBufferTimer",Private).GetValue(mission);
            check(buffer>0 && controls.WeaponSlotAmmo[0].StartsWith("..."),
                "Live multiplayer processes fire input and reload weapon controls");
            Set(manager,"isHost",false);Set(manager,"MatchState",DuelMatchState.InRound);Set(manager,"roundId",4);
            Set(manager,"LocalScore",0);Set(manager,"RivalScore",0);health.Initialize(100);
            Packet(manager,DuelPacketCodec.SerializeRoundState(DuelMatchState.RoundOver,1,0,"HOST",4));
            check(health.IsDead,"Host elimination result also kills the victim on their own peer");
            check((float)typeof(UrbanPlayer).GetField("duelHitFlash",Private).GetValue(player)>0,"Received lethal hit triggers visible local feedback");
            Set(player,"view",camera);Set(player,"scope",true);
            typeof(UrbanPlayer).GetMethod("Update",Private).Invoke(player,null);
            check(!player.Scoped,"Eliminated player leaves scope for the death view");
            camera.transform.localPosition=new Vector3(0,.3f,0);
            player.Face(Vector3.forward*30);health.Initialize(100);
            check(camera.transform.localPosition.y==1.55f && !health.IsDead,"Next round restores living eye height");
            Set(manager,"playerCamera",camera);Set(manager,"MatchState",DuelMatchState.InRound);player.InputBlocked=false;
            check(manager.CanShowRivalLocator && manager.GameplayCamera==camera,"Locator uses the duel camera for a living active player");
            player.InputBlocked=true;
            check(!manager.CanShowRivalLocator,"World locator hides while tactical map or controls block play");
            player.InputBlocked=false;Set(manager,"MatchState",DuelMatchState.RoundOver);
            check(!manager.CanShowRivalLocator,"World locator hides after the round ends");
            var locatorTarget=new RenderTexture(1200,720,24);camera.targetTexture=locatorTarget;camera.aspect=1200f/720f;camera.transform.rotation=Quaternion.identity;
            var project=typeof(SniperDuelHUD).GetMethod("ProjectRivalMarker",BindingFlags.Static|BindingFlags.NonPublic);
            Rect safe=new Rect(30,20,1140,680), markerBounds=new Rect(70,140,1000,400);
            object[] args={camera,camera.transform.position+Vector3.forward*100,safe,1f,markerBounds,false,false};
            Vector2 marker=(Vector2)project.Invoke(null,args);
            check(!((bool)args[6]) && Vector2.Distance(marker,new Vector2(570,340))<.1f,"Ahead marker respects offset safe area");
            args[1]=camera.transform.position+new Vector3(-300,0,100);marker=(Vector2)project.Invoke(null,args);
            check((bool)args[6] && Mathf.Abs(marker.x-markerBounds.x)<.1f,"Off-screen rival points to the left edge");
            args[1]=camera.transform.position+new Vector3(300,0,100);marker=(Vector2)project.Invoke(null,args);
            check((bool)args[6] && Mathf.Abs(marker.x-markerBounds.xMax)<.1f,"Off-screen rival points to the right edge");
            args[1]=camera.transform.position-Vector3.forward*100;marker=(Vector2)project.Invoke(null,args);
            check((bool)args[6] && markerBounds.Contains(marker-Vector2.right*.1f),"Rival behind camera stays at a visible edge");
            args[1]=camera.transform.position+new Vector3(0,100,100);marker=(Vector2)project.Invoke(null,args);
            check((bool)args[6] && Mathf.Abs(marker.y-markerBounds.y)<.1f,"Elevated off-screen rival points upward");
            args[1]=camera.transform.position+new Vector3(300,100,100);args[5]=true;marker=(Vector2)project.Invoke(null,args);
            check((bool)args[6] && Mathf.Abs(Vector2.Distance(marker,markerBounds.center)-200)<.1f,"Scope arrow stays within the circular optic");
            camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(locatorTarget);
            rival.Respawn(Vector3.zero,Quaternion.identity);rival.ApplyNetworkState(Vector3.zero,0,0,false,true,100);
            check(Mathf.Abs(rival.LocatorPosition.y-.95f)<.01f,"Locator follows the actual crouched avatar height");
            Set(manager,"MatchState",DuelMatchState.InRound);Set(manager,"mission",mission);Set(manager,"roundId",1);
            rival.transform.position=new Vector3(340,80,480);
            var getRival=typeof(UrbanCombatMission).GetMethod("TryGetDuelMapRival",Private);
            object[] mapArgs={null};
            check((bool)getRival.Invoke(mission,mapArgs) && (DuelOpponentAvatar)mapArgs[0]==rival,"Real rival remains on radar beyond 85m and 25m height without any bots");
            typeof(UrbanCombatMission).GetMethod("FocusDuelMap",Private).Invoke(mission,new object[]{rival.transform.position});
            Vector3 mapCenter=(Vector3)typeof(UrbanCombatMission).GetField("mapBrowseCenter",Private).GetValue(mission);
            float mapSpan=(float)typeof(UrbanCombatMission).GetField("mapSpan",Private).GetValue(mission);
            var viewport=new StreetMapViewport(new Rect(0,0,800,500),mapCenter,mapSpan);
            check(viewport.Rect.Contains(viewport.Project(player.transform.position)) && viewport.Rect.Contains(viewport.Project(rival.transform.position)),"Find Rival frames both players on tactical map");
            var clamp=typeof(UrbanCombatMission).GetMethod("ClampDuelMapPoint",BindingFlags.Static|BindingFlags.NonPublic);
            Vector2 mapEdge=(Vector2)clamp.Invoke(null,new object[]{new Rect(0,0,150,150),new Vector2(900,-600),14f});
            check(new Rect(13,13,124,124).Contains(mapEdge) && mapEdge.x>75 && mapEdge.y<75,"Out-of-range radar marker preserves rival bearing at a visible edge");
            rival.Die();mapArgs[0]=null;
            check(!(bool)getRival.Invoke(mission,mapArgs),"Dead rival disappears from live radar");
            var roofs=new System.Collections.Generic.List<Vector3>();
            for(int i=0;i<3;i++)
            {
                var platform=new GameObject("Readable duel roof "+i);platform.transform.SetParent(root.transform);platform.transform.position=new Vector3(i*120,9,200);
                platform.AddComponent<BoxCollider>().size=new Vector3(10,2,10);roofs.Add(new Vector3(i*120,10.1f,200));
            }
            Set(mission,"rooftopSpawns",roofs);Physics.SyncTransforms();
            bool selected=(bool)typeof(SniperDuelManager).GetMethod("DetermineRooftopPerches",Private).Invoke(manager,null);
            check(selected && Mathf.Abs(Vector3.Distance(manager.LocalSpawnPoint,manager.RivalSpawnPoint)-120)<.1f,"Arena favors clear 120m perches over the farthest rooftop pair");
            typeof(SniperDuelManager).GetMethod("BuildArenaCover",Private).Invoke(manager,null);Physics.SyncTransforms();
            var coverRoot=(GameObject)typeof(SniperDuelManager).GetField("arenaCoverRoot",Private).GetValue(manager);
            check(coverRoot.transform.childCount==4,"Both supported rooftops receive matching waist-high cover");
            check(manager.ArenaRadius>=75 && manager.ArenaRadius<=175,"Compact arena radius encloses shared spawn pair");
            Set(manager,"isHost",true);health.Initialize(100);health.Damage(20);player.transform.position=manager.ArenaCenter+Vector3.right*500;
            typeof(SniperDuelManager).GetMethod("ReturnLocalPlayerToArena",Private).Invoke(manager,null);
            check(Vector3.Distance(player.transform.position,manager.LocalSpawnPoint)<.01f && health.Health==80,"Leaving arena returns position without healing");
            Set(manager,"MatchState",DuelMatchState.InRound);Set(manager,"LocalScore",0);Set(manager,"RivalScore",0);
            rival.Respawn(new Vector3(20,0,30),Quaternion.identity);rival.TakeLocalDamage(1,30,Vector3.zero);
            typeof(SniperDuelManager).GetMethod("ResolveTimedRound",Private).Invoke(manager,null);
            check(manager.LocalScore==1 && manager.MatchState==DuelMatchState.RoundOver && !health.IsDead && !rival.IsDead,"Round timer awards higher health without inventing an elimination");
            Set(manager,"MatchState",DuelMatchState.InRound);Set(manager,"roundId",8);Set(manager,"lastRemoteStateSequence",-1);
            Packet(manager,DuelPacketCodec.SerializeStateSync(new Vector3(40,0,60),0,0,false,false,70,8,2));
            Packet(manager,DuelPacketCodec.SerializeStateSync(new Vector3(10,0,20),0,0,false,false,70,8,1));
            check((Vector3)typeof(DuelOpponentAvatar).GetField("targetPosition",Private).GetValue(rival)==new Vector3(40,0,60),"Reordered movement does not rewind rival position");
            Set(net,"CompetitiveSpotting",true);Set(manager,"rivalRevealUntil",-1f);
            check(!manager.RivalTrackingVisible,"Competitive room hides continuous rival tracking");
            Packet(manager,DuelPacketCodec.SerializeFire(Vector3.zero,Vector3.forward,1,7,1));
            check(!manager.RivalTrackingVisible,"Old-round gunfire cannot reveal competitive rival");
            Packet(manager,DuelPacketCodec.SerializeFire(Vector3.zero,Vector3.forward,2,8,1));
            check(!manager.RivalTrackingVisible,"Suppressed rifle does not reveal competitive rival");
            Packet(manager,DuelPacketCodec.SerializeFire(Vector3.zero,Vector3.forward,1,8,2,true));
            check(!manager.RivalTrackingVisible,"Acoustically masked gunfire does not reveal competitive rival");
            Packet(manager,DuelPacketCodec.SerializeFire(Vector3.zero,Vector3.forward,1,8,3));
            check(manager.RivalTrackingVisible,"Unsuppressed gunfire creates a temporary competitive ping");
            Set(manager,"isHost",false);Set(manager,"RoundSecondsRemaining",180f);
            Packet(manager,DuelPacketCodec.SerializeRoundClock(7,5));
            check(manager.RoundSecondsRemaining==180,"Clock from old round is ignored");
            Packet(manager,DuelPacketCodec.SerializeRoundClock(8,87));
            check(manager.RoundSecondsRemaining==87,"Guest round timer follows the host");
            check(DuelAudioFeedback.TryClosestPass(Vector3.zero,Vector3.forward*100,new Vector3(2,0,50),out _,out float passDistance) && Mathf.Abs(passDistance-2)<.01f,"Near-miss audio detects a close forward bullet segment");
            check(!DuelAudioFeedback.TryClosestPass(Vector3.zero,Vector3.forward*100,new Vector3(2,0,-5),out _,out _) && !DuelAudioFeedback.TryClosestPass(Vector3.zero,Vector3.forward*100,new Vector3(2,0,105),out _,out _),"Near-miss audio cannot play behind or beyond bullet segment");
            check(!DuelAudioFeedback.TryClosestPass(Vector3.zero,Vector3.forward*100,new Vector3(6,0,50),out _,out _),"Distant bullet does not play a near-miss crack");
            Set(manager,"MatchState",DuelMatchState.InRound);Set(manager,"isHost",false);
            health.Initialize(100);player.transform.position=Vector3.zero;player.Face(Vector3.forward*30);rival.Respawn(Vector3.forward*30,Quaternion.identity);Physics.SyncTransforms();
            rival.ApplyNetworkState(Vector3.forward*30,0,0,false,true,100);Physics.SyncTransforms();
            check(!SniperHitQuery.TryCast(new Ray(new Vector3(0,1.65f,0),Vector3.forward),null,player.transform,camera.transform,out _,100),"Crouched rival cannot be hit at the standing head height");
            rival.ApplyNetworkState(Vector3.forward*30,0,0,false,false,100);Physics.SyncTransforms();
            check(SniperHitQuery.TryCast(new Ray(new Vector3(0,1.55f,0),Vector3.forward),null,player.transform,camera.transform,out var headShot,100) && headShot.Collider.name=="Hitbox_Head","Standing head aim reaches head hitbox without torso interception");
            var arcade=root.AddComponent<BallisticsSystem>();var settings=ScriptableObject.CreateInstance<BallisticsConfig>();settings.isArcadeMode=true;settings.maxRange=100;settings.baseDamage=85;arcade.config=settings;
            arcade.Initialize(null,player.transform,camera,null);
            bool confirmedAvatarHit=false;int notifications=0;
            arcade.ShotResolved+=hit=>confirmedAvatarHit=hit;
            Action<DamageInfo> notification=hit=>notifications++;
            DamageSystem.OnDamageDealt+=notification;
            try { arcade.Fire(); }
            finally { DamageSystem.OnDamageDealt-=notification;UnityEngine.Object.DestroyImmediate(settings); }
            check(confirmedAvatarHit && !rival.IsDead && notifications==0,"Guest collision predicts visuals while awaiting host confirmation");
            DamageSystem.OnDamageDealt+=notification;
            var confirmed=DuelPacketCodec.SerializeShotResult(manager.RoundNumber,1,false,1,2,100,0,rival.LocatorPosition,30);
            try { Packet(manager,confirmed);Packet(manager,confirmed); }
            finally { DamageSystem.OnDamageDealt-=notification; }
            check(rival.IsDead && notifications==1,"Host confirmation applies guest damage and hit feedback exactly once");

            Set(manager,"MatchState",DuelMatchState.InRound);Set(manager,"isHost",true);Set(manager,"LocalScore",0);Set(manager,"RivalScore",0);
            health.Initialize(100);rival.Respawn(Vector3.forward*30,Quaternion.identity);Authority(manager,12,rival.transform.position);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(root.transform);wall.transform.position=new Vector3(0,1.5f,15);wall.transform.localScale=new Vector3(4,4,1);Physics.SyncTransforms();
            manager.NotifyLocalGunshot(new Vector3(0,1.55f,0),Vector3.forward,1);
            check(rival.CurrentHealth==100 && manager.LocalScore==0,"Host rejects damage through solid cover");
            UnityEngine.Object.DestroyImmediate(wall);Physics.SyncTransforms();Authority(manager,13,rival.transform.position);
            Set(manager,"localShotSequence",0);
            Packet(manager,DuelPacketCodec.SerializeStateSync(rival.transform.position,0,0,false,false,0,13,999));
            check(rival.CurrentHealth==100,"Guest cannot set authoritative health using movement packets");
            manager.NotifyLocalGunshot(new Vector3(0,1.55f,0),Vector3.forward,1);
            check(rival.IsDead && manager.LocalScore==1,"Unobstructed host shot uses authoritative target health");
            int oldMag=PlayerPrefs.GetInt("GeoSniper.WpnMagLvl_1",0);PlayerPrefs.SetInt("GeoSniper.WpnMagLvl_1",2);weapon.CurrentWeaponIndex=1;weapon.UseDuelStats=true;
            check(weapon.MaxAmmo==20,"Duel magazines ignore progression upgrades");PlayerPrefs.SetInt("GeoSniper.WpnMagLvl_1",oldMag);
            Set(manager,"MatchState",DuelMatchState.InRound);Set(manager,"RoundSecondsRemaining",77f);
            Set(net,"IsReconnecting",true);typeof(SniperDuelManager).GetMethod("Update",Private).Invoke(manager,null);
            check(!manager.CanPlay && manager.RoundSecondsRemaining==77,"Reconnection pauses input and round clock without clearing scores");
            Set(net,"IsReconnecting",false);
            Set(manager,"isHost",false);Set(manager,"shotClock",12f);
            Packet(manager,DuelPacketCodec.SerializeRoundClock(manager.RoundNumber,169f));
            check((float)typeof(SniperDuelManager).GetField("shotClock",Private).GetValue(manager)==12f,"Delayed display-clock corrections cannot move shot time backwards");
            Set(manager,"awaitingResumeClock",true);health.Initialize(100);
            Packet(manager,DuelPacketCodec.SerializeShotResult(manager.RoundNumber,10,true,1,2,100,0,player.transform.position,30));
            check(health.IsDead && !manager.CanPlay,"Buffered host-confirmed death applies while waiting for resume clock");
            Packet(manager,DuelPacketCodec.SerializeResumeClock(manager.RoundNumber,169f,11f,2,7,19,-1,0,.5f));
            check((float)typeof(SniperDuelManager).GetField("shotClock",Private).GetValue(manager)==11f
                && (int)typeof(UrbanCombatMission).GetField("ammo",Private).GetValue(mission)==7
                && (float)typeof(UrbanCombatMission).GetField("fireCooldown",Private).GetValue(mission)==.5f,
                "Recovery restores the host shot clock, current magazine and chamber cooldown");
            mission.RestoreMultiplayerDuelAmmo(2,7,19,0,.7f,0);
            typeof(UrbanCombatMission).GetMethod("CompleteReloadAmmo",Private).Invoke(mission,null);
            var restoredInventory=(int[])typeof(UrbanCombatMission).GetField("weaponAmmo",Private).GetValue(mission);
            check(restoredInventory[0]==5 && (int)typeof(UrbanCombatMission).GetField("ammo",Private).GetValue(mission)==7,
                "Restored reload refills its original rifle without granting ammo to the switched rifle");
            Set(manager,"awaitingResumeClock",true);Set(manager,"RivalScore",0);
            Packet(manager,DuelPacketCodec.SerializeRoundState(DuelMatchState.RoundOver,1,0,"HOST",manager.RoundNumber));
            check(!(bool)typeof(SniperDuelManager).GetField("awaitingResumeClock",Private).GetValue(manager)
                && manager.MatchState==DuelMatchState.RoundOver,"Queued round result releases resume wait before the next arena");
            Set(manager,"MatchState",DuelMatchState.Warmup);Set(manager,"RoundSecondsRemaining",0f);
            typeof(SniperDuelManager).GetMethod("ActivateRound",Private).Invoke(manager,null);
            check(manager.CanPlay && manager.RoundSecondsRemaining==180f && (float)typeof(SniperDuelManager).GetField("shotClock",Private).GetValue(manager)==0,
                "Engage initializes shot timing atomically before live packet handling");
            DuelKillReplayChecks.Run(check,camera);
            CheckCampaignTransition(root,manager,net,mission,player,camera,check);
            CheckDuelAdvertising(root,manager,net,mission,player,check);
        }
        catch(Exception error){report.AppendLine("FAIL: "+error);throw;}
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/DuelGameplayChecks.txt",report.ToString());
        }
    }
    static void CheckCampaignTransition(GameObject root,SniperDuelManager manager,SniperDuelNetwork net,
        UrbanCombatMission mission,UrbanPlayer player,Camera camera,Action<bool,string> check)
    {
        var disconnected=typeof(SniperDuelManager).GetMethod("HandleDisconnected",Private);
        Set(manager,"mission",mission);Set(manager,"MatchState",DuelMatchState.InRound);
        Set(mission,"MultiplayerDuel",true);mission.enabled=true;
        check(manager.HasActiveDuel,"Enabled multiplayer mission exposes its active duel HUD");
        mission.enabled=false;
        check(!manager.HasActiveDuel,"Disabled multiplayer mission cannot expose a stale duel HUD");
        Set(manager,"MatchNotice","Stale duel disconnect");Set(manager,"NoticeTimer",3600f);
        disconnected.Invoke(manager,null);
        check(manager.MatchState==DuelMatchState.Idle && manager.MatchNotice=="" && manager.NoticeTimer==0,
            "Disconnect after disabling the mission clears the duel notice instead of reopening it");
        mission.enabled=true;Set(mission,"MultiplayerDuel",false);Set(manager,"MatchState",DuelMatchState.InRound);
        check(!manager.HasActiveDuel,"Campaign mission cannot expose a duel HUD even with a stale round state");
        Set(manager,"MatchNotice","Stale duel disconnect");Set(manager,"NoticeTimer",3600f);
        disconnected.Invoke(manager,null);
        check(manager.MatchState==DuelMatchState.Idle && manager.MatchNotice=="" && manager.NoticeTimer==0,
            "Disconnect while a campaign mission is enabled does not publish a duel error");

        Set(mission,"MultiplayerDuel",true);Set(manager,"MatchState",DuelMatchState.InRound);
        Set(net,"LastDisconnectReason","Verification host disconnected");
        Set(manager,"LocalScore",2);Set(manager,"RivalScore",1);
        disconnected.Invoke(manager,null);
        check(manager.HasActiveDuel && manager.MatchState==DuelMatchState.LobbyWait
            && manager.MatchNotice.Contains("DUEL STOPPED: Verification host disconnected") && manager.NoticeTimer>0
            && manager.LocalScore==2 && manager.RivalScore==1,
            "Genuine multiplayer disconnect retains scores and presents the rejoin notice");

        var staleRivalObject=new GameObject("Stale rival during campaign switch");staleRivalObject.transform.SetParent(root.transform);
        var staleRival=staleRivalObject.AddComponent<DuelOpponentAvatar>();Set(manager,"Opponent",staleRival);
        Set(manager,"localPlayer",player);Set(manager,"playerCamera",camera);Set(manager,"roundId",17);
        Set(manager,"RoundSecondsRemaining",77f);Set(manager,"LocalBoundarySecondsRemaining",3f);
        Set(manager,"awaitingResumeClock",true);player.InputBlocked=true;
        Set(net,"IsHost",true);Set(net,"IsConnected",true);
        typeof(SniperDuelManager).GetMethod("EnsureNetwork",Private).Invoke(manager,null);
        check(Get<SniperDuelNetwork>(manager,"network")==net,"Campaign transition uses the isolated fixture's network");

        // An inactive object avoids the full lobby Awake/bootstrap and does not change PlayerPrefs.
        var gameObject=new GameObject("Campaign mode transition");gameObject.transform.SetParent(root.transform);gameObject.SetActive(false);
        var game=gameObject.AddComponent<GeoSniperGame>();
        Set(game,"pendingMultiplayerDuel",true);Set(game,"pendingDuelHost",true);Set(game,"pendingDuelTarget","D-TEST");
        Set(game,"duelLocationSelection",true);Set(game,"showingLocationConsent",true);Set(game,"resumeMissionAfterLocationConsent",true);
        int disconnectCallbacks=0;Action onDisconnected=()=>disconnectCallbacks++;
        net.OnDisconnected+=onDisconnected;
        try {game.SwitchTab(GeoSniperGame.LobbyTab.Campaign);}
        finally {net.OnDisconnected-=onDisconnected;}
        check(game.currentTab==GeoSniperGame.LobbyTab.Campaign && manager.MatchState==DuelMatchState.Idle
            && !manager.HasActiveDuel && manager.MatchNotice=="" && manager.NoticeTimer==0,
            "Switching from a stopped duel to Campaign removes its scoreboard and disconnect notice");
        check(manager.LocalScore==0 && manager.RivalScore==0 && manager.RoundNumber==0 && manager.RoundSecondsRemaining==0
            && manager.LocalBoundarySecondsRemaining==-1 && !Get<bool>(manager,"awaitingResumeClock"),
            "Campaign switch clears duel scores, round clock, boundary warning and reconnect wait");
        check(manager.Opponent==null && Get<UrbanCombatMission>(manager,"mission")==null
            && Get<UrbanPlayer>(manager,"localPlayer")==null && manager.GameplayCamera==null && !player.InputBlocked,
            "Campaign switch removes the rival and releases the previous duel player and camera");
        check(!Get<bool>(game,"pendingMultiplayerDuel") && !Get<bool>(game,"pendingDuelHost")
            && Get<string>(game,"pendingDuelTarget")=="" && !Get<bool>(game,"duelLocationSelection")
            && !Get<bool>(game,"showingLocationConsent") && !Get<bool>(game,"resumeMissionAfterLocationConsent"),
            "Campaign switch clears host, join target and pending duel location deployment flags");
        check(disconnectCallbacks==1 && !net.IsConnected && !net.IsHost && manager.MatchNotice=="",
            "Synchronous network disconnect during campaign switch cannot recreate the stopped duel overlay");
        Set(manager,"MatchState",DuelMatchState.InRound);
        check(!manager.HasActiveDuel,"Detached mission cannot expose a duel HUD even if a late callback leaves a round state");
        Set(manager,"MatchState",DuelMatchState.Idle);
    }
    static void CheckRematchConsent(SniperDuelManager manager,SniperDuelNetwork net,UrbanCombatMission mission,
        AdManager ads,Action<bool,string> check)
    {
        int completed=Get<int>(ads,"completedDuelMatches");
        var clear=typeof(SniperDuelManager).GetMethod("ClearRematchNegotiation",Private);
        var tick=typeof(SniperDuelManager).GetMethod("TickRematch",Private);
        var reconnect=typeof(SniperDuelManager).GetMethod("HandleReconnectState",Private);
        var disconnected=typeof(SniperDuelManager).GetMethod("HandleDisconnected",Private);
        Action<bool> seed=host=>{
            manager.StopAllCoroutines();Set(manager,"arenaRoutine",null);
            clear.Invoke(manager,new object[]{""});
            mission.enabled=true;Set(mission,"MultiplayerDuel",true);Set(manager,"mission",mission);
            Set(net,"IsConnected",true);Set(net,"IsReconnecting",false);Set(net,"IsHost",host);Set(manager,"isHost",host);
            Set(manager,"roundId",100);Set(manager,"completedMatchScope",100);Set(manager,"rematchArenaSequence",100);
            Set(manager,"pendingRematchArenaRound",0);Set(manager,"rematchStartInFlight",false);
            Set(manager,"nextRematchRequestId",0);Set(manager,"lastRemoteRematchId",0);
            Set(manager,"matchCompletionNotified",true);Set(manager,"MatchState",DuelMatchState.MatchOver);
            Set(manager,"LocalScore",1);Set(manager,"RivalScore",manager.TargetScore);
        };
        Action<DuelRematchAction,int,bool,int> signal=(action,id,byHost,next)=>
            Packet(manager,DuelPacketCodec.SerializeRematch(action,100,id,byHost,next));

        seed(true);manager.RequestRematch();
        int request=Get<int>(manager,"outgoingRematchId");float deadline=Get<float>(manager,"rematchDeadline");
        manager.RequestRematch();
        check(manager.MatchState==DuelMatchState.MatchOver && manager.LocalScore==1 && manager.RivalScore==3
            && manager.RematchOutgoingPending && manager.CanCancelRematch && Get<int>(manager,"outgoingRematchId")==request
            && Get<float>(manager,"rematchDeadline")==deadline,
            "Repeated rematch request preserves result and cannot extend the same proposal deadline");
        Packet(manager,DuelPacketCodec.SerializeRematch(DuelRematchAction.Accept,99,request,true));
        signal(DuelRematchAction.Accept,request+1,true,0);
        signal(DuelRematchAction.Commit,request,true,101);
        check(manager.MatchState==DuelMatchState.MatchOver && Get<int>(manager,"rematchOfferId")==0,
            "Host ignores stale match scope, wrong request approvals and guest-authored commits");
        signal(DuelRematchAction.Accept,request,true,0);
        check(manager.MatchState==DuelMatchState.MatchOver && Get<int>(manager,"rematchOfferId")==request,
            "Rival acceptance waits for matching guest readiness before host starts an arena");
        manager.CancelRematch();signal(DuelRematchAction.Ready,request,true,0);
        check(manager.MatchState==DuelMatchState.MatchOver && !manager.RematchOutgoingPending && manager.RivalScore==3,
            "Cancelling during confirmation rejects a late Ready without erasing the completed result");
        manager.RequestRematch();int newer=Get<int>(manager,"outgoingRematchId");
        signal(DuelRematchAction.Accept,request,true,0);signal(DuelRematchAction.Ready,request,true,0);
        check(newer>request && manager.MatchState==DuelMatchState.MatchOver && Get<int>(manager,"rematchOfferId")==0,
            "A cancelled request cannot approve a newer proposal in the same completed match");
        signal(DuelRematchAction.Accept,newer,true,0);signal(DuelRematchAction.Ready,newer,true,0);
        manager.StopAllCoroutines();Set(manager,"arenaRoutine",null);
        check(manager.MatchState==DuelMatchState.Warmup && manager.LocalScore==0 && manager.RivalScore==0
            && !Get<bool>(manager,"matchCompletionNotified") && Get<int>(manager,"pendingRematchArenaRound")==101
            && Get<int>(ads,"completedDuelMatches")==completed,
            "Only matched consent and guest Ready let host commit one new arena without consuming the ad counter");
        signal(DuelRematchAction.Ready,newer,true,0);
        check(Get<int>(manager,"pendingRematchArenaRound")==101,
            "Duplicate readiness cannot commit a second arena after rematch warmup begins");
        signal(DuelRematchAction.Cancel,newer,true,0);
        check(manager.MatchState==DuelMatchState.MatchOver && manager.LocalScore==1 && manager.RivalScore==3
            && Get<bool>(manager,"matchCompletionNotified"),
            "A timeout cancellation received during rematch warmup restores the preceding finished result");
        manager.RequestRematch();newer=Get<int>(manager,"outgoingRematchId");
        signal(DuelRematchAction.Accept,newer,true,0);signal(DuelRematchAction.Ready,newer,true,0);
        manager.StopAllCoroutines();Set(manager,"arenaRoutine",null);
        check(Get<int>(manager,"pendingRematchArenaRound")==102,
            "Retry after cancelled warmup allocates a new arena ID instead of reusing stale Ready or Engage signals");
        Set(net,"IsReconnecting",true);reconnect.Invoke(manager,new object[]{true});
        check(manager.MatchState==DuelMatchState.MatchOver && manager.LocalScore==1 && manager.RivalScore==3
            && Get<bool>(manager,"matchCompletionNotified") && !manager.RematchIncomingPending && !manager.RematchOutgoingPending,
            "Reconnection during rematch warmup restores completed scores and cancels pending consent");

        seed(false);signal(DuelRematchAction.Request,8,true,0);
        deadline=Get<float>(manager,"rematchDeadline");signal(DuelRematchAction.Request,8,true,0);
        signal(DuelRematchAction.Offer,8,true,0);signal(DuelRematchAction.Commit,8,true,101);
        check(manager.RematchIncomingPending && manager.MatchState==DuelMatchState.MatchOver && manager.RivalScore==3
            && !Get<bool>(manager,"rematchReady") && Get<float>(manager,"rematchDeadline")==deadline,
            "Guest must accept an incoming proposal; duplicate request, unsolicited offer and commit cannot start it");
        manager.AcceptRematch();
        check(!manager.RematchIncomingPending && manager.RematchOutgoingPending && manager.CanCancelRematch
            && manager.MatchState==DuelMatchState.MatchOver,
            "Accept moves incoming consent to pending confirmation while retaining the completed result");
        signal(DuelRematchAction.Offer,8,true,0);manager.CancelRematch();manager.DeclineRematch();manager.AcceptRematch();
        check(Get<bool>(manager,"rematchReady") && !manager.CanCancelRematch && manager.RematchOutgoingPending,
            "Guest Ready locks consent so late UI actions cannot cancel an already authorized host commit");
        signal(DuelRematchAction.Commit,7,true,101);
        signal(DuelRematchAction.Commit,8,true,101);manager.StopAllCoroutines();Set(manager,"arenaRoutine",null);
        signal(DuelRematchAction.Commit,8,true,101);
        check(manager.MatchState==DuelMatchState.Warmup && Get<int>(manager,"pendingRematchArenaRound")==101
            && !Get<bool>(manager,"matchCompletionNotified") && Get<int>(ads,"completedDuelMatches")==completed,
            "Guest starts only its accepted ready proposal on host commit and ignores stale or duplicate commits");
        Set(net,"IsConnected",false);disconnected.Invoke(manager,null);
        check(manager.MatchState==DuelMatchState.MatchOver && manager.RivalScore==3 && Get<bool>(manager,"matchCompletionNotified"),
            "Peer departure during rematch warmup preserves the prior completion and normal exit eligibility");

        seed(false);signal(DuelRematchAction.Request,9,true,0);manager.DeclineRematch();
        signal(DuelRematchAction.Request,9,true,0);signal(DuelRematchAction.Offer,9,true,0);
        check(!manager.RematchIncomingPending && !manager.RematchOutgoingPending && manager.MatchState==DuelMatchState.MatchOver,
            "Declined proposal cannot be reopened by its delayed request or offer");
        signal(DuelRematchAction.Request,10,true,0);
        tick.Invoke(manager,new object[]{Get<float>(manager,"rematchDeadline")});
        signal(DuelRematchAction.Offer,10,true,0);
        check(!manager.RematchIncomingPending && !manager.RematchOutgoingPending && manager.RematchSecondsRemaining==0
            && manager.RematchStatus.Contains("EXPIRED") && manager.RivalScore==3,
            "Incoming proposal expires at twenty seconds and a late offer cannot restart it");

        seed(true);manager.RequestRematch();signal(DuelRematchAction.Request,11,false,0);
        int simultaneous=Get<int>(manager,"rematchOfferId");bool owner=Get<bool>(manager,"rematchOfferByHost");
        signal(DuelRematchAction.Accept,Get<int>(manager,"outgoingRematchId"),true,0);
        check(simultaneous==11 && !owner && Get<int>(manager,"rematchOfferId")==simultaneous
            && manager.MatchState==DuelMatchState.MatchOver,
            "Simultaneous requests grant mutual consent and host keeps one proposal for confirmation");
        signal(DuelRematchAction.Ready,simultaneous,owner,0);manager.StopAllCoroutines();Set(manager,"arenaRoutine",null);
        check(manager.MatchState==DuelMatchState.Warmup,"Simultaneous requests start one rematch after guest readiness");
        seed(false);manager.RequestRematch();signal(DuelRematchAction.Request,12,true,0);
        signal(DuelRematchAction.Offer,12,true,0);
        check(Get<bool>(manager,"rematchReady") && !manager.RematchIncomingPending,
            "Guest simultaneous request consents to the incoming host proposal without another accept prompt");

        seed(true);manager.RequestRematch();tick.Invoke(manager,new object[]{Get<float>(manager,"rematchDeadline")});
        check(manager.MatchState==DuelMatchState.MatchOver && manager.RivalScore==3 && !manager.RematchOutgoingPending,
            "Outgoing request timeout retains finished scores and frees a new request");
        manager.RequestRematch();Set(net,"IsReconnecting",true);reconnect.Invoke(manager,new object[]{true});
        Set(net,"IsReconnecting",false);reconnect.Invoke(manager,new object[]{false});
        check(manager.MatchState==DuelMatchState.MatchOver && !manager.RematchIncomingPending && !manager.RematchOutgoingPending,
            "Reconnect cancels unresolved rematch and cannot resume stale consent after recovery");
        manager.RequestRematch();Set(net,"IsConnected",false);disconnected.Invoke(manager,null);
        check(manager.MatchState==DuelMatchState.MatchOver && !manager.RematchIncomingPending && !manager.RematchOutgoingPending,
            "Rival leaving cancels unresolved proposal while preserving the completed match");
        seed(true);manager.RequestRematch();manager.ResetSession("Rematch fixture cleanup.");
        check(!manager.RematchIncomingPending && !manager.RematchOutgoingPending && manager.RematchSecondsRemaining==0
            && manager.RematchStatus=="" && Get<int>(manager,"completedMatchScope")==0
            && Get<int>(manager,"nextRematchRequestId")==0 && Get<int>(ads,"completedDuelMatches")==completed,
            "Session reset clears consent, request identity and deadline without changing completed advertising matches");
    }
    static void CheckDuelAdvertising(GameObject root,SniperDuelManager manager,SniperDuelNetwork net,
        UrbanCombatMission mission,UrbanPlayer player,Action<bool,string> check)
    {
        var adObject=new GameObject("Inactive duel advertising checks");adObject.transform.SetParent(root.transform);adObject.SetActive(false);
        var ads=adObject.AddComponent<AdManager>();
        var adInstance=typeof(AdManager).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);
        var previousAds=AdManager.ActiveInstance;adInstance.SetValue(null,ads);
        var complete=typeof(SniperDuelManager).GetMethod("CheckMatchCompletion",Private);
        var notify=typeof(SniperDuelManager).GetMethod("NotifyCompletedMultiplayerMatch",Private);
        Action stopArena=()=>{manager.StopAllCoroutines();Set(manager,"arenaRoutine",null);};
        try
        {
            mission.enabled=true;Set(mission,"MultiplayerDuel",true);Set(manager,"mission",mission);Set(manager,"localPlayer",player);
            Set(net,"IsConnected",true);Set(net,"IsHost",true);Set(manager,"isHost",true);
            Set(manager,"roundId",70);
            Set(manager,"matchCompletionNotified",false);Set(manager,"MatchState",DuelMatchState.InRound);
            Set(manager,"LocalScore",0);Set(manager,"RivalScore",manager.TargetScore);
            complete.Invoke(manager,null);complete.Invoke(manager,null);
            check(Get<int>(ads,"completedDuelMatches")==1 && Get<bool>(manager,"matchCompletionNotified") && !ads.IsBusy,
                "Host defeat counts one completed multiplayer match without showing an ad or counting twice");

            manager.RequestRematch();stopArena();
            check(manager.MatchState==DuelMatchState.MatchOver && Get<bool>(manager,"matchCompletionNotified")
                && manager.LocalScore==0 && manager.RivalScore==manager.TargetScore && manager.RematchOutgoingPending,
                "Requesting a rematch preserves the completed result until the rival consents");
            int request=Get<int>(manager,"outgoingRematchId");
            Packet(manager,DuelPacketCodec.SerializeRematch(DuelRematchAction.Accept,70,request,true));
            Packet(manager,DuelPacketCodec.SerializeRematch(DuelRematchAction.Ready,70,request,true));stopArena();
            check(!Get<bool>(manager,"matchCompletionNotified") && Get<int>(ads,"completedDuelMatches")==1
                && manager.MatchState==DuelMatchState.Warmup && manager.LocalScore==0 && manager.RivalScore==0,
                "Mutually approved rematch resets completion dedup while preserving previous matches toward the exit cap");
            // Exercise victory qualification without invoking its unrelated PlayerPrefs reward payout.
            Set(manager,"roundId",71);Set(manager,"rematchStartInFlight",false);
            Set(manager,"MatchState",DuelMatchState.MatchOver);Set(manager,"LocalScore",manager.TargetScore);Set(manager,"RivalScore",0);
            notify.Invoke(manager,null);notify.Invoke(manager,null);
            check(Get<int>(ads,"completedDuelMatches")==2 && !ads.IsBusy,
                "A multiplayer victory also counts once and waits until exit to offer an interstitial");

            Set(manager,"isHost",false);
            Packet(manager,DuelPacketCodec.SerializeRematch(DuelRematchAction.Request,71,2,true));
            manager.AcceptRematch();
            Packet(manager,DuelPacketCodec.SerializeRematch(DuelRematchAction.Offer,71,2,true));
            Packet(manager,DuelPacketCodec.SerializeRematch(DuelRematchAction.Commit,71,2,true,72));stopArena();
            check(!Get<bool>(manager,"matchCompletionNotified") && Get<int>(ads,"completedDuelMatches")==2,
                "Host-committed accepted rematch resets completion dedup without consuming finished matches");
            Set(manager,"rematchStartInFlight",false);
            Set(manager,"isHost",false);Set(manager,"roundId",99);Set(manager,"MatchState",DuelMatchState.InRound);
            var loss=DuelPacketCodec.SerializeRoundState(DuelMatchState.MatchOver,manager.TargetScore,0,"TIMEOUT",99);
            Packet(manager,loss);Packet(manager,loss);
            check(manager.MatchState==DuelMatchState.MatchOver && Get<int>(ads,"completedDuelMatches")==3 && !ads.IsBusy,
                "Guest authoritative match-over result counts its defeat once despite duplicate packets");
            CheckRematchConsent(manager,net,mission,ads,check);

            Set(manager,"matchCompletionNotified",false);Set(mission,"MultiplayerDuel",false);
            notify.Invoke(manager,null);
            check(Get<int>(ads,"completedDuelMatches")==3 && !Get<bool>(manager,"matchCompletionNotified"),
                "Practice or campaign matches do not enter the completed multiplayer ad counter");
            Set(mission,"MultiplayerDuel",true);mission.enabled=false;notify.Invoke(manager,null);
            check(Get<int>(ads,"completedDuelMatches")==3,"A disabled old duel mission cannot count as a completed live match");
            mission.enabled=true;Set(net,"IsConnected",false);notify.Invoke(manager,null);
            check(Get<int>(ads,"completedDuelMatches")==3,"A disconnected duel cannot invent a completed match for advertising");

            Set(net,"IsHost",true);Set(manager,"matchCompletionNotified",true);Set(manager,"duelExitFinished",true);
            manager.StartDuelMatch(mission,true);
            check(!Get<bool>(manager,"matchCompletionNotified") && !Get<bool>(manager,"duelExitFinished")
                && manager.MatchState==DuelMatchState.LobbyWait && Get<int>(ads,"completedDuelMatches")==3,
                "Starting a new room resets manager exit and completion guards without losing the advertising counter");
            Set(manager,"MatchState",DuelMatchState.MatchOver);Set(manager,"matchCompletionNotified",true);
            Set(net,"IsConnected",true);Set(manager,"localPlayer",player);
            int disconnects=0;bool stoppedBeforeDisconnect=false;
            Action onDisconnect=()=>{
                disconnects++;
                stoppedBeforeDisconnect=manager.MatchState==DuelMatchState.Idle && !mission.enabled
                    && Get<UrbanCombatMission>(manager,"mission")==null;
                manager.LeaveDuel();
            };
            net.OnDisconnected+=onDisconnect;
            try {manager.LeaveDuel();manager.LeaveDuel();}
            finally {net.OnDisconnected-=onDisconnect;}
            check(stoppedBeforeDisconnect && disconnects==1 && manager.MatchState==DuelMatchState.Idle
                && Get<bool>(manager,"duelExitFinished") && !Get<bool>(manager,"leavingDuel") && !net.IsConnected,
                "Finished duel exit stops gameplay before synchronous disconnect and rejects repeated or reentrant exits");
            check(!ads.IsBusy && Get<int>(ads,"completedDuelMatches")==3,
                "Unavailable exit ad never blocks teardown or consumes completed match eligibility");

            mission.enabled=true;Set(mission,"MultiplayerDuel",true);Set(manager,"mission",mission);
            Set(manager,"MatchState",DuelMatchState.MatchOver);Set(manager,"matchCompletionNotified",true);
            Set(manager,"duelExitFinished",false);Set(manager,"LocalScore",1);Set(manager,"RivalScore",manager.TargetScore);
            Set(manager,"roundId",100);Set(manager,"completedMatchScope",100);
            Set(net,"IsConnected",true);Set(net,"IsReconnecting",false);
            var eligible=typeof(SniperDuelManager).GetMethod("CompletedDuelExitEligible",Private);
            check(manager.CanRematch,"Completed match can offer a rematch while the rival remains connected");
            net.Disconnect("Opponent left the duel after the final result.");
            check(manager.MatchState==DuelMatchState.MatchOver && manager.HasActiveDuel && manager.LocalScore==1
                && manager.RivalScore==manager.TargetScore && manager.MatchNotice.Contains("MATCH COMPLETE")
                && !manager.MatchNotice.Contains("DUEL STOPPED") && Get<int>(ads,"completedDuelMatches")==3,
                "Rival leaving after the final result preserves completed match results instead of publishing a connection error");
            check(!manager.CanRematch && (bool)eligible.Invoke(manager,null),
                "Completed peer departure disables rematch while preserving the second player's normal exit ad eligibility");
            manager.RequestRematch();Packet(manager,DuelPacketCodec.SerializeRematch(DuelRematchAction.Request,100,1,true));
            check(manager.MatchState==DuelMatchState.MatchOver && Get<bool>(manager,"matchCompletionNotified"),
                "Neither local nor delayed remote rematch can reopen an arena after the rival leaves");
            Set(net,"IsReconnecting",true);
            check(!(bool)eligible.Invoke(manager,null),"Exit advertising remains unavailable while reconnection is in progress");
            Set(net,"IsReconnecting",false);manager.LeaveDuel();
            check(manager.MatchState==DuelMatchState.Idle && !mission.enabled && !ads.IsBusy
                && Get<int>(ads,"completedDuelMatches")==3,
                "Second player can leave a completed match after the rival without waiting for an unavailable ad");

            mission.enabled=true;Set(manager,"mission",mission);Set(manager,"MatchState",DuelMatchState.InRound);
            Set(manager,"matchCompletionNotified",false);Set(net,"IsConnected",true);
            net.Disconnect("Verification interrupted an unfinished round.");
            check(manager.MatchState==DuelMatchState.LobbyWait && manager.MatchNotice.Contains("DUEL STOPPED")
                && !(bool)eligible.Invoke(manager,null) && Get<int>(ads,"completedDuelMatches")==3,
                "Unfinished peer disconnect still stops the duel and never qualifies the interrupted round for an exit ad");

            adInstance.SetValue(null,null);
            foreach(bool practice in new[]{false,true})
            {
                mission.enabled=true;Set(mission,"MultiplayerDuel",!practice);Set(manager,"mission",mission);
                Set(manager,"MatchState",practice?DuelMatchState.MatchOver:DuelMatchState.LobbyWait);
                Set(manager,"matchCompletionNotified",true);Set(manager,"duelExitFinished",false);
                Set(net,"IsConnected",practice);
                manager.LeaveDuel();
                check(AdManager.ActiveInstance==null && manager.MatchState==DuelMatchState.Idle,
                    practice?"Practice exit does not create an ad manager":"Disconnected exit does not create an ad manager");
            }
            mission.enabled=true;Set(mission,"MultiplayerDuel",true);Set(manager,"mission",mission);
            Set(manager,"MatchState",DuelMatchState.InRound);Set(manager,"matchCompletionNotified",false);
            Set(manager,"duelExitFinished",false);Set(net,"IsConnected",true);
            manager.LeaveDuel();
            check(AdManager.ActiveInstance==null && manager.MatchState==DuelMatchState.Idle,
                "Aborting an unfinished multiplayer round does not create an ad manager");
        }
        finally
        {
            stopArena();adInstance.SetValue(null,previousAds);UnityEngine.Object.DestroyImmediate(adObject);
        }
    }
}
