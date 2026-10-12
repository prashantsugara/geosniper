using System.Collections.Generic;

using UnityEngine;

using UnityEngine.AI;

namespace GeoSniper

{

    public sealed partial class UrbanCombatMission

    {

        sealed class StoryPoint {public Vector3 Position;public GameObject Visual;public bool Done;public EnterableBuilding Building;public Vector3 RestPosition;public Quaternion RestRotation;}

        readonly List<StoryPoint> storyPoints=new List<StoryPoint>();

        readonly List<EnemyBot> rescueAttackers=new List<EnemyBot>();

        GameObject storyObjects;

        float storyHold,storyUseUntil;
        StoryPoint interactingStoryPoint;

        int storyCompleted;

        bool hostageReleased;

        bool storyAftermathRead;

        bool storyOutcomeNarrated;

        bool StoryFieldMode=>activeContract.Type==CampaignContractType.EvidenceRecovery || activeContract.Type==CampaignContractType.Sabotage || activeContract.Type==CampaignContractType.Rescue;

        void ResetStoryOperation()

        {

            if(storyObjects!=null){storyObjects.SetActive(false);Destroy(storyObjects);storyObjects=null;}

            StoryNarrator.Stop(gameObject);storyOutcomeNarrated=false;

            storyAftermathRead=false;

            storyPoints.Clear();rescueAttackers.Clear();storyHold=0;storyUseUntil=0;storyCompleted=0;hostageReleased=false;

            ResetLeakOperation();

            floorBriefings.Clear();extractionResponseDispatched=false;sabotageExitHold=0;sabotageLastHealth=100;interactionHealth=100;interactingStoryPoint=null;

        }

        void PrepareStoryOperation()

        {

            if(campaignNode==null || player==null)return;

            var story=CampaignStory.Get(campaignNode.id);

            if(!StoryFieldMode)

            {

                if(story.windowStart && contractTarget!=null)TryStoryWindowNest(contractTarget.transform.position);

                else if(story.windowStart && contractVIP!=null)TryStoryWindowNest(contractVIP.transform.position);

                return;

            }

            var ground=GetInnerGroundPoints();ground.RemoveAll(p=>!SupportedStandingPoint(p));

            ground.Sort((a,b)=>FlatDistance(a,player.transform.position).CompareTo(FlatDistance(b,player.transform.position)));

            if(ground.Count==0){contractSetupError="No supported route for this field operation. Choose another sector.";return;}

            Vector3 start=ground[0];

            if(activeContract.Type==CampaignContractType.Rescue && contractVIP!=null)

            {

                float best=float.MaxValue;

                foreach(var p in ground)

                {

                    float d=FlatDistance(p,contractVIP.transform.position);

                    if(d>=14 && d<best && StoryReachable(p,contractVIP.transform.position)){best=d;start=p;}

                }

                contractVIP.HeldForRescue=true;contractVIP.name="Story witness // "+campaignNode.title;

                foreach(var enemy in enemies)if(enemy!=null && enemy.isVipAttacker){rescueAttackers.Add(enemy);enemy.isVipAttacker=false;}

                player.Place(start);

                storyPoints.Add(new StoryPoint{Position=contractVIP.transform.position,Visual=contractVIP.gameObject});

                PrepareObjectiveGuards(ground);storyPoints.Clear();

                SetWaypoint(contractVIP.transform.position,"RELEASE WITNESS");return;

            }

            int required=Mathf.Clamp(story.interactions,1,3);

            if(!FindIndoorOperationStart(ground,required,out start)){contractSetupError="No reachable indoor objectives here. Choose another sector with accessible buildings.";return;}

            player.Place(start);

            ground.Sort((a,b)=>FlatDistance(a,start).CompareTo(FlatDistance(b,start)));

            storyObjects=new GameObject("Story field objectives");storyObjects.transform.SetParent(transform,false);

            PlaceIndoorEvidence(start,required);

            if(storyPoints.Count<required){contractSetupError="No reachable indoor objectives here. Choose another sector with accessible buildings.";return;}

            PrepareObjectiveGuards(ground);

            extractionPoint=start;

            foreach(var p in ground)

            {

                bool separate=true;foreach(var objective in storyPoints)if(FlatDistance(p,objective.Position)<(LeakOperation?25:8))separate=false;

                if(separate && FlatDistance(p,start)>18 && FlatDistance(p,start)<100 && StoryReachable(start,p)){extractionPoint=p;break;}

            }

            var exit=CreateExtractionBeacon(extractionPoint);

            exit.transform.SetParent(storyObjects.transform,true);

            SetWaypoint(storyPoints[0].Position,activeContract.Type==CampaignContractType.Sabotage?"SWITCH OFF RELAY":"RECOVER EVIDENCE");

        }

