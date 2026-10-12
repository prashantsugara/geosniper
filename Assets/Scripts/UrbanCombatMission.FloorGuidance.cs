using System.Collections.Generic;
using UnityEngine;
namespace GeoSniper
{
    public sealed partial class UrbanCombatMission
    {
        readonly HashSet<StoryPoint> floorBriefings=new HashSet<StoryPoint>();
        float sabotageExitHold,sabotageLastHealth=100;
        bool extractionResponseDispatched;
        void UpdateObjectiveFloorGuidance()
        {
            foreach(var task in storyPoints)
            {
                var building=task.Building;
                if(task!=CurrentStoryPoint() || task.Done || building==null || building.Floors<=1 || floorBriefings.Contains(task) || !building.Contains(player.transform.position))continue;
                int floor=building.FloorAt(task.Position);
                string destination=floor==0?"GROUND FLOOR":"FLOOR "+(floor+1);
                floorBriefings.Add(task);
                StoryNarrator.PlayRadio(gameObject,"Floor"+floor);
                shotNotice="HQ: "+(activeContract.Type==CampaignContractType.Sabotage?"Relay control":"Dispatch documents")+" on "+destination+". Guards cover the other floors. Use the stairs.";
                shotNoticeTime=8;
                SetWaypoint(task.Position,(activeContract.Type==CampaignContractType.Sabotage?"RELAY // ":"DOCUMENTS // ")+destination);
            }
        }
        void DrawExtractionHoldHUD(float w,float h)
        {
            if(player==null || (currentMissionState!=MissionState.InProgress && currentMissionState!=MissionState.Extraction) || victoryPending || !stageMode)return;
            float hold=0,duration=0;
            if(stageIndex==0 && openingExitActive){hold=openingExitHold;duration=4;}
            else if(StoryFieldMode && storyPoints.Count>0 && storyCompleted==storyPoints.Count)
            {
                if(LeakOperation){hold=leakExtractionHold;duration=LeakExtractionSeconds;}
                else if(activeContract.Type==CampaignContractType.Sabotage){hold=sabotageExitHold;duration=6;}
            }
            if(duration<=0)return;
            bool inside=Vector3.Distance(player.transform.position,extractionPoint)<3;
            float width=Mathf.Min(360,w*.42f);
            Rect panel=new Rect((w-width)*.5f,h*.18f,width,48);
            CommandGUI.Fill(panel,new Color(.035f,.07f,.085f,.88f));
            GUI.Label(new Rect(panel.x+8,panel.y+3,width-16,30),inside?"EXTRACTION // HOLD "+Mathf.CeilToInt(Mathf.Max(0,duration-hold))+"s":"EXTRACTION // REACH THE SIGNAL",
                new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=14,normal={textColor=Color.white}});
            CommandGUI.Fill(new Rect(panel.x+8,panel.yMax-10,width-16,4),new Color(.2f,.28f,.3f));
            CommandGUI.Fill(new Rect(panel.x+8,panel.yMax-10,(width-16)*Mathf.Clamp01(hold/duration),4),new Color(.2f,.85f,.55f));
        }
        void UpdateSabotageExtraction(float dt)
        {
            if(!extractionResponseDispatched)
            {
                extractionResponseDispatched=true;
                foreach(var guard in enemies)
                    if(guard!=null && !guard.Actor.IsDead && !guard.isCounterSniper && StoryReachable(guard.transform.position,extractionPoint))
                        guard.BeginAssault(extractionPoint);
            }
            float health=player.Health.Health;
            bool inside=Vector3.Distance(player.transform.position,extractionPoint)<3;
            sabotageExitHold=inside && health>=sabotageLastHealth?sabotageExitHold+dt:0;
            sabotageLastHealth=health;
            if(inside){shotNotice="NETWORK OFFLINE // HOLD EXTRACTION "+Mathf.CeilToInt(6-sabotageExitHold)+"s";shotNoticeTime=1;}
            if(sabotageExitHold>=6)TriggerMissionVictory("NETWORK DISABLED","COUNTERATTACK SURVIVED // EXTRACTED");
        }
    }
}

