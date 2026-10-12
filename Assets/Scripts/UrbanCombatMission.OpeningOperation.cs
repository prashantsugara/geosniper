using UnityEngine;
namespace GeoSniper
{
    public sealed partial class UrbanCombatMission
    {
        Vector3 openingRoofRoute,openingWindowRoute,openingExit;
        bool openingRoutesReady,openingExitActive;
        float openingExitHold,openingPreviousHealth;
        GameObject openingOperationProps;
        void PrepareOpeningOperation()
        {
            openingRoutesReady=false;openingExitActive=false;openingExitHold=0;
            if(openingOperationProps!=null){openingOperationProps.SetActive(false);Destroy(openingOperationProps);}
            if(stageIndex!=0 || contractTarget==null)return;
            Physics.SyncTransforms();var streets=GetInnerGroundPoints();
            bool found=false;int supportedWindows=0,visibleStreets=0,stairBuildings=0;
            foreach(var building in EnterableBuilding.All)
            {
                if(!building.HasRoofAccess)continue;
                stairBuildings++;
                var roofCandidates=new System.Collections.Generic.List<Vector3>();
                float roofHeight=building.transform.position.y;
                foreach(var floor in building.GetComponentsInChildren<MeshCollider>())if(floor.name=="Upper floor / roof")roofHeight=Mathf.Max(roofHeight,floor.bounds.max.y);
                foreach(float x in new[]{-12f,-6f,0f,6f,12f})foreach(float z in new[]{-12f,-6f,0f,6f,12f})
                {
                    var probe=building.transform.TransformPoint(new Vector3(x,0,z));probe.y=roofHeight+.5f;
                    if(Physics.Raycast(probe,Vector3.down,out var roofHit,.7f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) && roofHit.normal.y>.7f)
                    {var roofPoint=roofHit.point+Vector3.up*.15f;if(SupportedStandingPoint(roofPoint))roofCandidates.Add(roofPoint);}
                }
                foreach(var window in building.OpenWindows)
                {
                    var wall=building.transform.TransformPoint(window.Center);
                    var outward=building.transform.TransformDirection(window.Outward).normalized;
                    var inside=wall-outward*.85f+Vector3.up*.1f;
                    if(Physics.Raycast(inside+Vector3.up*.7f,Vector3.down,out var footing,1.2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))inside=footing.point+Vector3.up*.12f;
                    if(!SupportedStandingPoint(inside))continue;
                    supportedWindows++;
                    foreach(var street in streets)
                    {
                        float distance=FlatDistance(street,inside);
                        if(distance<35 || distance>100 || Vector3.Dot(street-wall,outward)<5 || !SupportedStandingPoint(street) || !OpeningSightline(inside,street))continue;
                        visibleStreets++;
                        foreach(var roof in roofCandidates)
                        {
                            if(FlatDistance(roof,building.transform.position)>22 || roof.y<wall.y+3 || !SupportedStandingPoint(roof) || !OpeningClearSightline(roof,street))continue;
                            openingRoofRoute=roof;openingWindowRoute=inside;
                            var controller=contractTarget.GetComponent<CharacterController>();if(controller!=null)controller.enabled=false;
                            contractTarget.transform.position=street;if(controller!=null)controller.enabled=true;
                            contractTarget.InitializePatrol();contractTarget.speed=0;contractTarget.patrolRadius=0;
                            player.Place(roof);player.Face(street+Vector3.up*1.4f,true);
                            foreach(var guard in enemies)
                            {
                                if(guard==null || guard==contractTarget || guard.isCounterSniper)continue;
                                foreach(var candidate in streets)
                                    if(FlatDistance(candidate,street)>=8 && FlatDistance(candidate,street)<18 && StoryReachable(street,candidate) && SupportedStandingPoint(candidate))
                                    {
                                        var body=guard.GetComponent<CharacterController>();if(body!=null)body.enabled=false;
                                        guard.transform.position=candidate;if(body!=null)body.enabled=true;
                                        guard.speed=1.1f;guard.patrolRadius=3;guard.detectionRange=45;guard.engagementRange=50;guard.InitializePatrol();break;
                                    }
                            }
                            found=true;break;
                        }
                        if(found)break;
                    }
                    if(found)break;
                }
                if(found)break;
            }
            openingRoutesReady=found;
            if(!found)Debug.LogWarning("Opening routes: stairs="+stairBuildings+", supported windows="+supportedWindows+", visible streets="+visibleStreets+", roofs="+rooftopSpawns.Count);
            openingExit=contractTarget.transform.position;
            foreach(var candidate in streets)
                if(FlatDistance(candidate,contractTarget.transform.position)>20 && FlatDistance(candidate,contractTarget.transform.position)<65 && SupportedStandingPoint(candidate) && ValidExtractionFootprint(candidate,3) && StoryReachable(contractTarget.transform.position,candidate)){openingExit=candidate;break;}
            openingPreviousHealth=player.Health!=null?player.Health.Health:100;
            if(found){SetWaypoint(openingWindowRoute,"OPTIONAL WINDOW APPROACH");shotNotice="ROOFTOP SHOT / or take the stairs to a window / identify the marked scout";shotNoticeTime=10;}
        }
        void UpdateOpeningExtraction(float delta)
        {
            if(!openingExitActive)
            {
                openingExitActive=true;openingOperationProps=CreateExtractionBeacon(openingExit);openingOperationProps.transform.SetParent(transform,true);
                SetWaypoint(openingExit,"EXTRACT // HOLD 4 SECONDS");
                foreach(var guard in enemies)if(guard!=null && !guard.Actor.IsDead && guard!=contractTarget)guard.BeginAssault(player.transform.position);
                StoryNarrator.PlayRadio(gameObject,"OpeningExit");
                GameAnalyticsManager.TrackCustomEvent("opening_extraction_started");
            }
            float health=player.Health!=null?player.Health.Health:100;
            bool damaged=health<openingPreviousHealth;openingPreviousHealth=health;
            bool inside=Vector3.Distance(player.transform.position,openingExit)<3;
            openingExitHold=inside && !damaged?openingExitHold+delta:0;
            shotNotice=inside?"HOLD EXTRACTION // "+Mathf.CeilToInt(Mathf.Max(0,4-openingExitHold))+"s":"SCOUT DOWN // REACH THE EXTRACTION MARKER";shotNoticeTime=.3f;
            if(openingExitHold>=4)TriggerMissionVictory("SCOUT CONFIRMED // EXTRACTED","FIRST CONTRACT COMPLETE");
        }
    }
}
