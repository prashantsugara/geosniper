using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using GeoSniper;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Real Play-mode smoke tests, intentionally restricted to the disposable review project.
[InitializeOnLoad]
public static class CampaignGameplayChecks
{
    const string Key="GeoSniper.CampaignPlayChecks";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly Stack<IEnumerator> work=new Stack<IEnumerator>();
    static readonly StringBuilder report=new StringBuilder();
    static DateTime started;
    static CampaignGameplayChecks() {EditorApplication.update+=Tick;}
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/.utmp/review-unity/")) throw new Exception("Use the isolated review project");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        SessionState.SetBool(Key,true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if(work.Count==0) {started=DateTime.UtcNow;report.AppendLine(started.ToString("O"));work.Push(Checks());}
            if((DateTime.UtcNow-started).TotalMinutes>5) throw new Exception("Campaign smoke-test timeout");
            var job=work.Peek();
            if(!job.MoveNext()) {work.Pop();if(work.Count==0) Finish(true);}
            else if(job.Current is IEnumerator nested) work.Push(nested);
        }
        catch(Exception error) {report.AppendLine("FAIL: "+error);Finish(false);}
    }
    static void Finish(bool success)
    {
        while(work.Count>0) (work.Pop() as IDisposable)?.Dispose();
        SessionState.SetBool(Key,false);
        Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CampaignGameplayChecks.txt",report.ToString());
        Debug.Log(report.ToString());EditorApplication.Exit(success?0:1);
    }
    static object Get(object obj,string name)=>obj.GetType().GetField(name,Private).GetValue(obj);
    static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Private).SetValue(obj,value);
    static void Require(bool ok,string message) {if(!ok) throw new Exception(message);}
    static IEnumerator Frames(int count)
    {
        int until=Time.frameCount+count;
        while(Time.frameCount<until) yield return null;
    }
    static IEnumerator Checks()
    {
        bool hadDifficulty=PlayerPrefs.HasKey("GeoSniper.Difficulty");int oldDifficulty=PlayerPrefs.GetInt("GeoSniper.Difficulty");
        var random=UnityEngine.Random.state;
        try
        {
            DifficultyProfile.Selected=DifficultyLevel.Standard;
            UnityEngine.Random.InitState(230919);
            var world=new GameObject("Campaign test sector").AddComponent<SectorWorld>();
            yield return world.GenerateAsync(SectorMap.Offline());
            Physics.SyncTransforms();
            report.AppendLine("Offline world: "+world.SpawnPoints.Count+" street points, "+world.RooftopSpawns.Count+" rooftops");
            var camera=new GameObject("Check camera").AddComponent<Camera>();
            int liveVipChecks=0,liveCourierChecks=0;
            foreach(var node in CampaignNodeGraph.Nodes)
            {
                var root=new GameObject("Campaign node "+node.id);
                var mission=root.AddComponent<UrbanCombatMission>();
                mission.Begin(camera,world.SpawnPoints,world.RooftopSpawns,false,null,false,null,true);
                if(node.isPvPDuel) mission.BeginPvPDuel(node.id);else mission.BeginStage(node.stageIndex);
                Require(mission.currentMissionState==(node.isPvPDuel?MissionState.InProgress:MissionState.Briefing),"Node "+node.id+" setup failed: "+Get(mission,"contractSetupError"));
                var enemies=(List<EnemyBot>)Get(mission,"enemies");
                Require(enemies.Count>0 && enemies.Count<=node.enemyCount,"Threat budget exceeded at "+node.id);
                Require(ReferenceEquals(Get(mission,"campaignNode"),node),"Wrong node bound at "+node.id);
                if(!node.isPvPDuel) Require(((CampaignContract)Get(mission,"activeContract")).Type==node.contractType,"Wrong mission type at "+node.id);
                Require(Physics.Raycast(mission.Player.transform.position+Vector3.up*.2f,Vector3.down,2f),"Unsupported player at "+node.id);
                foreach(var enemy in enemies) Require(enemy.Difficulty==mission.Difficulty && enemy.Actor.Health>0,"Spawn bypassed difficulty at "+node.id);
                if(node.id==0)
                {
                    Require(enemies.Count==2 && Get(mission,"contractCounterSniper")==null,"Training has elite threats");
                    Require((float)Get(mission,"contractModeTimer")==0,"Training is timed");
                    foreach(var enemy in enemies) Require(enemy.speed==0,"Training target is moving");
                }
                var vip=Get(mission,"contractVIP") as CivilianBot;
                if(node.contractType==CampaignContractType.Overwatch)
                {
                    var firstAmbusher=enemies.Find(enemy=>enemy.isVipAttacker);
                    Require(firstAmbusher!=null && (float)Get(firstAmbusher,"fireCooldown")>=13.9f,
                        "VIP ambush starts before a reaction window at "+node.id);
                    for(int a=0;a<enemies.Count;a++)
                        if(enemies[a].isVipAttacker)
                            for(int b=a+1;b<enemies.Count;b++)
                                if(enemies[b].isVipAttacker)
                                {
                                    Vector3 spacing=enemies[a].transform.position-enemies[b].transform.position;
                                    spacing.y=0f;
                                    Require(spacing.magnitude>=7.9f,"VIP ambushers spawned together at "+node.id+": "+spacing.magnitude.ToString("F2")+"m at "+enemies[a].transform.position+" and "+enemies[b].transform.position);
                                }
                }
                if(node.contractType==CampaignContractType.TimedInterception)
                {
                    var courier=Get(mission,"contractTarget") as EnemyBot;
                    Require(courier!=null && courier.runnerStartDelay>=10f && (float)Get(mission,"contractModeTimer")>=85f,
                        "Courier mission has too little reaction time at "+node.id);
                }
                Vector3 vipPosition=vip!=null?vip.transform.position:Vector3.zero;
                float health=mission.Player.Health.Health;
                yield return Frames(8);
                if(!node.isPvPDuel)
                {
                    Require(mission.Player.Health.Health==health,"Briefing applies damage");
                    if(vip!=null) Require(Vector3.Distance(vipPosition,vip.transform.position)<.01f && vip.Suspended,"VIP advances during briefing");
                    mission.currentMissionState=MissionState.InProgress;
                    Set(mission,"mapOpen",true);
                    float timer=(float)Get(mission,"contractModeTimer");
                    var waves=Get(mission,"extractionWaves") as ExtractionWavePlan;
                    float waveElapsed=waves!=null?waves.Elapsed:0;
                    yield return Frames(8);
                    Require((float)Get(mission,"contractModeTimer")==timer,"Map consumes mission timer");
                    Require(waves==null || waves.Elapsed==waveElapsed,"Map consumes wave schedule");
                    if(vip!=null) Require(Vector3.Distance(vipPosition,vip.transform.position)<.01f,"VIP advances on map");
                    Set(mission,"mapOpen",false);
                    yield return Frames(4);
                    Require(mission.currentMissionState!=MissionState.Failed,"Immediate objective failure at "+node.id);
                    bool checkVip=node.contractType==CampaignContractType.Overwatch && (liveVipChecks++<2 || node.id==41);
                    bool checkCourier=node.contractType==CampaignContractType.TimedInterception && (liveCourierChecks++<2 || node.id==42);
                    if(checkVip || checkCourier)
                    {
                        var objective=checkVip?(Component)vip:Get(mission,"contractTarget") as EnemyBot;
                        Require(objective!=null,"Missing live objective at "+node.id);
                        foreach(var enemy in enemies)if(enemy!=objective)enemy.gameObject.SetActive(false);
                        Vector3 start=objective.transform.position;
                        if(checkCourier)
                        {
                            var courier=(EnemyBot)objective;
                            Require(courier.runnerStartDelay>=10f,"Courier has no opening reaction window at "+node.id);
                            yield return Frames(120);
                            Require(Vector3.Distance(start,objective.transform.position)<.3f,
                                "Courier departed during opening reaction window at "+node.id);
                            courier.runnerStartDelay=0f;
                        }
                        yield return Frames(120);
                        Require(Vector3.Distance(start,objective.transform.position)>.7f,
                            "Objective did not move after briefing at "+node.id);
                        Require(mission.currentMissionState==MissionState.InProgress,
                            "Objective failed before player could act at "+node.id);
                        if(checkCourier)
                        {
                            ((EnemyBot)objective).Actor.Damage(10000f);
                            yield return Frames(3);
                            Require((bool)Get(mission,"victoryPending") || mission.currentMissionState==MissionState.Complete,
                                "Courier elimination did not finish node "+node.id);
                        }
                        else
                        {
                            objective.transform.position=(Vector3)Get(mission,"extractionPoint");
                            yield return Frames(3);
                            Require((bool)Get(mission,"victoryPending") || mission.currentMissionState==MissionState.Complete,
                                "VIP extraction did not finish node "+node.id);
                        }
                        report.AppendLine("PASS: live "+node.contractType+" movement and completion at node "+node.id);
                    }
                    if(waves!=null)CheckAssaultWaves(mission,waves,node.id);
                }
                if(node.isPvPDuel)
                {
                    float damage=mission.Difficulty.enemyDamage;
                    mission.Restart();yield return Frames(8);
                    Require(mission.isPvPDuel && mission.currentMissionState==MissionState.InProgress,"Retry lost duel mode");
                    Require(ReferenceEquals(Get(mission,"campaignNode"),node) && Mathf.Approximately(mission.Difficulty.enemyDamage,damage),"Retry changed node or compounded difficulty");
                }
                Set(mission,"victoryPending",true);
                typeof(UrbanCombatMission).GetMethod("FailMission",Private).Invoke(mission,new object[]{"TEST OBJECTIVE FAILURE"});
                Require(!(bool)Get(mission,"victoryPending"),"Failure kept pending victory at "+node.id);
                Set(mission,"shotNotice","UNRELATED NOTICE");
                Require((string)Get(mission,"missionFailureReason")=="TEST OBJECTIVE FAILURE","Failure reason was overwritten at "+node.id);
                string guidance=(string)typeof(UrbanCombatMission).GetMethod("FailureGuidance",Private).Invoke(mission,null);
                Require(!string.IsNullOrWhiteSpace(guidance) && guidance.Length>40,"Missing actionable failure guidance at "+node.id);
                report.AppendLine("PASS: node "+node.id+" "+node.title+" / "+CampaignProgression.Label(node)+" / "+enemies.Count+" threats");
                Object.Destroy(root);yield return Frames(3);
            }
            // No invisible rooftop fallback on maps with no roofs. A supported
            // street duel is acceptable; an airborne fallback is not.
            var invalidRoot=new GameObject("No-roof duel");var invalid=invalidRoot.AddComponent<UrbanCombatMission>();
            invalid.Begin(camera,world.SpawnPoints,new List<Vector3>(),false,null,false,null,true);
            invalid.BeginPvPDuel();
            Require(invalid.currentMissionState==MissionState.InProgress,"Duel failed despite supported street fallback");
            Require(Physics.Raycast(invalid.Player.transform.position+Vector3.up*.2f,Vector3.down,2f),"No-roof duel spawned player in mid-air");
            var invalidEnemies=(List<EnemyBot>)Get(invalid,"enemies");
            Require(invalidEnemies.Count==1 && Physics.Raycast(invalidEnemies[0].transform.position+Vector3.up*.2f,Vector3.down,2f),"No-roof duel spawned rival in mid-air");
            Require(Get(invalid,"campaignNode")==null,"Standalone duel retained campaign identity");
            report.AppendLine("PASS: no-roof duel uses supported street fallback; standalone campaign identity cleared");
            Object.Destroy(invalidRoot);yield return Frames(3);
            var reuseHost=new GameObject("Sector reuse controller");
            var reuseGame=reuseHost.AddComponent<GeoSniperGame>();
            Set(reuseGame,"world",world);
            Set(reuseGame,"cachedSectorReady",true);
            Set(reuseGame,"cachedSectorOffline",true);
            Set(reuseGame,"geographicMap",false);
            reuseGame.ReturnToLevelMap();
            Require(ReferenceEquals(Get(reuseGame,"world"),world) && !world.gameObject.activeSelf,
                "Returning to the campaign menu destroyed the reusable sector");
            typeof(GeoSniperGame).GetMethod("StartOffline",Private).Invoke(reuseGame,new object[]{true});
            yield return Frames(16);
            Require(ReferenceEquals(Get(reuseGame,"world"),world) && (bool)Get(reuseGame,"ready")
                && world.gameObject.activeSelf,"Next mission rebuilt the same offline sector");
            Require(UnityEngine.AI.NavMesh.SamplePosition(world.SpawnPoints[0],out _,2f,UnityEngine.AI.NavMesh.AllAreas),
                "Cached sector did not restore navigation when reactivated");
            reuseGame.ReturnToLevelMap();
            Set(reuseGame,"cachedSectorOffline",false);
            Set(reuseGame,"cachedSectorElevationEnabled",reuseGame.enableElevationForGameplay);
            Set(reuseGame,"cachedSectorLatitude",28.6139d);
            Set(reuseGame,"cachedSectorLongitude",77.209d);
            var samePlace=new GameLocation{Latitude=28.6139,Longitude=77.209,Label="Test place"};
            var anotherPlace=new GameLocation{Latitude=28.6339,Longitude=77.209,Label="Other place"};
            var reuseMethod=typeof(GeoSniperGame).GetMethod("CanReuseSector",Private);
            Require((bool)reuseMethod.Invoke(reuseGame,new object[]{samePlace}),"Same location misses built sector");
            Require(!(bool)reuseMethod.Invoke(reuseGame,new object[]{anotherPlace}),"Different location reuses wrong sector");
            Set(reuseGame,"cachedSectorFromGps",true);
            var driftingGps=new GameLocation{Latitude=28.6141,Longitude=77.209,Source="gps"};
            Require((bool)reuseMethod.Invoke(reuseGame,new object[]{driftingGps}),"GPS drift rebuilt the same sector");
            Require(!(bool)reuseMethod.Invoke(reuseGame,new object[]{anotherPlace}),"Distant GPS fix reused old sector");
            bool hadSource=PlayerPrefs.HasKey("GeoSniper.LocationSource");
            string priorSource=PlayerPrefs.GetString("GeoSniper.LocationSource","");
            PlayerPrefs.SetString("GeoSniper.LocationSource","gps");
            Set(reuseGame,"activeLocation",driftingGps);
            var preferredMethod=typeof(GeoSniperGame).GetMethod("PreferredLocation",Private);
            Require(ReferenceEquals(preferredMethod.Invoke(reuseGame,null),driftingGps),
                "Next campaign mission requests GPS despite a loaded GPS sector");
            typeof(GeoSniperGame).GetMethod("ReleaseLobbySectorOnLowMemory",Private).Invoke(reuseGame,null);
            Require(Get(reuseGame,"world")==null,"Low-memory release retained cached sector");
            Require(preferredMethod.Invoke(reuseGame,null)==null,"Released GPS sector was reused");
            if(hadSource) PlayerPrefs.SetString("GeoSniper.LocationSource",priorSource);
            else PlayerPrefs.DeleteKey("GeoSniper.LocationSource");
            Object.Destroy(reuseHost);yield return Frames(3);
            report.AppendLine("PASS: mission transition reuses a same-location sector and releases it on low memory");
            report.AppendLine("PASS: all campaign Play-mode checks");
        }
        finally
        {
            if(hadDifficulty) PlayerPrefs.SetInt("GeoSniper.Difficulty",oldDifficulty);else PlayerPrefs.DeleteKey("GeoSniper.Difficulty");
            PlayerPrefs.Save();UnityEngine.Random.state=random;
        }
    }

    static void CheckAssaultWaves(UrbanCombatMission mission,ExtractionWavePlan waves,int nodeId)
    {
        var enemies=(List<EnemyBot>)Get(mission,"enemies");
        int initial=enemies.Count;
        var update=typeof(UrbanCombatMission).GetMethod("UpdateExtractionWaves",Private);
        // Accelerate the scheduler without advancing combat; verify actual generated
        // spawn sites and extraction transition, not just the pure timing policy.
        for(int step=0;step<180 && mission.currentMissionState==MissionState.InProgress;step++)
        {
            int alive=0;
            foreach(var enemy in enemies)if(enemy!=null && !enemy.Actor.IsDead)alive++;
            Require(alive<=waves.ActiveCap,"Wave exceeded active cap at node "+nodeId);
            // Simulate successful defense, allowing each wave to finish arriving.
            if(alive>=waves.ActiveCap || (waves.Pending==0 && step%5==0))
                foreach(var enemy in enemies)
                    if(enemy!=null && !enemy.Actor.IsDead)
                    {enemy.Actor.Damage(10000);enemy.gameObject.SetActive(false);}
            int before=enemies.Count;
            update.Invoke(mission,new object[]{1f});
            for(int i=before;i<enemies.Count;i++)
            {
                var position=enemies[i].transform.position;
                Require(SectorWorld.DryFootprint(position,.6f) && Physics.Raycast(position+Vector3.up*.2f,Vector3.down,2f),"Unsafe reinforcement at node "+nodeId);
            }
        }
        Require(waves.Wave==3 && waves.Pending==0 && enemies.Count>=initial+4,"Campaign failed to deliver all three waves at node "+nodeId);
        Require(mission.currentMissionState==MissionState.Extraction,"Wave mission stalled before extraction at node "+nodeId+": "+Get(mission,"missionFailureReason"));
        report.AppendLine("PASS: node "+nodeId+" delivered three waves, safe reinforcements and extraction; cap "+waves.ActiveCap);
    }
}
