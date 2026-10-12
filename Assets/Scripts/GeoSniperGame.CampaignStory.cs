using UnityEngine;
namespace GeoSniper
{
    public sealed partial class GeoSniperGame
    {
        bool campaignLocationEdit;
        bool returnToCampaignFromArmory;
        bool campaignBriefingReviewed;
        void OpenCampaignMission()
        {
            selectedNodeId=GetDefaultSelectedNodeId();
            PlayerPrefs.SetInt("GeoSniper.SelectedMode",0);
            SwitchTab(LobbyTab.Campaign);
        }
        bool UseStarterSector()=>selectedNodeId==0 || (selectedNodeId>=0 && selectedNodeId<3
            && !LocationSelectionPolicy.RequiresLiveFix(PlayerPrefs.GetString("GeoSniper.LocationSource","")) && PreferredLocation()==null);
        string CampaignLocationLabel()=>UseStarterSector()?"Training sector / no GPS or download needed":LocationSelectionPolicy.RequiresLiveFix(PlayerPrefs.GetString("GeoSniper.LocationSource",""))
            ?"My live location":PreferredLocation()?.Label ?? "Choose a location";
        void DeploySelectedStory()
        {
            var node=CampaignNodeGraph.GetNode(selectedNodeId);
            if(node==null || !CampaignNodeGraph.IsNodeUnlocked(node.id) || loading)return;
            StopBriefingAudio();campaignLocationEdit=false;campaignBriefingReviewed=true;
            showingCampaignStory=false;choosingStage=false;readinessContinue=true;
            PlayerPrefs.SetInt("GeoSniper.SelectedMode",0);PlayerPrefs.SetInt("GeoSniper.ActiveNodeId",node.id);
            rangeToStart=false;campaignNodeToStart=node.id;isPvPDuel=node.isPvPDuel;stageIndexToStart=node.isPvPDuel?-1:node.stageIndex;
            GameAnalyticsManager.TrackCustomEvent("campaign_deploy_clicked",new System.Collections.Generic.Dictionary<string,object>{{"stage_index",node.stageIndex+1},{"map_mode",UseStarterSector()?"training":"real_world"}});
            if(UseStarterSector())StartOffline(true);else StartPreferredLocationMission();
        }
        void SelectCampaignPlace(GameLocation place)
        {
            activeLocation=place;
            PlayerPrefs.SetString("GeoSniper.LocationSource","selected");
            PlayerPrefs.SetFloat("GeoSniper.SavedLat",(float)place.Latitude);PlayerPrefs.SetFloat("GeoSniper.SavedLon",(float)place.Longitude);
            PlayerPrefs.SetString("GeoSniper.SavedLabel",place.Label);PlayerPrefs.Save();
            campaignLocationEdit=false;
            if(campaignBriefingReviewed){resumeMissionAfterLocationConsent=false;StartCoroutine(Play(place));}
            else SwitchTab(LobbyTab.Campaign);
        }
        static string StoryActivity(LevelNode node)
        {
            if(node.isPvPDuel)return "Sniper Duel";
            switch(node.contractType)
            {
                case CampaignContractType.TargetIdentification:return "Target Hunt";
                case CampaignContractType.Overwatch:return "Protect & Escort";
                case CampaignContractType.TimedInterception:return "Courier Intercept";
                case CampaignContractType.EvidenceRecovery:return "Evidence Recovery";
                case CampaignContractType.Escape:return "Hold & Extract";
                default:return node.contractType.ToString();
            }
        }
        void DrawCampaignStory(float w,float h)
        {
            selectedNodeId=Mathf.Clamp(selectedNodeId,0,49);
            var node=CampaignNodeGraph.GetNode(selectedNodeId);if(node==null)return;
            LobbyFill(new Rect(0,0,w,h),CommandGUI.ThemeBg);
            float width=Mathf.Min(1040,w-40),height=Mathf.Min(620,h-32);
            var panel=new Rect((w-width)/2,(h-height)/2,width,height);
            LobbyFill(panel,CommandGUI.ThemeCard);
            var body=new GUIStyle(GUI.skin.label){fontSize=17,wordWrap=true,normal={textColor=CommandGUI.Text}};
            var small=new GUIStyle(body){fontSize=13,normal={textColor=CommandGUI.Muted}};
            var heading=new GUIStyle(body){fontSize=29,fontStyle=FontStyle.Bold};
            if(CommandGUI.DrawButton(new Rect(panel.x+22,panel.y+18,100,36),"LOBBY",false,12))SwitchTab(LobbyTab.Home);
            GUI.Label(new Rect(panel.x+138,panel.y+21,width-340,30),"GLASS MERIDIAN / "+(node.id+1)+" OF 50",new GUIStyle(small){normal={textColor=CommandGUI.AccentGold}});
            if(CommandGUI.DrawButton(new Rect(panel.xMax-176,panel.y+18,154,36),StoryNarrator.Enabled?"NARRATION ON":"NARRATION OFF",false,11))
            {StoryNarrator.Enabled=!StoryNarrator.Enabled;if(StoryNarrator.Enabled)PlayBriefingAudio(node.id,true);else StopBriefingAudio();}
            float y=panel.y+79;
            bool enabled=GUI.enabled;GUI.enabled=enabled && node.id>0;
            if(CommandGUI.DrawButton(new Rect(panel.x+22,y+8,44,46),"<",false,18)){selectedNodeId--;PlayBriefingAudio(selectedNodeId,true);}
            GUI.enabled=enabled && node.id<49;
            if(CommandGUI.DrawButton(new Rect(panel.xMax-66,y+8,44,46),">",false,18)){selectedNodeId++;PlayBriefingAudio(selectedNodeId,true);}
            GUI.enabled=enabled;
            GUI.Label(new Rect(panel.x+82,y,width-164,42),node.title,heading);
            GUI.Label(new Rect(panel.x+82,y+45,width-164,25),CampaignLocationProfile.ForStage(node.id).Name+" / "+StoryActivity(node)+" / "+DifficultyProfile.Selected,small);
            GUI.Label(new Rect(panel.x+28,panel.y+178,width-56,128),node.briefing,body);
            GUI.Label(new Rect(panel.x+28,panel.y+312,width-56,65),"OBJECTIVE: "+node.objective,new GUIStyle(body){fontSize=16,fontStyle=FontStyle.Bold});
            GUI.Label(new Rect(panel.x+28,panel.y+384,width-56,36),"Optional: "+CampaignStory.BonusDescription(node.bonusGoal)+" (+10% reward)",small);
            float infoY=panel.yMax-174;
            GUI.Label(new Rect(panel.x+28,infoY,width-210,25),"LOCATION / "+CampaignLocationLabel(),small);
            if(CommandGUI.DrawButton(new Rect(panel.xMax-158,infoY-3,130,32),"CHANGE",false,11)){campaignLocationEdit=true;SwitchTab(LobbyTab.Location);}
            int rifle=WeaponCatalog.SelectedOwnedWeapon;int rank=WeaponProgression.Tier(rifle,WeaponUpgrade.Damage);
            bool underpowered=CampaignProgression.Underpowered(node,CampaignProgression.ExpectedHealth(node,DifficultyProfile.Selected),rifle,rank);
            GUI.Label(new Rect(panel.x+28,infoY+38,width-210,42),"LOADOUT / "+WeaponCatalog.Get(rifle).Name+(underpowered?" — damage upgrade recommended":""),new GUIStyle(small){normal={textColor=underpowered?CommandGUI.AccentGold:CommandGUI.Muted}});
            if(CommandGUI.DrawButton(new Rect(panel.xMax-158,infoY+35,130,32),"ARMORY",false,11)){returnToCampaignFromArmory=true;SwitchTab(LobbyTab.Armory);}
            GUI.Label(new Rect(panel.x+28,panel.yMax-78,width-310,22),"+"+CampaignProgression.CreditsReward(node).ToString("N0")+" CR  /  +"+CampaignProgression.XPReward(node)+" XP"+(CampaignNodeGraph.IsNodeCompleted(node.id)?" / Replay":""),small);
            GUI.enabled=enabled && CampaignNodeGraph.IsNodeUnlocked(node.id);
            if(CommandGUI.DrawGoldPlayButton(new Rect(panel.xMax-278,panel.yMax-79,250,58),GUI.enabled?"PLAY MISSION":"LOCKED"))
            {
                GameAnalyticsManager.TrackCustomEvent("deploy_tap",new System.Collections.Generic.Dictionary<string,object>{{"action","play_mission"},{"stage_index",node.stageIndex+1},{"attempt_id",GameAnalyticsManager.NextDeploymentAttempt()}});
                DeploySelectedStory();
            }
            GUI.enabled=enabled;
        }
    }
}
