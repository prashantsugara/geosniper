using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
namespace GeoSniper
{
    public sealed partial class UrbanCombatMission
    {
        bool LeakOperation=>stageMode && campaignNode!=null && campaignNode.title=="THE LEAK";
        float leakExtractionHold,leakPreviousHealth,leakInteractionBlockedUntil;
        int leakResponses;
        const float LeakRecoverySeconds=4f,LeakExtractionSeconds=12f;
        float StoryRecoverySeconds=>LeakOperation?LeakRecoverySeconds:activeContract.Type==CampaignContractType.Sabotage || activeContract.Type==CampaignContractType.Rescue?3f:1.5f;
        void ResetLeakOperation(){leakExtractionHold=0;leakResponses=0;leakPreviousHealth=100;leakInteractionBlockedUntil=0;}
        void PrepareObjectiveGuards(List<Vector3> ground)
        {
            var assigned=new HashSet<EnemyBot>();var positions=new List<Vector3>();
            var securityPoints=new List<StoryPoint>();
            var securedBuildings=new HashSet<EnterableBuilding>();
            foreach(var task in storyPoints)
            {
                if(task.Building==null || task.Building.Floors<=1){securityPoints.Add(task);continue;}
                if(!securedBuildings.Add(task.Building))continue;
                for(int floor=0;floor<task.Building.Floors;floor++)
                {
                    bool objectiveFloor=storyPoints.Exists(p=>p.Building==task.Building && task.Building.FloorAt(p.Position)==floor);
                    if(objectiveFloor)continue;
                    var local=task.Building.transform.InverseTransformPoint(task.Position);local.y=floor*task.Building.FloorHeight+.08f;
                    securityPoints.Add(new StoryPoint{Position=task.Building.transform.TransformPoint(local),Building=task.Building});
                }
            }
            foreach(var objective in securityPoints)
            {
                var candidates=new List<Vector3>(ground);
                // Guard the approach floors; the archive/control room remains a separate destination.
                foreach(float radius in new[]{3f,5f,8f,12f})for(int bearing=0;bearing<16;bearing++)
                {
                    var offset=Quaternion.Euler(0,bearing*22.5f,0)*Vector3.forward*radius;
                    if(NavMesh.SamplePosition(objective.Position+offset,out var indoorHit,.6f,NavMesh.AllAreas) &&
                        Mathf.Abs(indoorHit.position.y-objective.Position.y)<1 && SupportedStandingPoint(indoorHit.position))candidates.Add(indoorHit.position);
                }
                if(objective.Building!=null && objective.Building.Floors>1)
                {
                    // Small rooms need samples on the actual floor rather than coarse radial rings.
                    int floor=objective.Building.FloorAt(objective.Position);
                    foreach(var station in objective.Building.EvidenceStations)
                    {
                        var local=station.Standing;local.y=floor*objective.Building.FloorHeight+.08f;
                        candidates.Add(objective.Building.transform.TransformPoint(local));
                    }
                    var bounds=new Bounds();
                    foreach(var corner in objective.Building.Footprint)bounds.Encapsulate(new Vector3(corner.x,0,corner.y));
                    for(float x=bounds.min.x+.7f;x<bounds.max.x-.5f;x+=1.2f)
                        for(float z=bounds.min.z+.7f;z<bounds.max.z-.5f;z+=1.2f)
                        {
                            var probe=objective.Building.transform.TransformPoint(new Vector3(x,floor*objective.Building.FloorHeight+.08f,z));
                            if(NavMesh.SamplePosition(probe,out var floorHit,.65f,NavMesh.AllAreas) &&
                                Mathf.Abs(floorHit.position.y-objective.Position.y)<.7f && objective.Building.Contains(floorHit.position) &&
                                SupportedStandingPoint(floorHit.position))candidates.Add(floorHit.position);
                        }
                }
                candidates.Sort((a,b)=>Vector3.Distance(a,objective.Position).CompareTo(Vector3.Distance(b,objective.Position)));
                int count=0;
                int required=LeakOperation || campaignNode.id>=10?2:1;
                foreach(var position in candidates)
                {
                    if(objective.Building!=null && objective.Building.Floors>1 && (Mathf.Abs(position.y-objective.Position.y)>1 || !objective.Building.Contains(position)))continue;
                    if(storyPoints.Exists(p=>p.Building!=null && p.Building.Floors>1 && p.Building.Contains(position) && p.Building.FloorAt(position)==p.Building.FloorAt(p.Position)))continue;
                    float d=Vector3.Distance(position,objective.Position);
                    if(d<(objective.Building!=null && objective.Building.Floors>1?.8f:2.5f) || d>24 ||
                        Vector3.Distance(position,player.transform.position)<5 || !SupportedStandingPoint(position) ||
                        !StoryReachable(objective.Position,position))continue;
                    if(positions.Exists(p=>Vector3.Distance(p,position)<4))continue;
                    EnemyBot guard=null;foreach(var enemy in enemies)
                        if(enemy!=null && !enemy.Actor.IsDead && enemy!=contractTarget && !enemy.isCounterSniper && !assigned.Contains(enemy)){guard=enemy;break;}
                    if(guard==null)
                    {
                        int before=enemies.Count;SpawnEnemy(before,position+Vector3.up*.12f);
                        if(enemies.Count==before)continue;guard=enemies[enemies.Count-1];
                    }
                    var controller=guard.GetComponent<CharacterController>();bool enabled=controller!=null && controller.enabled;
                    if(controller!=null)controller.enabled=false;
                    guard.transform.position=position+Vector3.up*.12f;
                    var agent=guard.GetComponent<NavMeshAgent>();
                    if(agent!=null && agent.enabled && NavMesh.SamplePosition(position,out var hit,2,NavMesh.AllAreas))agent.Warp(hit.position);
                    if(controller!=null)controller.enabled=enabled;
                    guard.name=LeakOperation?"Dispatch archive guard":"Objective security guard";
                    guard.patrolRadius=objective.Building!=null?1.5f:4;guard.detectionRange=LeakOperation?55:38;
                    guard.currentState=AIState.Patrol;
                    guard.InitializePatrol();assigned.Add(guard);positions.Add(position);
                    if(++count>=required)break;
                }
                if(count==0){contractSetupError="No guarded approach to this objective. Choose another sector with accessible buildings.";return;}
            }
        }
        void LeakRecovered(Vector3 reportedPosition)
        {
            if(!LeakOperation)return;
            foreach(var enemy in enemies)if(enemy!=null && !enemy.Actor.IsDead && !enemy.isCounterSniper)enemy.BeginAssault(reportedPosition);
            // One bounded response per recovered log. Never spawn beside the player or in plain sight.
            if(leakResponses>=2)return;
            leakResponses++;
            var ground=GetInnerGroundPoints();
            ground.Sort((a,b)=>FlatDistance(a,reportedPosition).CompareTo(FlatDistance(b,reportedPosition)));
            int spawned=0;
            foreach(var position in ground)
            {
                float d=FlatDistance(position,player.transform.position);
                if(d<30 || d>90 || !SupportedStandingPoint(position) || !StoryReachable(position,reportedPosition))continue;
                if(cameraView!=null && Vector3.Dot(cameraView.transform.forward,(position-cameraView.transform.position).normalized)>.1f && ClearSniperTarget(player.transform.position,position))continue;
                bool occupied=false;foreach(var enemy in enemies)if(enemy!=null && FlatDistance(enemy.transform.position,position)<8){occupied=true;break;}
                if(occupied)continue;
                int before=enemies.Count;SpawnEnemy(before,position+Vector3.up*.12f);if(enemies.Count==before)continue;
                var responder=enemies[enemies.Count-1];responder.name="Meridian dispatch response";responder.BeginAssault(reportedPosition);
                if(++spawned>=2)break;
            }
            shotNotice=storyCompleted==storyPoints.Count?"LOGS SECURED — REACH EXTRACTION AND HOLD FOR 12s":"LOG ACCESS TRACED — GUARDS ARE CONVERGING";shotNoticeTime=5;
        }
        void UpdateLeakDamage()
        {
            if(!LeakOperation || player.Health==null)return;
            float health=player.Health.Health;
            if(health<leakPreviousHealth){storyHold=0;leakExtractionHold=0;leakInteractionBlockedUntil=Time.time+1;}
            leakPreviousHealth=health;
        }
        void UpdateLeakExtraction(float dt)
        {
            if(Vector3.Distance(player.transform.position,extractionPoint)>=3){leakExtractionHold=0;return;}
            if(Time.time>=leakInteractionBlockedUntil)leakExtractionHold+=dt;
            if(leakExtractionHold>=LeakExtractionSeconds)TriggerMissionVictory("DISPATCH LOGS EXTRACTED","COUNTERATTACK SURVIVED // EVIDENCE SECURED");
        }
    }
}