        bool StoryReachable(Vector3 from,Vector3 to)

        {

            if(!NavMesh.SamplePosition(from,out var a,2,NavMesh.AllAreas))return false;

            if(!NavMesh.SamplePosition(to,out var b,2,NavMesh.AllAreas))return false;

            var path=new NavMeshPath();return NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete;

        }

        GameObject CreateStoryBox(string label,Vector3 point,Vector3 size,Color color)

        {

            var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=label;box.transform.position=point;box.transform.localScale=size;

            box.GetComponent<Collider>().enabled=false;var material=new Material(Shader.Find("Standard")){color=color};ownedMaterials.Add(material);

            box.GetComponent<Renderer>().sharedMaterial=material;return box;

        }

        void TryStoryWindowNest(Vector3 target)

        {

            foreach(var interior in EnterableBuilding.All)

                    foreach(var window in interior.FiringWindows)

                    {

                        Vector3 opening=interior.transform.TransformPoint(window);

                        Vector3 flat=(target-opening);flat.y=0;if(flat.sqrMagnitude<25 || flat.sqrMagnitude>160*160)continue;

                        Vector3 foot=opening-flat.normalized*.8f-Vector3.up*1.7f;

                        if(!SupportedStandingPoint(foot+Vector3.up*.08f) || !ClearSniperTarget(foot,target))continue;

                        player.Place(foot+Vector3.up*.08f);return;

                    }

        }

        StoryPoint CurrentStoryPoint()

        {

            StoryPoint nearest=null;float distance=float.MaxValue;

            foreach(var point in storyPoints)if(!point.Done){float d=Vector3.Distance(player.transform.position,point.Position);if(d<distance){distance=d;nearest=point;}}

            return nearest;

        }

        bool StoryInReach(Vector3 position)

        {

            if(player==null || Vector3.Distance(player.transform.position,position)>2.8f)return false;

            Vector3 eye=player.transform.position+Vector3.up*1.4f,target=position+Vector3.up*.8f;

            if(cameraView!=null && Vector3.Dot(cameraView.transform.forward,(target-cameraView.transform.position).normalized)<.1f)return false;

            return !Physics.Linecast(eye,target,out var obstruction,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) ||

                (contractVIP!=null && obstruction.collider.GetComponentInParent<CivilianBot>()==contractVIP);

        }

        void UpdateStoryField(float dt)

