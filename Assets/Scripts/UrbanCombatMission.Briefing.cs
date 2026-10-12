using UnityEngine;

namespace GeoSniper
{
    public sealed partial class UrbanCombatMission
    {
        static void BriefingText(Rect rect,string text,int size,Color color,bool bold=false)
        {
            GUI.Label(rect,text,new GUIStyle(GUI.skin.label){fontSize=size,wordWrap=true,
                fontStyle=bold?FontStyle.Bold:FontStyle.Normal,normal={textColor=color}});
        }

        public void StartPreparedCampaign()
        {
            if(currentMissionState!=MissionState.Briefing || contractSetupError!=null)return;
            currentMissionState=MissionState.InProgress;
            PrepareCampaignAcoustics();earlyFieldHintSeen=false;
            if(player!=null)player.InputBlocked=false;
            foreach(var enemy in enemies)if(enemy!=null)enemy.Suspended=false;
            foreach(var civ in civilians)if(civ!=null)civ.Suspended=false;
            GameAnalyticsManager.TrackMissionStart(stageIndex,activeContract.Type.ToString(),weapon!=null?weapon.CurrentWeaponIndex:1);
            PlayHQDispatchSound();
        }
        void DrawCommandBriefing()
        {
            var previous=GUI.matrix;
            var previousColor=GUI.color;
            try
            {
                GUI.color=Color.white;
                GUI.matrix=Matrix4x4.identity;
                CommandGUI.Fill(new Rect(0,0,Screen.width,Screen.height),new Color(.025f,.033f,.039f,.94f));
                Rect safe=Screen.safeArea;
                if(safe.width<=0 || safe.height<=0) safe=new Rect(0,0,Screen.width,Screen.height);
                float scale=Mathf.Max(.01f,Mathf.Min(safe.width/1000f,safe.height/640f));
                float width=safe.width/scale,height=safe.height/scale;
                GUI.matrix=Matrix4x4.TRS(new Vector3(safe.x,Screen.height-safe.yMax,0),Quaternion.identity,new Vector3(scale,scale,1));
                float x=(width-920)/2,y=(height-560)/2;
                var amber=CommandGUI.AccentGold;var text=CommandGUI.Text;var muted=CommandGUI.Muted;
                CommandGUI.Fill(new Rect(x,y,920,560),CommandGUI.ThemeBg);
                CommandGUI.Fill(new Rect(x,y,4,68),amber);
                BriefingText(new Rect(x+26,y+20,500,24),"OPERATIONS / MISSION BRIEFING",13,amber,true);
                BriefingText(new Rect(x+700,y+22,190,22),"AWAITING YOUR COMMAND",10,muted);
                CommandGUI.Fill(new Rect(x+26,y+66,868,1),CommandGUI.ThemeBorder);

                string title=campaignNode!=null?campaignNode.title:activeContract.Title;
                BriefingText(new Rect(x+30,y+89,820,65),title,32,text,true);
                BriefingText(new Rect(x+32,y+160,820,25),
                    (campaignNode!=null?campaignNode.codeName:"FIELD OPERATION")+"   /   "+CampaignProgression.Label(campaignNode),13,amber,true);

                CommandGUI.Fill(new Rect(x+30,y+215,550,230),CommandGUI.ThemeCard);
                BriefingText(new Rect(x+50,y+232,510,24),"YOUR OBJECTIVE",12,muted,true);
                BriefingText(new Rect(x+50,y+269,510,100),activeContract.Type==CampaignContractType.Escape
                    ?"Survive three assault waves and the minimum hold time. Reinforcements change approach. Reach the helicopter when extraction opens."
                    :activeContract.Objective,19,text);
                string tip=activeContract.Type==CampaignContractType.Stealth?"Stay concealed. Watch the alarm meter."
                    :activeContract.Type==CampaignContractType.Overwatch?"Protect the VIP. Prioritize attackers along the route."
                    :activeContract.Type==CampaignContractType.TimedInterception?"Find the courier first. Lead moving targets."
                    :activeContract.Type==CampaignContractType.Escape?"Reload between waves. Clear hostiles to let the next wave advance."
                    :"Identify your targets. Steady your aim before firing.";
                BriefingText(new Rect(x+50,y+390,510,40),StoryFieldMode?"Move to the marker. Start the action, then stay close while it completes.":tip,14,amber);

                BriefingText(new Rect(x+615,y+225,265,22),"REAL-WORLD LOCATION",12,muted,true);
                BriefingText(new Rect(x+615,y+260,265,48),"NEAR " + GetTargetBuildingLocationName(),18,text,true);
                if(campaignNode!=null)BriefingText(new Rect(x+615,y+310,265,16),"OPTIONAL SITE: "+CampaignLocationProfile.ForStage(stageIndex).Name,11,amber);
                CommandGUI.Fill(new Rect(x+615,y+327,265,1),CommandGUI.ThemeBorder);
                BriefingText(new Rect(x+615,y+345,265,78),campaignNode!=null?campaignNode.briefing:"Watch your route and keep cover nearby.",13,text);
                BriefingText(new Rect(x+30,y+447,860,20),campaignNode!=null?"OPTIONAL: "+CampaignStory.BonusDescription(campaignNode.bonusGoal):"",12,amber);

                CommandGUI.Fill(new Rect(x+30,y+468,860,1),CommandGUI.ThemeBorder);
                if(CommandGUI.DrawButton(new Rect(x+30,y+491,145,46),"BACK",false,14)) ExitToLevelMap();
                if(CommandGUI.DrawButton(new Rect(x+440,y+491,210,46),"INSPECT MAP",false,14)) OpenMap();
                if(CommandGUI.DrawButton(new Rect(x+670,y+491,220,46),"BEGIN MISSION  >",true,15))
                {
                    StartPreparedCampaign();
                }
            }
            finally {GUI.matrix=previous;GUI.color=previousColor;}
        }
    }
}
