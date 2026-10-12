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
[InitializeOnLoad]
public static class CleanFlowChecks
{
    const string Key="GeoSniper.CleanFlowChecks";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly Stack<IEnumerator> work=new Stack<IEnumerator>();
    static readonly StringBuilder report=new StringBuilder();
    static DateTime started;
    static AsyncOperation pendingOperation;
    static CleanFlowChecks(){EditorApplication.update+=Tick;}
    public static void RunIndoor(){SessionState.SetBool("GeoSniper.IndoorChecksOnly",true);SessionState.SetBool("GeoSniper.LeakChecks",true);Run();}
    public static void RunRetention(){SessionState.SetBool("GeoSniper.RetentionOnly",true);Run();}
    public static void RunLeak(){SessionState.SetBool("GeoSniper.LeakChecks",true);SessionState.SetBool("GeoSniper.RetentionOnly",true);Run();}
    public static void RunCommander(){SessionState.SetBool("GeoSniper.LeakChecks",true);SessionState.SetBool("GeoSniper.RetentionOnly",false);Run();}
    public static void RunScopePreview(){SessionState.SetBool("GeoSniper.EnemyScopeCapture",true);SessionState.SetBool("GeoSniper.ScopeCaptureOnly",true);RunRetention();}
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/.utmp/review-unity/"))throw new Exception("Use disposable review project");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    static void CaptureObjective(Camera view,string name)
    {
        var target=new RenderTexture(1280,720,24);var previous=view.targetTexture;view.targetTexture=target;view.Render();var active=RenderTexture.active;RenderTexture.active=target;
        var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes("Logs/"+name,texture.EncodeToPNG());
        view.targetTexture=previous;RenderTexture.active=active;UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(target);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling)return;
        try
        {
            if(work.Count==0){started=DateTime.UtcNow;work.Push(Checks());}
            if((DateTime.UtcNow-started).TotalMinutes>3)throw new Exception("Clean flow check timeout");
            if(pendingOperation!=null){if(!pendingOperation.isDone)return;pendingOperation=null;}
            var next=work.Peek();if(!next.MoveNext()){work.Pop();if(work.Count==0)Finish(true);}else if(next.Current is IEnumerator child)work.Push(child);else if(next.Current is AsyncOperation operation)pendingOperation=operation;
        }
        catch(Exception error){report.AppendLine("FAIL: "+error);Finish(false);}
    }
    static void Finish(bool success)
    {
        while(work.Count>0)(work.Pop() as IDisposable)?.Dispose();
        SessionState.SetBool("GeoSniper.IndoorChecksOnly",false);
        SessionState.SetBool(Key,false);Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CleanFlowChecks.txt",report.ToString());
        Debug.Log(report.ToString());EditorApplication.Exit(success?0:1);
    }
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    static void CheckObjectiveSecurity(UrbanCombatMission mission,Vector3 objective,int level)
    {
        foreach(var building in EnterableBuilding.All)
        {
            if(building.Floors<=1 || !building.Contains(objective))continue;
            var tasks=(IList)Get(mission,"storyPoints");
            for(int floor=0;floor<building.Floors;floor++)
            {
                bool targetFloor=false;
                foreach(var task in tasks)
                    if(task.GetType().GetField("Building").GetValue(task)==building && building.FloorAt((Vector3)task.GetType().GetField("Position").GetValue(task))==floor)targetFloor=true;
                int security=0;
                foreach(var enemy in (List<EnemyBot>)Get(mission,"enemies"))
                    if(!enemy.Actor.IsDead && (enemy.name=="Objective security guard" || enemy.name=="Dispatch archive guard") && building.Contains(enemy.transform.position) && building.FloorAt(enemy.transform.position)==floor)security++;
                Check(targetFloor?security==0:security>0,"Incorrect floor security at level "+(level+1)+", floor "+(floor+1));
            }
            report.AppendLine("PASS: level "+(level+1)+" guards cover every non-objective floor");
            var player=(UrbanPlayer)Get(mission,"player");player.Place(building.transform.TransformPoint(new Vector3(0,.3f,0)));
            if(building.Contains(player.transform.position))
            {
                Call(mission,"UpdateObjectiveFloorGuidance");
                Check(((string)Get(mission,"shotNotice")).StartsWith("HQ:") || (string)Get(mission,"shotNotice")=="Already briefed","Missing HQ floor briefing");
                Set(mission,"shotNotice","Already briefed");Call(mission,"UpdateObjectiveFloorGuidance");
                Check((string)Get(mission,"shotNotice")=="Already briefed","Repeated floor briefing");
            }
            return;
        }
        int nearby=0;
        foreach(var guard in (List<EnemyBot>)Get(mission,"enemies"))
        {
            if(guard.Actor.IsDead || (guard.name!="Objective security guard" && guard.name!="Dispatch archive guard") || Vector3.Distance(guard.transform.position,objective)>25)continue;
            if(!UnityEngine.AI.NavMesh.SamplePosition(objective,out var start,2,UnityEngine.AI.NavMesh.AllAreas) ||
                !UnityEngine.AI.NavMesh.SamplePosition(guard.transform.position,out var end,2,UnityEngine.AI.NavMesh.AllAreas))continue;
            var path=new UnityEngine.AI.NavMeshPath();
            if(UnityEngine.AI.NavMesh.CalculatePath(start.position,end.position,UnityEngine.AI.NavMesh.AllAreas,path) && path.status==UnityEngine.AI.NavMeshPathStatus.PathComplete)nearby++;
        }
        Check(nearby>0,"Unguarded or disconnected objective at level "+(level+1));
        report.AppendLine("PASS: level "+(level+1)+" objective has reachable nearby security ("+nearby+")");
    }
    static object Get(object owner,string name)=>owner.GetType().GetField(name,Private).GetValue(owner);
    static void Set(object owner,string name,object value)=>owner.GetType().GetField(name,Private).SetValue(owner,value);
    static void Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Private).Invoke(owner,args);
    static IEnumerator CaptureGame(string name)
    {
        var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        view.position=new Rect(0,0,1280,740);view.Show();
        float settle=Time.realtimeSinceStartup+1.2f;
        while(Time.realtimeSinceStartup<settle){view.Repaint();yield return null;}
        string path=Path.GetFullPath("Logs/"+name+".png");var requested=DateTime.UtcNow;
        ScreenCapture.CaptureScreenshot(path);float deadline=Time.realtimeSinceStartup+15;
        while((!File.Exists(path) || File.GetLastWriteTimeUtc(path)<requested) && Time.realtimeSinceStartup<deadline)yield return null;
        Check(File.Exists(path) && File.GetLastWriteTimeUtc(path)>=requested,"Missing screenshot: "+name);
    }
    static IEnumerator Checks()
    {
        for(int floor=0;floor<6;floor++)
        {
            var radio=Resources.Load<AudioClip>("Audio/Story/Radio_Floor"+floor);
            Check(radio!=null && radio.channels==1 && radio.length>3,"Missing HQ floor audio "+floor);
            Resources.UnloadAsset(radio);
        }
        report.AppendLine("PASS: all six commander floor announcements load as mono audio");
        var keys=new[]{"GeoSniper.SavedLat","GeoSniper.SavedLon","GeoSniper.SavedLabel","GeoSniper.LocationSource","GeoSniper.StoryNarration","GeoSniper.SelectedMode","GeoSniper.ActiveNodeId","GeoSniper.NodeCompleted_0","GeoSniper.RankUpFrom","GeoSniper.RankUpTo","GeoSniper.XP","GeoSniper.PlayerRank","GeoSniper.StarterLegacyMagazine","GeoSniper.StarterLegacyMagazineTier"};
        var saved=new Dictionary<string,string>();var existed=new HashSet<string>();
        foreach(var key in keys){if(PlayerPrefs.HasKey(key))existed.Add(key);saved[key]=key.EndsWith("Lat") || key.EndsWith("Lon")?PlayerPrefs.GetFloat(key).ToString(System.Globalization.CultureInfo.InvariantCulture):key.EndsWith("Label") || key.EndsWith("Source")?PlayerPrefs.GetString(key):PlayerPrefs.GetInt(key).ToString();}
        try
        {
            for(int id=0;id<(SessionState.GetBool("GeoSniper.RetentionOnly",false)?0:50);id++)foreach(string part in new[]{"Mission_","Outcome_"})
            {
                var clip=Resources.Load<AudioClip>("Audio/Story/"+part+id.ToString("00"));Check(clip!=null && clip.channels==1 && clip.length>2,"Missing playable narration "+id+part);
                var importer=(AudioImporter)AssetImporter.GetAtPath("Assets/Resources/Audio/Story/"+part+id.ToString("00")+".wav");Check(importer.defaultSampleSettings.compressionFormat==AudioCompressionFormat.Vorbis,"Speech isn't compressed");Resources.UnloadAsset(clip);
            }
            report.AppendLine(SessionState.GetBool("GeoSniper.RetentionOnly",false)?"SKIP: unchanged narration asset import checks (previous full run passed)":"PASS: all 100 bundled narration clips import as mono compressed speech");
            var game=UnityEngine.Object.FindAnyObjectByType<GeoSniperGame>();Check(game!=null,"Game bootstrap missing");
            StoryNarrator.Enabled=true;game.PlayBriefingAudio(0,true);yield return null;
            Check(StoryNarrator.Playing(game.gameObject),"Briefing isn't playing");game.StopBriefingAudio();Check(!StoryNarrator.Playing(game.gameObject),"Leaving briefing doesn't stop audio");
            StoryNarrator.Enabled=false;game.PlayBriefingAudio(1,true);Check(!StoryNarrator.Playing(game.gameObject),"Mute ignored");StoryNarrator.Enabled=true;
            StoryNarrator.Play(game.gameObject,49,true);yield return null;Check(StoryNarrator.Playing(game.gameObject),"Outcome doesn't play");StoryNarrator.StopOutcome(game.gameObject);Check(!StoryNarrator.Playing(game.gameObject),"Destroyed mission leaves aftermath playing");
            game.PlayBriefingAudio(1,true);StoryNarrator.StopOutcome(game.gameObject);Check(StoryNarrator.Playing(game.gameObject),"Old mission cleanup stops the next briefing");game.StopBriefingAudio();
            report.AppendLine("PASS: briefings and aftermath play; mute and stop release narration");
            foreach(var key in new[]{"GeoSniper.SavedLat","GeoSniper.SavedLon","GeoSniper.SavedLabel","GeoSniper.LocationSource"})PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey("GeoSniper.NodeCompleted_0");
            Set(game,"activeLocation",null);Set(game,"selectedNodeId",0);Call(game,"DeploySelectedStory");
            Check((bool)Get(game,"loading") && !(bool)Get(game,"geographicMap") && !(bool)Get(game,"showingLocationConsent"),"First mission should load offline without a GPS prompt");
            float deadline=Time.realtimeSinceStartup+90;
            while((bool)Get(game,"loading") && Time.realtimeSinceStartup<deadline)yield return null;
            Check((bool)Get(game,"ready"),"First offline deployment did not reach gameplay");
            Check(((UrbanCombatMission)Get(game,"mission")).currentMissionState==MissionState.InProgress,"Starter mission requires another briefing click");
            Check(!(bool)Get(game,"deploymentTracked"),"Successful deployment tracking stayed open");
            report.AppendLine("PASS: first mission reaches playable offline sector without GPS/download or second briefing");
            Call(game,"LoadingFailed",new TimeoutException("simulated deployment timeout"));
            Check(!(bool)Get(game,"ready") && (bool)Get(game,"loadFailed"),"Deployment failure did not return to recovery UI");
            Call(game,"DeployBundledRecovery");
            deadline=Time.realtimeSinceStartup+90;
            while((bool)Get(game,"loading") && Time.realtimeSinceStartup<deadline)yield return null;
            Check((bool)Get(game,"ready") && (int)Get(game,"stageIndexToStart")==0 && (int)Get(game,"campaignNodeToStart")==0,"Bundled recovery lost the selected contract");
            Check(((UrbanCombatMission)Get(game,"mission")).currentMissionState==MissionState.InProgress,"Recovery requires an extra briefing");
            report.AppendLine("PASS: failed deployment recovers into the same playable bundled contract");
            foreach(var building in EnterableBuilding.All)
            {
                int floorSigns=0;
                foreach(var sign in building.GetComponentsInChildren<WorldSign>())
                {
                    if(!sign.Caption.StartsWith("GROUND FLOOR") && !sign.Caption.StartsWith("FLOOR "))continue;
                    floorSigns++;
                    foreach(var collider in sign.GetComponentsInChildren<Collider>())Check(!collider.enabled,"Floor sign blocks movement");
                }
                Check(floorSigns==building.Floors,"Missing floor wayfinding in accessible building");
            }
            report.AppendLine("PASS: every accessible floor has nonblocking wayfinding");
            Physics.SyncTransforms();int traversableWindows=0;
            foreach(var building in EnterableBuilding.All)
                foreach(var window in building.FiringWindows)
                {
                    var center=building.transform.TransformPoint(window);
                    // A standing 1.8m player at jump height must fit across the actual wall opening.
                    if(!Physics.CheckCapsule(center+Vector3.down*.5f,center+Vector3.up*.6f,.35f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))traversableWindows++;
                }
            Check(traversableWindows>20,"Open windows do not clear the jumping player capsule");
            report.AppendLine("PASS: "+traversableWindows+" open windows clear the standing player capsule at jump height");
            yield return TraversalTurnChecks.Windows(((UrbanCombatMission)Get(game,"mission")).Player,line=>report.AppendLine(line));
            TraversalTurnChecks.Weapons(line=>report.AppendLine(line));
            game.ReturnToLobby();yield return null;
            Set(game,"campaignBriefingReviewed",false);Set(game,"campaignLocationEdit",true);
            Call(game,"SelectCampaignPlace",new GameLocation{Latitude=28.6,Longitude=77.2,Label="Test place",Source="search"});
            Check(game.currentTab==GeoSniperGame.LobbyTab.Campaign && (int)Get(game,"selectedNodeId")==0 && (int)Get(game,"stageIndexToStart")==0,"Changing place switches to free roam");
            Check(PlayerPrefs.GetString("GeoSniper.LocationSource")=="selected","Place choice not persisted");
            report.AppendLine("PASS: changing place preserves mission");
            game.StopBriefingAudio();Set(game,"activeLocation",null);
            PlayerPrefs.SetString("GeoSniper.LocationSource","gps");Set(game,"campaignBriefingReviewed",true);Set(game,"readinessContinue",true);
            Call(game,"StartPreferredLocationMission",false);
            Check((bool)Get(game,"loading") && !(bool)Get(game,"showingLocationConsent") && (int)Get(game,"campaignNodeToStart")==0,"Saved GPS choice adds another screen or loses the mission");
            game.StopAllCoroutines();Input.location.Stop();Set(game,"loading",false);Set(game,"campaignBriefingReviewed",false);
            report.AppendLine("PASS: saved GPS choice starts loading without another app confirmation");
            var world=new GameObject("Clean flow sector").AddComponent<SectorWorld>();yield return world.GenerateAsync(SectorMap.Training());Physics.SyncTransforms();
            Set(game,"world",world);Set(game,"geographicMap",false);Set(game,"isPvPDuel",false);Set(game,"rangeToStart",false);Set(game,"stageIndexToStart",0);Set(game,"campaignNodeToStart",0);Set(game,"campaignBriefingReviewed",true);
            Call(game,"StartMission");
            var mission=(UrbanCombatMission)Get(game,"mission");Check(mission.currentMissionState==MissionState.InProgress && !mission.Player.InputBlocked,"Play requires a second briefing click");
            foreach(var enemy in (List<EnemyBot>)Get(mission,"enemies"))Check(!enemy.Suspended,"Direct start leaves targets suspended");
            Check(!(bool)Get(game,"campaignBriefingReviewed"),"Reviewed briefing intent leaked into another mission");
            mission.BeginStage(0);Check(mission.currentMissionState==MissionState.Briefing,"Unreviewed setup state: "+mission.currentMissionState+" / "+Get(mission,"contractSetupError"));mission.StartPreparedCampaign();Check(mission.currentMissionState==MissionState.InProgress,"Prepared mission doesn't start");
            report.AppendLine("PASS: reviewed deployment starts gameplay directly; unreviewed setup retains safe briefing; mission retry also starts");
            PlayerPrefs.SetInt("GeoSniper.NodeCompleted_0",1);
            mission.Player.ToggleScope();
            Call(mission,"TriggerMissionVictory","OPERATION SUCCESSFUL","EXTRACTION CONFIRMED");
            Check(!mission.Player.Scoped && ((Camera)Get(mission,"cameraView")).fieldOfView==65,"Victory retained scope/zoom");
            Call(mission,"CompletePendingVictory");
            Check(mission.currentMissionState==MissionState.Complete,"Victory did not reach results");
            report.AppendLine("PASS: mission victory exits scope and restores normal camera before results");
            Call(mission,"ContinueFromResults",false);yield return null;
            Check(mission.currentMissionState==MissionState.InProgress && (int)Get(mission,"stageIndex")==1 && !mission.Player.InputBlocked,"Next Mission does not start the next contract directly");
            var acoustic=mission.GetComponentInChildren<CampaignAcoustics>();
            Check(acoustic!=null && acoustic.GetComponentsInChildren<AudioSource>().Length==6,"Campaign audio voice pool is not bounded");
            int spatialVoices=0;foreach(var source in acoustic.GetComponentsInChildren<AudioSource>())if(source.spatialBlend==1)spatialVoices++;
            Check(spatialVoices==4,"Campaign impact pool no longer has exactly four spatial voices");
            report.AppendLine("PASS: Next Mission starts the next contract without lobby/loading; campaign uses four pooled impact voices");
            Check(WeaponProgression.Damage(1,0)>=40 && WeaponProgression.Magazine(1,0)==5,"Starter rifle balance incorrect");
            for(int id=0;id<(SessionState.GetBool("GeoSniper.IndoorChecksOnly",false)?0:3);id++)
            {
                mission.BeginStage(id);mission.StartPreparedCampaign();yield return null;
                Check(mission.currentMissionState==MissionState.InProgress,"Opening mission "+id+" isn't playable: "+mission.currentMissionState+" / "+Get(mission,"contractSetupError")+" / "+Get(mission,"missionFailureReason"));
                var focus=(EnemyBot)Get(mission,"contractTarget");
                var vip=(CivilianBot)Get(mission,"contractVIP");
                Vector3 position=focus!=null?focus.transform.position:vip.transform.position;
                bool visible=(bool)typeof(UrbanCombatMission).GetMethod("OpeningSightline",Private).Invoke(mission,new object[]{mission.Player.transform.position,position});
                if(!visible)
                {
                    var eye=mission.Player.transform.position+Vector3.up*1.55f;var aim=position+Vector3.up*1.4f;
                    report.AppendLine("Sightline positions: player="+mission.Player.transform.position+" target="+position);
                    foreach(var obstruction in Physics.RaycastAll(eye,(aim-eye).normalized,Vector3.Distance(eye,aim),Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                        report.AppendLine("Sightline collider="+obstruction.collider.name+" at="+obstruction.point);
                }
                Check(visible,"Opening mission "+id+" has no initial target sightline");
            }
            if(!SessionState.GetBool("GeoSniper.IndoorChecksOnly",false))report.AppendLine("PASS: three opening missions start with visible objectives; starter sniper has 40 damage and five rounds");
            mission.BeginStage(0);mission.StartPreparedCampaign();yield return null;
            Check((bool)Get(mission,"openingRoutesReady"),"Opening mission lacks its rooftop/window approach pair");
            Vector3 openingExit=(Vector3)Get(mission,"openingExit");
            var scout=(EnemyBot)Get(mission,"contractTarget");
            if(SessionState.GetBool("GeoSniper.EnemyScopeCapture",false))
            {
                foreach(var guard in (List<EnemyBot>)Get(mission,"enemies"))guard.Suspended=true;
                mission.Player.Face(scout.transform.position+Vector3.up*1.4f,true);
                Set(mission.Player,"scope",true);MobileCombatInput.ZoomSliderValue=0;
                yield return CaptureGame("ScopeNear");
                MobileCombatInput.ZoomSliderValue=1;yield return CaptureGame("ScopeFar");
                Set(mission.Player,"scope",false);MobileCombatInput.ZoomSliderValue=0;
            }
            scout.Actor.Damage(10000);
            Call(mission,"UpdateOpeningExtraction",1f);
            Check(!(bool)Get(mission,"victoryPending"),"Scout kill completes the mission before extraction");
            Call(mission,"SetWaypoint",openingExit+Vector3.right*20,"OPEN // ENTER");
            object[] exitArguments={Vector3.zero};
            Check((bool)typeof(UrbanCombatMission).GetMethod("TryGetExtractionDestination",Private).Invoke(mission,exitArguments)
                && (Vector3)exitArguments[0]==openingExit,"Finding a building overwrites extraction destination");
            if(SessionState.GetBool("GeoSniper.EnemyScopeCapture",false))
            {Set(mission,"mapOpen",true);yield return CaptureGame("ExtractionMap");Set(mission,"mapOpen",false);}
            ((WaypointPin)Get(mission,"waypointPin")).Clear();exitArguments[0]=Vector3.zero;
            Check((bool)typeof(UrbanCombatMission).GetMethod("TryGetExtractionDestination",Private).Invoke(mission,exitArguments),"Clearing a manual pin removes extraction");
            Call(mission,"RestoreExtractionGuidance");
            Check(((WaypointPin)Get(mission,"waypointPin")).Position==openingExit,"Extraction button cannot restore route");
            report.AppendLine("PASS: extraction remains available after a building pin or cleared manual pin; extraction button restores it");
            foreach(var guard in UnityEngine.Object.FindObjectsByType<EnemyBot>(FindObjectsSortMode.None))guard.Suspended=true;
            mission.Player.Place(openingExit);Call(mission,"UpdateOpeningExtraction",2f);
            Check((float)Get(mission,"openingExitHold")>=2,"Extraction hold doesn't progress inside the beacon");
            mission.Player.Place(openingExit+Vector3.right*5);Call(mission,"UpdateOpeningExtraction",1f);
            Check((float)Get(mission,"openingExitHold")==0,"Extraction progress doesn't reset outside");
            mission.Player.Place(openingExit);Call(mission,"UpdateOpeningExtraction",4.1f);
            Check((bool)Get(mission,"victoryPending"),"Opening extraction does not finish after its hold");
            report.AppendLine("PASS: opening mission has rooftop/window choices, scout kill requires extraction, and four-second hold resets outside");
            if(SessionState.GetBool("GeoSniper.ScopeCaptureOnly",false))
            {SessionState.SetBool("GeoSniper.ScopeCaptureOnly",false);yield break;}
            if(SessionState.GetBool("GeoSniper.LeakChecks",false))
            {
                mission.BeginStage(20);mission.StartPreparedCampaign();yield return null;
                Check(mission.currentMissionState==MissionState.InProgress,"The Leak setup failed: "+Get(mission,"contractSetupError"));
                var points=(IList)Get(mission,"storyPoints");Check(points.Count==2,"The Leak must have two dispatch archives");
                var first=(Vector3)points[0].GetType().GetField("Position").GetValue(points[0]);
                var second=(Vector3)points[1].GetType().GetField("Position").GetValue(points[1]);
                Check(Vector3.Distance(first,second)>=30,"Leak archives still clustered together");
                foreach(var point in points)
                {
                    var visual=(GameObject)point.GetType().GetField("Visual").GetValue(point);
                    Check(visual.GetComponentInChildren<MeshRenderer>()!=null && visual.transform.Find("Printed dispatch document")!=null,"Evidence isn't a document folder");
                    Check(Physics.Raycast(visual.transform.position+Vector3.up*.05f,Vector3.down,out var desk,2) && desk.collider.GetComponentInParent<EnterableBuilding>()!=null,"Document isn't on a desk inside a building");
                }
                var hostiles=(List<EnemyBot>)Get(mission,"enemies");int guards=0;
                foreach(var bot in hostiles){if(bot.name=="Dispatch archive guard")guards++;bot.Suspended=true;}
                Check(guards>=2,"No guards protecting dispatch archives");
                int before=hostiles.Count;Call(mission,"LeakRecovered",first);Call(mission,"LeakRecovered",second);Call(mission,"LeakRecovered",second);
                Check((int)Get(mission,"leakResponses")==2 && hostiles.Count<=before+4,"Leak response is unbounded");
                foreach(var bot in hostiles)bot.Suspended=true;
                foreach(var point in points)point.GetType().GetField("Done").SetValue(point,true);
                Set(mission,"storyCompleted",2);var exit=(Vector3)Get(mission,"extractionPoint");mission.Player.Health.Invulnerable=true;
                mission.Player.Place(exit);Call(mission,"UpdateStoryField",1f);
                Check(!(bool)Get(mission,"victoryPending"),"Leak extraction wins instantly");
                mission.Player.Place(exit+Vector3.forward*8);Call(mission,"UpdateStoryField",1f);Check((float)Get(mission,"leakExtractionHold")==0,"Leaving extraction doesn't reset hold");
                mission.Player.Place(exit);Call(mission,"UpdateStoryField",11f);Check(!(bool)Get(mission,"victoryPending"),"Leak hold is shorter than twelve seconds");
                Call(mission,"UpdateStoryField",1f);Check((bool)Get(mission,"victoryPending"),"Twelve-second extraction doesn't complete");
                for(int level=0;level<50;level++)
                {
                    var type=CampaignStory.Get(level).type;
                    mission.BeginStage(level);mission.StartPreparedCampaign();yield return null;
                    Check(mission.currentMissionState==MissionState.InProgress,"Indoor operation setup failed at level "+(level+1)+": "+Get(mission,"contractSetupError"));
                    if(type=="Rescue")
                    {
                        var witness=(CivilianBot)Get(mission,"contractVIP");
                        CheckObjectiveSecurity(mission,witness.transform.position,level);
                    }
                    if(type!="EvidenceRecovery" && type!="Sabotage"){report.AppendLine("PASS: level "+(level+1)+" // "+type+" playable setup");continue;}
                    var objectives=(IList)Get(mission,"storyPoints");
                    var objectiveContainer=(GameObject)Get(mission,"storyObjects");
                    int roomSigns=0;
                    foreach(var sign in objectiveContainer.GetComponentsInChildren<WorldSign>())
                        if(sign.Caption.StartsWith(type=="Sabotage"?"ELECTRICAL CONTROL":"DISPATCH ARCHIVE"))roomSigns++;
                    Check(roomSigns==objectives.Count,"Objective room identity missing at level "+(level+1));
                    Check(objectives.Count==Mathf.Clamp(CampaignStory.Get(level).interactions,1,3),"Wrong objective count at level "+(level+1));
                    foreach(var objective in objectives)
                        CheckObjectiveSecurity(mission,(Vector3)objective.GetType().GetField("Position").GetValue(objective),level);
                    if(level==4)
                    {
                        var changedFloors=new Dictionary<EnterableBuilding,int>();
                        foreach(var objective in objectives)
                        {
                            var building=(EnterableBuilding)objective.GetType().GetField("Building").GetValue(objective);
                            if(!changedFloors.ContainsKey(building)){changedFloors[building]=building.Floors;building.Floors=1;}
                        }
                        try
                        {
                            Call(mission,"PrepareObjectiveGuards",typeof(UrbanCombatMission).GetMethod("GetInnerGroundPoints",Private).Invoke(mission,null));
                            Check(Get(mission,"contractSetupError")==null,"Single-storey archive rejects every guard approach");
                            report.AppendLine("PASS: single-storey archive accepts supported guard approaches without impossible floor exclusions");
                        }
                        finally{foreach(var pair in changedFloors)pair.Key.Floors=pair.Value;}
                    }
                    foreach(var enemy in (List<EnemyBot>)Get(mission,"enemies"))enemy.Suspended=true;
                    mission.Player.Health.Invulnerable=true;
                    var point=objectives[0];var root=(GameObject)point.GetType().GetField("Visual").GetValue(point);
                    var standing=(Vector3)point.GetType().GetField("Position").GetValue(point);
                    mission.Player.Place(standing);Physics.SyncTransforms();
                    var view=(Camera)Get(mission,"cameraView");view.transform.LookAt(root.transform.position);
                    Check((bool)typeof(UrbanCombatMission).GetMethod("StoryPointInReach",Private).Invoke(mission,new[]{point}),"Indoor action blocked at level "+(level+1));
                    if(level==4 || level==5)
                    {
                        CaptureObjective(view,level==4?"IndoorEvidence.png":"IndoorRelay.png");
                        if(level==4)
                        {
                            var aim=view.transform.rotation;view.transform.LookAt(root.transform.position+Vector3.up*.5f);
                            CaptureObjective(view,"InteriorRoom.png");view.transform.rotation=aim;
                            foreach(string material in new[]{"Plaster","StoneTile","Oak"})
                                Check(Resources.Load<Texture2D>("Textures/Interiors/"+material)!=null,"Missing interior surface "+material);
                            var storyRoot=(GameObject)Get(mission,"storyObjects");
                            Check(storyRoot.transform.Find("Portable extraction signal/Green extraction lamp")!=null,"Extraction signal asset missing");
                        }
                    }
                    if(level==4)
                    {
                        var acoustics=(CampaignAcoustics)Get(mission,"campaignAcoustics");
                        var city=(AudioSource)Get(acoustics,"citySource");var room=(AudioSource)Get(acoustics,"roomSource");
                        var sources=acoustics.GetComponentsInChildren<AudioSource>();
                        Check(sources.Length==6 && city.clip.length==12 && room.clip.length==12,"Atmosphere voices or loops are unbounded");
                        var cityClip=city.clip;acoustics.Initialize();Check(city.clip==cityClip && acoustics.GetComponentsInChildren<AudioSource>().Length==6,"Atmosphere reinitialization duplicates clips/sources");
                        for(int i=0;i<120;i++)acoustics.UpdateEnvironment(mission.Player,true,false,false);
                        Check(room.volume>city.volume,"Entering a building does not crossfade to room tone");
                        float audible=room.volume;for(int i=0;i<120;i++)acoustics.UpdateEnvironment(mission.Player,true,true,false);
                        Check(room.volume<audible,"Map/pause does not fade the environment bed");
                        CampaignAcoustics.SetMuted(true);Check(city.mute && room.mute,"Mute misses the environment bed");CampaignAcoustics.SetMuted(false);
                        StoryNarrator.Stop(mission.gameObject);Set(mission,"earlyFieldHintSeen",false);Set(mission,"shotNoticeTime",0f);Set(mission,"missionDuration",11f);
                        Call(mission,"TickEarlyFieldHint");Check(((string)Get(mission,"shotNotice")).Contains("RECOVER BOTH COPIES"),"Early document mission has no contextual guidance");
                        Set(mission,"shotNotice","Already coached");Call(mission,"TickEarlyFieldHint");Check((string)Get(mission,"shotNotice")=="Already coached","Contextual guidance repeats");Set(mission,"missionDuration",0f);
                        report.AppendLine("PASS: cached atmosphere crossfades indoors, fades while blocked, obeys mute; document guidance is contextual and one-shot");
                    }
                    var rest=(Vector3)point.GetType().GetField("RestPosition").GetValue(point);
                    Call(mission,"AnimateStoryInteraction",point,.85f);
                    if(type=="Sabotage")Check(root.transform.Find("Power indicator").GetComponent<Renderer>().sharedMaterial.color.r>.8f,"Relay lacks its power-isolation phase");
                    else Check(root.transform.position.y>rest.y+.1f,"Document pickup has no visible lift");
                    Check((bool)typeof(UrbanCombatMission).GetMethod("StoryPointInReach",Private).Invoke(mission,new[]{point}),"Interaction animation changes the reach test");
                    Call(mission,"AnimateStoryInteraction",point,0f);
                    Check(Vector3.Distance(root.transform.position,rest)<.001f,"Interrupted document animation doesn't return to desk");
                    if(level==4 || level==5)
                    {
                        Set(mission,"storyUseUntil",Time.unscaledTime+10);Call(mission,"UpdateStoryField",.3f);
                        mission.Player.Health.Invulnerable=false;mission.Player.Health.Damage(1);mission.Player.Health.Invulnerable=true;
                        Call(mission,"UpdateStoryField",.1f);
                        Check((float)Get(mission,"storyHold")==0 && (float)Get(mission,"storyUseUntil")==0,"Incoming fire does not interrupt field action");
                    }
                    Set(mission,"storyUseUntil",Time.unscaledTime+10);Call(mission,"UpdateStoryField",5f);
                    Check((bool)point.GetType().GetField("Done").GetValue(point),"Action did not complete at level "+(level+1));
                    if(type=="Sabotage")
                    {
                        Check(root.activeSelf && root.transform.Find("Disconnect lever")!=null && root.transform.Find("Power indicator").GetComponent<Renderer>().sharedMaterial.color.g<.2f,"Relay shutdown state missing");
                        mission.Player.Place((Vector3)Get(mission,"extractionPoint"));Call(mission,"UpdateSabotageExtraction",0f);Call(mission,"UpdateSabotageExtraction",2f);
                        Check((float)Get(mission,"sabotageExitHold")>=2,"Relay extraction has no defended hold");
                        mission.Player.Place(standing);Call(mission,"UpdateSabotageExtraction",.1f);
                        Check((float)Get(mission,"sabotageExitHold")==0,"Relay extraction hold survives leaving zone");
                    }
                    else Check(!root.activeSelf,"Recovered documents remain on desk");
                    report.AppendLine("PASS: level "+(level+1)+" // "+type+" indoor placement, animated phases and action");
                }
                mission.BeginStage(2);mission.StartPreparedCampaign();
                SessionState.SetBool("GeoSniper.LeakChecks",false);
                report.AppendLine("PASS: The Leak has separated indoor documents on desks, archive guards, bounded counterattacks, and a twelve-second extraction that resets outside the zone");
            }
            PlayerPrefs.SetInt("GeoSniper.RankUpFrom",4);PlayerPrefs.SetInt("GeoSniper.RankUpTo",5);
            Check(!(bool)typeof(GeoSniperGame).GetProperty("EconomyModalOpen",Private).GetValue(game),"Rank-up still blocks navigation");
            Check(!CampaignAdPolicy.AllowsBreak(2,50,1000) && !CampaignAdPolicy.AllowsBreak(3,3,299) && CampaignAdPolicy.AllowsBreak(3,3,300),"Opening ad protection boundary incorrect");
            PlayerPrefs.SetInt("GeoSniper.StarterLegacyMagazine",25);PlayerPrefs.SetInt("GeoSniper.StarterLegacyMagazineTier",1);
            Check(WeaponProgression.Magazine(1,1)==25 && WeaponProgression.Magazine(1,2)==26,"Previously bought magazine capacity or new upgrade lost");
            report.AppendLine("PASS: rank-up is nonblocking; early ad protection and purchased magazine migration preserve progression");
            PlayerPrefs.SetInt("GeoSniper.XP",3000);PlayerPrefs.SetInt("GeoSniper.PlayerRank",4);
            PlayerPrefs.SetInt("GeoSniper.RankUpFrom",3);PlayerPrefs.SetInt("GeoSniper.RankUpTo",4);
            Set(mission,"rewardCash",390);Set(mission,"rewardXP",196);Set(mission,"totalShotsFired",3);Set(mission,"totalHitsScored",3);Set(mission,"totalHeadshotsScored",2);
            mission.Player.ToggleScope();
            Call(mission,"TriggerMissionVictory","OPERATION SUCCESSFUL","EXTRACTION CONFIRMED");
            Check(!mission.Player.Scoped && ((Camera)Get(mission,"cameraView")).fieldOfView==65,"Victory retained scope/zoom");
            Call(mission,"CompletePendingVictory");
            Check(mission.currentMissionState==MissionState.Complete,"Victory did not reach results");
            report.AppendLine("PASS: mission victory exits scope and restores normal camera before results");
            var gameView=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            foreach(bool compact in new[]{false,true})
            {
                gameView.position=compact?new Rect(0,0,800,620):new Rect(0,0,1280,740);gameView.Show();
                float settle=Time.realtimeSinceStartup+2;while(Time.realtimeSinceStartup<settle){gameView.Repaint();yield return null;}
                string path=Path.GetFullPath("Logs/RetentionResults"+(compact?"Compact":"Wide")+".png");Directory.CreateDirectory("Logs");
                var requestedAt=DateTime.UtcNow;ScreenCapture.CaptureScreenshot(path);
                float captureDeadline=Time.realtimeSinceStartup+15;
                while((!File.Exists(path) || File.GetLastWriteTimeUtc(path)<requestedAt) && Time.realtimeSinceStartup<captureDeadline)yield return null;
                Check(File.Exists(path) && File.GetLastWriteTimeUtc(path)>=requestedAt,"Results capture missing");
            }
            report.AppendLine("PASS: consolidated results captured at wide and compact sizes");


        }
        finally
        {
            foreach(var key in keys)
            {
                if(!existed.Contains(key))PlayerPrefs.DeleteKey(key);
                else if(key.EndsWith("Lat") || key.EndsWith("Lon"))PlayerPrefs.SetFloat(key,float.Parse(saved[key],System.Globalization.CultureInfo.InvariantCulture));
                else if(key.EndsWith("Label") || key.EndsWith("Source"))PlayerPrefs.SetString(key,saved[key]);else PlayerPrefs.SetInt(key,int.Parse(saved[key]));
            }
            PlayerPrefs.Save();
        }
    }
}