        {

            UpdateLeakDamage();

            UpdateObjectiveFloorGuidance();
            if(player.Health.Health<interactionHealth){storyHold=0;storyUseUntil=0;}
            interactionHealth=player.Health.Health;

            if(activeContract.Type==CampaignContractType.Rescue)

            {

                if(contractVIP==null || contractVIP.Actor.IsDead){FailMission("WITNESS LOST - RETRY THE RESCUE");return;}

                if(!hostageReleased)

                {

                    bool holding=StoryInReach(contractVIP.transform.position) && (Input.GetKey(KeyCode.E) || Time.unscaledTime<storyUseUntil);

                    storyHold=holding?storyHold+dt:0;

                    if(storyHold>=3)

                    {

                        hostageReleased=true;contractVIP.HeldForRescue=false;storyHold=0;

                        SetWaypoint(extractionPoint,"WITNESS EXTRACTION");

                        foreach(var enemy in rescueAttackers)if(enemy!=null)enemy.isVipAttacker=true;

                        shotNotice="WITNESS RELEASED - COVER THE ESCAPE";shotNoticeTime=4;

                    }

                }

                else if(Vector3.Distance(contractVIP.transform.position,extractionPoint)<5)TriggerMissionVictory("WITNESS EXTRACTED","SAFELY REACHED THE REAL EXTRACTION");

                return;

            }

            var point=CurrentStoryPoint();
            if(interactingStoryPoint!=null && interactingStoryPoint!=point)
            {if(!interactingStoryPoint.Done)AnimateStoryInteraction(interactingStoryPoint,0);storyHold=0;storyUseUntil=0;}
            interactingStoryPoint=point;

            if(point!=null)

            {

                bool holding=(!LeakOperation || Time.time>=leakInteractionBlockedUntil) && StoryPointInReach(point) && (Input.GetKey(KeyCode.E) || Time.unscaledTime<storyUseUntil);

                storyHold=holding?storyHold+dt:0;

                float duration=StoryRecoverySeconds;
                AnimateStoryInteraction(point,Mathf.Clamp01(storyHold/duration));

                if(storyHold>=duration)

                {

                    point.Done=true;if(activeContract.Type==CampaignContractType.Sabotage)SwitchRelayOff(point.Visual);else point.Visual.SetActive(false);storyCompleted++;storyHold=0;

                    var sound=Resources.Load<AudioClip>("Audio/Interactions/"+(activeContract.Type==CampaignContractType.Sabotage?"relay_off":"paper_pickup"));if(sound!=null && audioSource!=null)audioSource.PlayOneShot(sound,.8f);

                    var next=CurrentStoryPoint();SetWaypoint(next!=null?next.Position:extractionPoint,next==null?"REACH EXIT":activeContract.Type==CampaignContractType.Sabotage?"SWITCH OFF RELAY":"RECOVER EVIDENCE");

                    shotNotice=activeContract.Type==CampaignContractType.Sabotage?"RELAY OFFLINE - GUARDS ARE CONVERGING":"EVIDENCE RECOVERED";shotNoticeTime=3;

                    LeakRecovered(point.Position);

                    if(activeContract.Type==CampaignContractType.Sabotage)

                        foreach(var enemy in enemies)if(enemy!=null && !enemy.Actor.IsDead && !enemy.isCounterSniper)enemy.BeginAssault(player.transform.position);

                }

            }

            else if(LeakOperation)UpdateLeakExtraction(dt);

            else if(activeContract.Type==CampaignContractType.Sabotage)UpdateSabotageExtraction(dt);

            else if(Vector3.Distance(player.transform.position,extractionPoint)<3)

                TriggerMissionVictory("INTEL EXTRACTED","DOCUMENTS SECURED // OPERATION SUCCESSFUL");

        }

        string StoryHudObjective()=>activeContract.Type==CampaignContractType.Rescue

            ?hostageReleased?"COVER WITNESS // "+Mathf.CeilToInt(contractVIP.Actor.Health)+" HP // REACH EXTRACTION":"REACH THE WITNESS // RELEASE AND STAY CLOSE"

            :(activeContract.Type==CampaignContractType.Sabotage?"RELAYS DISABLED ":"EVIDENCE RECOVERED ")+storyCompleted+" / "+storyPoints.Count+(storyCompleted==storyPoints.Count?LeakOperation?" // HOLD EXTRACTION "+Mathf.CeilToInt(LeakExtractionSeconds-leakExtractionHold)+"s":" // REACH EXIT":"");

        void DrawStoryFieldHUD(float w,float h)

