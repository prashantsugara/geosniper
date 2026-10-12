using UnityEngine;
namespace GeoSniper
{
    public sealed partial class UrbanCombatMission
    {
        bool openingScopeSeen,earlyFieldHintSeen;
        CampaignAcoustics campaignAcoustics;
        void PrepareCampaignAcoustics()
        {
            if(campaignAcoustics==null){var obj=new GameObject("Campaign acoustic feedback");obj.transform.SetParent(transform,false);campaignAcoustics=obj.AddComponent<CampaignAcoustics>();}
            campaignAcoustics.Initialize();
            CampaignAcoustics.SetMuted(weapon!=null && weapon.Muted);
        }
        void TickOpeningLesson()
        {
            if(campaignNode==null || campaignNode.id>2 || player==null || totalShotsFired>0 || missionDuration>30)return;
            if(!openingScopeSeen && player.Scoped)
            {
                openingScopeSeen=true;
                shotNotice=campaignNode.id==0?"MARKED TARGET / aim for the head / fire when steady":campaignNode.id==1?"PROTECT MINA / watch for armed attackers":"COURIER / lead the moving target";
                shotNoticeTime=6;
                GameAnalyticsManager.TrackCustomEvent("opening_scope_used",new System.Collections.Generic.Dictionary<string,object>{{"stage_index",stageIndex+1},{"seconds_to_scope",Mathf.RoundToInt(missionDuration)}});
            }
            if(!openingScopeSeen && missionDuration>12 && shotNoticeTime<=0){shotNotice="TAP SCOPE / your objective is marked";shotNoticeTime=5;}
        }
        void TickEarlyFieldHint()
        {
            if(earlyFieldHintSeen || campaignNode==null || campaignNode.id<3 || campaignNode.id>5 || player==null || mapOpen || missionDuration<10 || shotNoticeTime>0 || StoryNarrator.Playing(gameObject))return;
            earlyFieldHintSeen=true;
            shotNotice=campaignNode.id==3?"QUIET APPROACH // use cover and watch the alarm. Break sight before reloading.":campaignNode.id==4?
                "RECOVER BOTH COPIES // enter the marked office. Command will identify the floor.":"ISOLATE THE RELAY // shut down power, then hold extraction against the response.";
            shotNoticeTime=6;
        }
        bool OpeningSightline(Vector3 vantage,Vector3 focus)
        {
            float distance=FlatDistance(vantage,focus);if(distance<8 || distance>180)return false;
            Vector3 target=focus+Vector3.up*1.4f;
            // Test the actual eye position; candidate placement adds its own clearance margin.
            foreach(float offset in new[]{0f})
            {
                Vector3 eye=vantage+Vector3.up*(1.55f+offset);
                foreach(var hit in Physics.RaycastAll(eye,(target-eye).normalized,Vector3.Distance(eye,target),Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                {
                    if(player!=null && hit.collider.transform.IsChildOf(player.transform))continue;
                    if(Vector3.Distance(hit.point,target)<2)continue;
                    return false;
                }
            }
            return true;
        }
        bool OpeningClearSightline(Vector3 vantage,Vector3 focus)
        {
            if(!OpeningSightline(vantage,focus) || !OpeningSightline(vantage+Vector3.up*.2f,focus) || !OpeningSightline(vantage-Vector3.up*.2f,focus))return false;
            // A moving VIP must not be placed on a sightline grazing a wall edge.
            foreach(var offset in new[]{Vector3.right*.4f,Vector3.left*.4f,Vector3.forward*.4f,Vector3.back*.4f,Vector3.up*.2f})
                if(!OpeningSightline(vantage,focus+offset))return false;
            return true;
        }
        void PrepareOpeningLesson()
        {
            if(campaignNode==null || campaignNode.id>2 || player==null || contractSetupError!=null)return;
            openingScopeSeen=false;
            Physics.SyncTransforms();
            Vector3 focus=contractTarget!=null?contractTarget.transform.position:contractVIP!=null?contractVIP.transform.position:player.transform.position;
            if(campaignNode.id==0 && rooftopSpawns!=null)
            {
                var perches=new System.Collections.Generic.List<Vector3>(rooftopSpawns);
                perches.Sort((a,b)=>Mathf.Abs(FlatDistance(a,focus)-65).CompareTo(Mathf.Abs(FlatDistance(b,focus)-65)));
                foreach(var perch in perches)
                    if(perch.y>focus.y+3 && SupportedStandingPoint(perch) && OpeningClearSightline(perch,focus)){player.Place(perch);break;}
            }
            if(!OpeningSightline(player.transform.position,focus))
            {
                var candidates=GetInnerGroundPoints();if(rooftopSpawns!=null)candidates.AddRange(rooftopSpawns);
                candidates.Sort((a,b)=>Mathf.Abs(FlatDistance(a,focus)-50).CompareTo(Mathf.Abs(FlatDistance(b,focus)-50)));
                foreach(var candidate in candidates)if(SupportedStandingPoint(candidate) && OpeningClearSightline(candidate,focus)){player.Place(candidate);break;}
                if(!OpeningSightline(player.transform.position,focus))
                    foreach(float radius in new[]{35f,50f,70f,18f,12f})
                    {
                        bool placed=false;
                        for(int i=0;i<24;i++)
                        {
                            float angle=i*Mathf.PI/12;var desired=focus+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                            if(!UnityEngine.AI.NavMesh.SamplePosition(desired,out var foot,3,UnityEngine.AI.NavMesh.AllAreas))continue;
                            var candidate=foot.position+Vector3.up*.08f;
                            if(SupportedStandingPoint(candidate) && OpeningClearSightline(candidate,focus)){player.Place(candidate);placed=true;break;}
                        }
                        if(placed)break;
                    }
            }
            player.Face(focus+Vector3.up*1.4f,true);
            shotNotice=campaignNode.id==0?"SCOPE IN / steady your aim / aim for the head"
                :campaignNode.id==1?"COVER MINA / shoot attackers / stay behind cover"
                :"COURIER / watch the route / lead your shot";
            shotNoticeTime=12;
            if(campaignNode.id==0 && contractTarget!=null)
            {contractTarget.speed=0;contractTarget.patrolRadius=0;contractTarget.InitializePatrol();}
            if(campaignNode.id==2 && contractTarget!=null)contractTarget.runnerStartDelay=Mathf.Max(18,contractTarget.runnerStartDelay);
        }
    }
}
