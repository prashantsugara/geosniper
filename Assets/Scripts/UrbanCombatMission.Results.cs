using UnityEngine;
namespace GeoSniper
{
    public sealed partial class UrbanCombatMission
    {
        void ResultText(Rect rect,string text,int size,Color color)
        {GUI.Label(rect,text,GUIStyleCache.Get(size,FontStyle.Bold,TextAnchor.MiddleLeft,color,true));}
        int NewlyAvailableRifle()
        {
            int from=PlayerPrefs.GetInt("GeoSniper.RankUpFrom",PlayerProgression.Level),to=PlayerPrefs.GetInt("GeoSniper.RankUpTo",PlayerProgression.Level),found=-1;
            for(int i=0;i<WeaponCatalog.Count;i++)if(!WeaponCatalog.Owned(i) && WeaponCatalog.RequiredRank(i)>from && WeaponCatalog.RequiredRank(i)<=to && (found<0 || WeaponCatalog.RequiredRank(i)>WeaponCatalog.RequiredRank(found)))found=i;
            return found;
        }
        void ContinueFromResults(bool armory=false)
        {
            int unlocked=armory?NewlyAvailableRifle():-1;
            var game=GetComponent<GeoSniperGame>();
            int nextMission=NextPlayableMission();
            System.Action finish=()=>
            {
                if(!armory && nextMission>=0)
                {
                    GameAnalyticsManager.TrackCustomEvent("next_mission_selected",new System.Collections.Generic.Dictionary<string,object>{{"from_stage",stageIndex+1},{"to_stage",nextMission+1}});
                    StoryNarrator.StopOutcome(gameObject);
                    PlayerPrefs.DeleteKey("GeoSniper.RankUpFrom");PlayerPrefs.DeleteKey("GeoSniper.RankUpTo");
                    BeginStage(nextMission);StartPreparedCampaign();
                    if(currentMissionState==MissionState.InProgress)StoryNarrator.Play(gameObject,nextMission,false);
                    return;
                }
                ExitToLevelMap();if(unlocked>=0 && game!=null)game.ViewAvailableWeapon(unlocked);
            };
            if(armory)PlayerPrefs.SetInt("GeoSniper.OpenArmoryOnExit",1);
            AdManager.Instance?.HideNativeScreenAd();
            var ads=AdManager.Instance;
            if(ads!=null)
            {
                ads.NotifyStageCompleted();
                if(stageMode && CampaignAdPolicy.AllowsBreak(stageIndex,CampaignNodeGraph.GetCompletedCount(),Time.unscaledTime))
                {ads.ShowInterstitialIfReady(finish,"stage_transition");return;}
            }
            finish();
        }
        int NextPlayableMission()
        {
            if(campaignNode?.nextNodeIds==null)return -1;
            foreach(int id in campaignNode.nextNodeIds)
                if(CampaignNodeGraph.IsNodeUnlocked(id) && CampaignNodeGraph.GetNode(id)!=null)return CampaignNodeGraph.GetNode(id).stageIndex;
            return -1;
        }
        void DrawVictoryRewardScreen(float w,float h)
        {
            if(campaignNode!=null && !storyOutcomeNarrated){storyOutcomeNarrated=true;StoryNarrator.Play(gameObject,campaignNode.id,true);}
            CommandGUI.Fill(new Rect(0,0,w,h),new Color(.02f,.04f,.07f,.82f));
            float width=Mathf.Min(620,w-24),height=Mathf.Min(620,h-20);
            var r=new Rect((w-width)/2,(h-height)/2,width,height);
            TacticalGUI.DrawPanel(r,CommandGUI.ThemeCard,CommandGUI.AccentGold,2);
            float x=r.x+24,inner=width-48;
            // A brief gold burst celebrates success without adding another navigation screen.
            if(victoryScreenTimer<2.4f)for(int i=0;i<18;i++)
            {
                float phase=Mathf.Clamp01(victoryScreenTimer/2.4f),angle=i*Mathf.PI*2/18;
                float radius=30+phase*170;
                var spark=new Rect(r.center.x+Mathf.Cos(angle)*radius,r.y+75+Mathf.Sin(angle)*radius*.32f+phase*phase*80,3,6);
                CommandGUI.Fill(spark,new Color(1,.75f,.24f,(1-phase)*.8f));
            }
            bool compact=height<520;
            ResultText(new Rect(x,r.y+20,inner,34),"MISSION COMPLETE",25,CommandGUI.AccentGold);
            ResultText(new Rect(x,r.y+57,inner,26),campaignNode!=null?campaignNode.title:"OPERATION SUCCESSFUL",17,Color.white);
            float celebration=Mathf.Clamp01(victoryScreenTimer/1.2f);
            string medals=new string('★',Mathf.Clamp(rewardStars,1,3));
            GUI.Label(new Rect(x,r.y+80,inner,30),medals,GUIStyleCache.Get(24,FontStyle.Bold,TextAnchor.MiddleCenter,Color.Lerp(CommandGUI.Muted,CommandGUI.AccentGold,celebration),false));
            ResultText(new Rect(x,r.y+112,inner,30),"+"+Mathf.RoundToInt(rewardCash*celebration).ToString("N0")+" CR     +"+Mathf.RoundToInt(rewardXP*celebration)+" XP     /     REWARDS DEPOSITED",18,CommandGUI.AccentCyan);
            int accuracy=totalShotsFired>0?Mathf.Clamp(Mathf.RoundToInt(100f*totalHitsScored/totalShotsFired),0,100):0;
            ResultText(new Rect(x,r.y+155,inner,35),accuracy+"% accuracy   /   "+totalHeadshotsScored+" headshots   /   "+Mathf.CeilToInt(missionDuration)+"s",14,CommandGUI.Muted);
            bool rankUp=PlayerPrefs.GetInt("GeoSniper.RankUpTo",0)>0;
            int unlocked=NewlyAvailableRifle(),next=WeaponCatalog.NextRankUnlock();
            if(!compact)
            {
            ResultText(new Rect(x,r.y+204,inner,28),(rankUp?"RANK UP! ":"")+"LEVEL "+PlayerProgression.Level+" / "+PlayerProgression.Title,17,rankUp?CommandGUI.AccentGold:Color.white);
            CommandGUI.DrawProgressBar(new Rect(x,r.y+237,inner,8),PlayerProgression.Progress,CommandGUI.AccentGold,CommandGUI.ThemeBg);
            string goal=unlocked>=0?"AVAILABLE TO BUY: "+WeaponCatalog.Get(unlocked).Name+" / "+WeaponCatalog.Get(unlocked).UnlockCost.ToString("N0")+(WeaponCatalog.Get(unlocked).Gold?" GOLD":" CR")
                :next>=0?"NEXT WEAPON: "+WeaponCatalog.Get(next).Name+" / LEVEL "+WeaponCatalog.RequiredRank(next):"All weapon rank requirements met";
            ResultText(new Rect(x,r.y+261,inner,48),goal,14,CommandGUI.AccentCyan);
            if(campaignNode!=null)ResultText(new Rect(x,r.y+316,inner,72),campaignNode.storyOutcome+"\nREPLAY CHALLENGE: "+CampaignStory.BonusDescription(campaignNode.bonusGoal),14,Color.white);
            }
            else if(campaignNode!=null)ResultText(new Rect(x,r.y+190,inner,32),"REPLAY: "+CampaignStory.BonusDescription(campaignNode.bonusGoal),12,CommandGUI.AccentCyan);
            bool enabled=GUI.enabled;GUI.enabled=enabled && !(AdManager.Instance!=null && AdManager.Instance.IsBusy);
            if(stageIndex>=3 && !stageRewardsDoubled && TacticalGUI.DrawButton(new Rect(x,r.yMax-174,inner,40),"OPTIONAL AD / DOUBLE MISSION REWARDS",false,12))
            {
                AdManager.Instance?.ShowRewardedAd(success=>
                {
                    if(!success || stageRewardsDoubled)return;
                    stageRewardsDoubled=true;
                    PlayerPrefs.SetInt("GeoSniper.Credits",PlayerPrefs.GetInt("GeoSniper.Credits",0)+rewardCash);
                    PlayerProgression.AddXP(rewardXP);PlayerPrefs.Save();rewardCash*=2;rewardXP*=2;
                },"victory_double_reward");
            }
            bool hasNext=campaignNode!=null && campaignNode.nextNodeIds!=null && campaignNode.nextNodeIds.Length>0;
            if(TacticalGUI.DrawGreenPlayButton(new Rect(x,r.yMax-120,inner,50),hasNext?"NEXT MISSION":"BACK TO CAMPAIGN"))ContinueFromResults();
            float gap=8,buttonWidth=(inner-(unlocked>=0?2:1)*gap)/(unlocked>=0?3:2);
            if(TacticalGUI.DrawButton(new Rect(x,r.yMax-54,buttonWidth,34),"REPLAY",false,12))
            {PlayerPrefs.DeleteKey("GeoSniper.RankUpFrom");PlayerPrefs.DeleteKey("GeoSniper.RankUpTo");if(stageMode)BeginStage(stageIndex);else Restart();}
            if(TacticalGUI.DrawButton(new Rect(x+buttonWidth+gap,r.yMax-54,buttonWidth,34),"LOBBY",false,12))
            {ExitToLevelMap();GetComponent<GeoSniperGame>()?.SwitchTab(GeoSniperGame.LobbyTab.Home);}
            if(unlocked>=0 && TacticalGUI.DrawButton(new Rect(x+2*(buttonWidth+gap),r.yMax-54,buttonWidth,34),"VIEW ARMORY",false,12))ContinueFromResults(true);
            GUI.enabled=enabled;
        }
    }
    public static class CampaignAdPolicy
    {
        public static bool AllowsBreak(int stageIndex,int completedMissions,float sessionSeconds)
            =>stageIndex>=3 && completedMissions>=3 && sessionSeconds>=300;
    }
}