        {

            if(!stageMode || !StoryFieldMode || player==null || player.Scoped)return;

            var point=CurrentStoryPoint();Vector3 location=activeContract.Type==CampaignContractType.Rescue && !hostageReleased?contractVIP.transform.position:point!=null?point.Position:extractionPoint;

            string label=activeContract.Type==CampaignContractType.Rescue && !hostageReleased?"RELEASE WITNESS":point!=null?activeContract.Type==CampaignContractType.Sabotage?"ISOLATE POWER":"PICK UP DOCUMENTS":"REACH EXIT";

            if(cameraView!=null)

            {

                var screen=cameraView.WorldToViewportPoint(location+Vector3.up*1.6f);

                if(screen.z>0)DrawTacticalScreenBadge(new Vector2(Mathf.Clamp(screen.x,.12f,.78f)*w,Mathf.Clamp(1-screen.y,.2f,.65f)*h),label+" // "+Mathf.RoundToInt(Vector3.Distance(player.transform.position,location))+"m",CommandGUI.AccentGold,w,h);

            }

            if((point!=null?StoryPointInReach(point):activeContract.Type==CampaignContractType.Rescue && !hostageReleased && StoryInReach(location)))

            {

                Rect button=new Rect((w-280)*.5f,h*.7f,280,44);

                float limit=StoryRecoverySeconds;

                if(CommandGUI.DrawButton(button,label+"  [E]",true,12))storyUseUntil=Time.unscaledTime+limit+.5f;

                GUI.Label(new Rect(button.x,button.y-23,button.width,22),InteractionPhaseLabel(storyHold/limit),new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=12,normal={textColor=Color.white}});

                CommandGUI.Fill(new Rect(button.x,button.yMax+3,button.width*Mathf.Clamp01(storyHold/limit),4),CommandGUI.AccentGold);

            }

        }

        bool StoryBonusEarned()

        {

            if(campaignNode==null)return false;

            switch(campaignNode.bonusGoal)

            {

                case "Precision":return totalHeadshotsScored>0 && totalShotsFired>0 && totalHitsScored/(float)totalShotsFired>=.7f;

                case "Ghost":return alerts.Level==0;

                case "Swift":return missionDuration<=180;

                default:return player!=null && player.Health!=null && player.Health.Health>=player.Health.maxHealth*.75f;

            }

        }

        void DrawStoryAftermath(float w,float h)

        {

            if(!storyOutcomeNarrated){storyOutcomeNarrated=true;StoryNarrator.Play(gameObject,campaignNode.id,true);}

            CommandGUI.Fill(new Rect(0,0,w,h),CommandGUI.ThemeBg);

            float width=Mathf.Min(760,w-32),height=Mathf.Min(410,h-32);Rect panel=new Rect((w-width)/2,(h-height)/2,width,height);

            CommandGUI.Fill(panel,CommandGUI.ThemeCard);

            var heading=new GUIStyle(GUI.skin.label){fontSize=22,fontStyle=FontStyle.Bold,wordWrap=true,normal={textColor=CommandGUI.AccentGold}};

            var body=new GUIStyle(GUI.skin.label){fontSize=18,wordWrap=true,normal={textColor=CommandGUI.Text}};

            GUI.Label(new Rect(panel.x+24,panel.y+20,width-48,40),campaignNode.id==49?"GLASS MERIDIAN // THE WAY IS CLEAR":campaignNode.codeName,heading);

            GUI.Label(new Rect(panel.x+24,panel.y+76,width-48,height-170),campaignNode.storyOutcome,body);

            GUI.Label(new Rect(panel.x+24,panel.yMax-98,width-48,35),"OPTIONAL CHALLENGE: "+(StoryBonusEarned()?"ACHIEVED (+10% REWARD)":"NOT ACHIEVED")+" // "+CampaignStory.BonusDescription(campaignNode.bonusGoal),new GUIStyle(body){fontSize=13});

            if(CommandGUI.DrawButton(new Rect(panel.x+24,panel.yMax-55,width-48,38),"VIEW REWARDS",true,13)){storyAftermathRead=true;StoryNarrator.Stop(gameObject);}

        }

    }

}

