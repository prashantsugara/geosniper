using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    public sealed partial class UrbanCombatMission
    {
        FirstContactContract firstContact;
        EnemyBot contractTarget;
        CombatActor contractTargetHealth;
        readonly List<Vector3> contractVantages=new List<Vector3>();
        readonly List<string> contractVantageNames=new List<string>();
        Vector3 contractInsertion;
        string contractSetupError;
        bool contractObservedDetection;
        Material contractExtractionMaterial;
        bool FirstContactMode => stageMode && stageIndex==0 && firstContact!=null;

        void BeginFirstContact()
        {
            CloseMap();firstContact=new FirstContactContract();contractSetupError=null;
            contractTarget=null;contractTargetHealth=null;contractVantages.Clear();contractVantageNames.Clear();
            contractObservedDetection=false;hasWaypointPin=false;
            currentMissionState=MissionState.Briefing;stageRewarded=false;defeated=0;stageTarget=1;
            reloadTimer=0;fireCooldown=.5f;ammo=weapon!=null?weapon.MaxAmmo:5;
            LoadDailyContract();
            foreach(var bot in enemies) if(bot!=null) {bot.gameObject.SetActive(false);Destroy(bot.gameObject);}
            foreach(var civilian in civilians) if(civilian!=null) {civilian.gameObject.SetActive(false);Destroy(civilian.gameObject);}
            enemies.Clear();civilians.Clear();countedDeaths.Clear();
            if(extractionMarker!=null) Destroy(extractionMarker);
            if(contractExtractionMaterial!=null) Destroy(contractExtractionMaterial);
            if (player != null)
            {
                if (player.Health != null) { player.Health.Initialize(100); player.Health.Invulnerable = false; }
                player.InputBlocked = true;
                if (player.Scoped) player.ToggleScope();
            }
            Physics.SyncTransforms();
            var body = player != null ? player.GetComponent<CharacterController>() : null;
            if (body != null) body.enabled = false;
            var candidates = new List<Vector3>();
            try
            {
                var raw = new List<Vector3>();
                if (mapSpawns != null) raw.AddRange(mapSpawns);
                AddNearbyGrassPatrols(raw);
                Vector3 pPos = player != null ? player.transform.position : Vector3.zero;
                raw.Sort((a, b) => FlatDistance(a, pPos).CompareTo(FlatDistance(b, pPos)));
                foreach (var sample in raw)
                {
                    if (candidates.Count >= 220) break;
                    if (FlatDistance(sample, pPos) > 240 || !ContractGround(sample, out var ground)) continue;
                    if (!candidates.Exists(p => FlatDistance(p, ground) < 5)) candidates.Add(ground);
                }
                if (candidates.Count == 0) { ContractUnavailable(); return; }
                contractInsertion = candidates[0];
                if (player != null) player.Place(contractInsertion);
                if (body != null) body.enabled = false;
                // Build a walking graph over actual street samples, checking body clearance
                // and support under each segment. The return route is the insertion point.
                var reachable=new List<Vector3>{contractInsertion};
                for(int cursor=0;cursor<reachable.Count;cursor++)
                {
                    for(int i=candidates.Count-1;i>=0;i--)
                    {
                        var p=candidates[i];
                        if(FlatDistance(reachable[cursor],p)>38 || !ContractWalkable(reachable[cursor],p)) continue;
                        if(!reachable.Exists(q=>FlatDistance(q,p)<3)) reachable.Add(p);
                        candidates.RemoveAt(i);
                    }
                }
                var vantages=new List<Vector3>(reachable);
                var names=new List<string>();foreach(var p in reachable) names.Add("Street position");
                // Roof choices are offered only when their ladder starts on a reachable street.
                foreach(var ladder in SectorWorld.Ladders)
                {
                    if(ladder.Owner==null || FlatDistance(ladder.Bottom,contractInsertion)>180) continue;
                    if(!reachable.Exists(p=>FlatDistance(p,ladder.Bottom)<16 && ContractWalkable(p,ladder.Bottom))) continue;
                    if(!ContractClearance(ladder.Landing)) continue;
                    vantages.Add(ladder.Landing);names.Add("Rooftop via ladder");
                }
                Vector3 target=default;
                var guards=new List<Vector3>();
                bool found=false;
                reachable.Sort((a,b)=>Mathf.Abs(FlatDistance(a,contractInsertion)-100).CompareTo(Mathf.Abs(FlatDistance(b,contractInsertion)-100)));
                foreach(var candidate in reachable)
                {
                    float distance=FlatDistance(candidate,contractInsertion);
                    if(distance<55 || distance>170) continue;
                    contractVantages.Clear();contractVantageNames.Clear();guards.Clear();
                    // Prefer a reachable roof when available, then distinct street angles.
                    for(int i=vantages.Count-1;i>=0 && contractVantages.Count<3;i--)
                    {
                        var vantage=vantages[i];
                        if(FlatDistance(vantage,contractInsertion)>180 || !ClearSniperTarget(vantage,candidate)) continue;
                        if(contractVantages.Exists(p=>FlatDistance(p,vantage)<18)) continue;
                        contractVantages.Add(vantage);contractVantageNames.Add(names[i]);
                    }
                    if(contractVantages.Count<3) continue;
                    foreach(var p in reachable)
                    {
                        if(guards.Count==2) break;
                        float d=FlatDistance(p,candidate);
                        if(d<10 || d>40 || FlatDistance(p,contractInsertion)<35 || guards.Exists(q=>FlatDistance(q,p)<12)) continue;
                        guards.Add(p);
                    }
                    if(guards.Count<2) continue;
                    target=candidate;found=true;break;
                }
                if(!found && reachable.Count > 0)
                {
                    target = reachable[reachable.Count / 2];
                    if (contractVantages.Count == 0)
                    {
                        contractVantages.Add(contractInsertion);
                        contractVantageNames.Add("Insertion Point");
                    }
                    while (guards.Count < 2)
                    {
                        Vector3 gPos = target + new Vector3(guards.Count == 0 ? 12f : -12f, 0, 8f);
                        if (Physics.Raycast(gPos + Vector3.up * 10f, Vector3.down, out var gHit, 20f, ~0, QueryTriggerInteraction.Ignore))
                            gPos = gHit.point + Vector3.up * 0.1f;
                        guards.Add(gPos);
                    }
                    found = true;
                }
                if(!found) {ContractUnavailable();return;}
                SpawnEnemy(0,target);contractTarget=enemies[0];contractTarget.name="First Contact - marked target";
                contractTargetHealth=contractTarget.GetComponent<CombatActor>();
                contractTarget.speed=1.2f;contractTarget.patrolRadius=2;contractTarget.detectionRange=65;contractTarget.engagementRange=60;
                contractTarget.InitializePatrol();contractTarget.Suspended=true;
                foreach(var p in guards)
                {
                    SpawnEnemy(enemies.Count,p);var guard=enemies[enemies.Count-1];
                    guard.speed=2.1f;guard.patrolRadius=9;guard.detectionRange=75;guard.engagementRange=60;
                    guard.InitializePatrol();guard.Suspended=true;
                }
                player.Face(target);
                extractionPoint=contractInsertion;
                SetWaypoint(contractVantages[0],"Position A");
                EnsureMissionBillboard();
            }
            finally {body.enabled=true;}
        }
        void ContractUnavailable()
        {
            contractVantages.Clear();contractVantageNames.Clear();
            Debug.LogWarning("[GeoSniper] First Contact fallback: starting free roam tactical combat.");
            currentMissionState = MissionState.InProgress;
            if (player != null) { player.InputBlocked = false; player.Health.Invulnerable = true; }
            SpawnFreeRoamPatrols();
        }
        bool ContractGround(Vector3 sample,out Vector3 ground)
        {
            ground=default;
            if(!SectorWorld.DryFootprint(sample,.6f))return false;
            if(!Physics.Raycast(sample+Vector3.up*3,Vector3.down,out var hit,8,~0,QueryTriggerInteraction.Ignore) || hit.normal.y<.85f) return false;
            string surface=hit.collider.name;
            if(surface!="Local terrain" && surface!="Sloped road" && surface!="park" && surface!="Street landing") return false;
            ground=hit.point+Vector3.up*.12f;
            return SectorWorld.DryFootprint(ground,.6f) && ContractClearance(ground);
        }
        bool ContractClearance(Vector3 point) => !Physics.CheckCapsule(point+Vector3.up*.45f,point+Vector3.up*1.5f,.34f,~0,QueryTriggerInteraction.Ignore);
        bool ContractWalkable(Vector3 a,Vector3 b)
        {
            var delta=b-a;float length=delta.magnitude;
            if(length<.5f) return true;
            if(Physics.SphereCast(a+Vector3.up*.95f,.34f,delta/length,out _,length,~0,QueryTriggerInteraction.Ignore)) return false;
            int steps=Mathf.CeilToInt(length/2);
            Vector3 previous=a;
            for(int i=1;i<=steps;i++)
            {
                if(!ContractGround(Vector3.Lerp(a,b,i/(float)steps),out var ground) || Mathf.Abs(ground.y-previous.y)>.5f) return false;
                previous=ground;
            }
            return true;
        }
        void StartFirstContact()
        {
            if(firstContact==null || contractSetupError!=null) return;
            firstContact.Start();currentMissionState=MissionState.InProgress;player.InputBlocked=false;
            PlayHQDispatchSound();
            foreach(var bot in enemies) if(bot!=null) bot.Suspended=false;
        }
        void UpdateFirstContact()
        {
            if(firstContact==null || !firstContact.Running) return;
            if(!mapOpen)
            {
                foreach(var bot in enemies)
                    if(bot!=null && !bot.GetComponent<CombatActor>().IsDead && (bot.currentState==AIState.Combat || bot.currentState==AIState.TakeCover)) contractObservedDetection=true;
                foreach(var p in contractVantages)
                    if(FlatDistance(player.transform.position,p)<5 && Mathf.Abs(player.transform.position.y-p.y)<3) firstContact.Scout();
                if(firstContact.Phase==ContractPhase.Active)
                {
                    if(contractTargetHealth!=null && contractTargetHealth.IsDead)
                    {
                        firstContact.EliminateTarget();
                        SetWaypoint(extractionPoint,"Extraction");
                        ShowContractExtraction();shotNotice="TARGET DOWN - REACH EXTRACTION";shotNoticeTime=4;
                    }
                    else if(contractTarget==null) firstContact.Fail("The target left the loaded area. Retry the contract.");
                }
            }
            bool inside=FlatDistance(player.transform.position,extractionPoint)<5 && Mathf.Abs(player.transform.position.y-extractionPoint.y)<2;
            firstContact.Tick(Time.deltaTime,mapOpen,player.Health.IsDead,contractObservedDetection,inside);
            currentMissionState=firstContact.Phase==ContractPhase.Extracting?MissionState.Extraction:
                firstContact.Phase==ContractPhase.Complete?MissionState.Complete:
                firstContact.Phase==ContractPhase.Failed?MissionState.Failed:MissionState.InProgress;
            if(currentMissionState==MissionState.Complete) {hasWaypointPin=false;RewardStage();}
        }
        void ShowContractExtraction()
        {
            if(extractionMarker!=null) Destroy(extractionMarker);
            extractionMarker=new GameObject("First Contact extraction");
            extractionMarker.transform.position=extractionPoint-Vector3.up*.08f;
            var mf = extractionMarker.AddComponent<MeshFilter>();
            var mr = extractionMarker.AddComponent<MeshRenderer>();
            Mesh discMesh = new Mesh();
            int segments = 24;
            Vector3[] verts = new Vector3[segments + 1];
            int[] tris = new int[segments * 3];
            verts[0] = Vector3.zero;
            for (int s = 0; s < segments; s++)
            {
                float angle = s * Mathf.PI * 2f / segments;
                verts[s + 1] = new Vector3(Mathf.Cos(angle) * 5f, 0, Mathf.Sin(angle) * 5f);
                tris[s * 3] = 0; tris[s * 3 + 1] = s + 1; tris[s * 3 + 2] = (s == segments - 1) ? 1 : (s + 2);
            }
            discMesh.vertices = verts; discMesh.triangles = tris; discMesh.RecalculateNormals();
            mf.sharedMesh = discMesh;
            contractExtractionMaterial=RuntimeMaterial("Extraction zone",new Color(.12f,.60f,.32f),0);
            mr.sharedMaterial=contractExtractionMaterial;
        }
        string FirstContactObjective()
        {
            if(firstContact==null) return "FIRST CONTACT";
            int seconds=Mathf.CeilToInt(firstContact.Remaining);
            string task=firstContact.TargetDown?"EXTRACT - RETURN TO INSERTION":"ELIMINATE THE MARKED TARGET";
            return "FIRST CONTACT  |  "+task+"  |  "+seconds/60+":"+(seconds%60).ToString("00")+"  |  "+(firstContact.Detected?"ALARM RAISED":"UNDETECTED");
        }
        void DrawFirstContactHUD(float width,float height)
        {
            if(firstContact==null) return;
            var body=new GUIStyle(GUI.skin.label){fontSize=17,wordWrap=true,alignment=TextAnchor.MiddleCenter};
            var button=new GUIStyle(GUI.skin.button){fontSize=16};
            if(firstContact.Phase==ContractPhase.Briefing || firstContact.Phase==ContractPhase.Complete || firstContact.Phase==ContractPhase.Failed)
            {
                float w=Mathf.Min(560,width-40),h=Mathf.Min(340,height-40);
                var box=new Rect((width-w)/2,(height-h)/2,w,h);
                TacticalGUI.DrawPanel(box,TacticalGUI.ThemeCard,TacticalGUI.ThemeBorder,2);
                string title="FIRST CONTACT";
                string message="Five minutes. One marked target. Two patrol guards.\n\nScout from any of the three marked positions. Eliminate the target, then return to insertion and hold for 2 seconds.\n\nMap: M  |  Scope: right mouse  |  Fire: left mouse\nMap pauses the contract. Patrol kills are optional.";
                if(firstContact.Phase==ContractPhase.Complete)
                {
                    title="CONTRACT COMPLETE";
                    message="Target eliminated. Extraction confirmed.\n\nAccuracy: "+firstContact.Accuracy+"%  |  Shots: "+firstContact.Shots+
                        "\nScouted: "+(firstContact.Scouted?"Yes":"No")+"  |  Undetected: "+(!firstContact.Detected?"Yes":"No")+
                        "\n\nEarned "+(100+firstContact.BonusXP)+" XP and "+(25+firstContact.BonusCredits)+" credits.";
                }
                else if(firstContact.Phase==ContractPhase.Failed) {title="CONTRACT FAILED";message=firstContact.Failure;}
                GUI.Label(new Rect(box.x+16,box.y+16,w-32,32),title,new GUIStyle(body){fontSize=23,fontStyle=FontStyle.Bold});
                GUI.Label(new Rect(box.x+24,box.y+60,w-48,h-134),message,body);
                if(firstContact.Phase==ContractPhase.Briefing)
                {if(GUI.Button(new Rect(box.center.x-110,box.yMax-64,220,48),"START CONTRACT",button)) StartFirstContact();}
                else
                {
                    if(GUI.Button(new Rect(box.x+24,box.yMax-64,w/2-32,48),"RETRY CONTRACT",button)) BeginStage(0);
                    string exitText = firstContact.Phase == ContractPhase.Complete ? "LEVEL SCREEN ▶" : "EXIT TO MAP";
                    if(GUI.Button(new Rect(box.center.x+8,box.yMax-64,w/2-32,48),exitText,button)) ExitToLevelMap();
                }
                return;
            }
            var statusStyle=new GUIStyle(GUI.skin.box){fontSize=13,alignment=TextAnchor.MiddleLeft};
            GUI.Box(new Rect(15,122,260,32),firstContact.Scouted?"SCOUTED  |  Choose your shot":"OPTIONAL: SCOUT A MARKED POSITION",statusStyle);
            for(int i=0;i<contractVantages.Count && !firstContact.TargetDown;i++)
                if(GUI.Button(new Rect(15,160+i*46,260,42),"POSITION "+(char)('A'+i)+" - "+contractVantageNames[i],button))
                {SetWaypoint(contractVantages[i],"Position "+(char)('A'+i));}
            if(firstContact.TargetDown) GUI.Box(new Rect(15,160,260,50),"EXTRACTION\nHold "+firstContact.ExtractionHold.ToString("0.0")+" / 2.0 seconds",statusStyle);
        }
        void LeaveFirstContact()
        {
            stageMode=false;freeRoam=true;firstContact=null;currentMissionState=MissionState.None;
            hasWaypointPin=false;player.Health.Initialize(100);player.Health.Invulnerable=true;player.InputBlocked=false;
            foreach(var enemy in enemies) if(enemy!=null) {enemy.gameObject.SetActive(false);Destroy(enemy.gameObject);}
            enemies.Clear();countedDeaths.Clear();if(extractionMarker!=null) Destroy(extractionMarker);
            SpawnFreeRoamPatrols();
        }
        void DrawFirstContactMap(StreetMapViewport view)
        {
            if(!FirstContactMode || firstContact==null) return;
            for(int i=0;i<contractVantages.Count;i++)
                DrawContractPin(view,contractVantages[i],"POSITION "+(char)('A'+i),new Color(.16f,.38f,.78f));
            if(contractTarget!=null && !firstContact.TargetDown)
                DrawContractPin(view,contractTarget.transform.position,"MARKED TARGET",new Color(.78f,.36f,.02f));
            DrawContractPin(view,extractionPoint,"INSERTION / EXTRACTION",new Color(.13f,.48f,.26f));
        }
        void DrawContractPin(StreetMapViewport view,Vector3 point,string text,Color color)
        {
            var p=view.Project(point);
            MapFill(new Rect(p.x-7,p.y-7,14,14),Color.white);MapFill(new Rect(p.x-5,p.y-5,10,10),color);
            var area=new Rect(p.x+10,p.y-12,195,25);MapFill(area,new Color(1,1,1,.95f));
            GUI.Label(area,text,MapText(13,color,true));
        }
    }
}
