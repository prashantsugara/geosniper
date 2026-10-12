using System;
using UnityEngine;

namespace GeoSniper
{
    public sealed partial class GeoSniperGame
    {
        Vector2 armoryScroll, rewardsScroll;
        void DrawEconomyPage(float w,float h,bool armory)
        {
            bool scroll=!armory && h<690;
            if(scroll)
            {
                Vector2 position=armory?armoryScroll:rewardsScroll;
                position=GUI.BeginScrollView(new Rect(0,0,w,h),position,new Rect(0,0,w-20,690));
                if(armory) armoryScroll=position; else rewardsScroll=position;
                w-=20;h=690;
            }
            try { if(armory) DrawWeaponProgressionArmory(w,h); else DrawWeaponEconomyRewards(w,h); }
            finally { if(scroll) GUI.EndScrollView(); }
        }
        int armoryRifle = 1;
        string weaponEconomyNotice = "";
        bool currencyAdPrompt, currencyAdGold, readinessPrompt, readinessContinue, readinessLiveLocation;
        int currencyAdCost, readinessWeapon, readinessUpgradeRank, readinessReplacement=-1;
        float readinessHealth;
        int readinessNodeId=-1;
        bool EconomyModalOpen => currencyAdPrompt || readinessPrompt;

        bool CheckCampaignWeaponReadiness(bool liveLocation)
        {
            if(readinessContinue) {readinessContinue=false;return false;}
            var node=CampaignNodeGraph.GetNode(campaignNodeToStart);
            if(node==null || rangeToStart) return false;
            int rifle=WeaponCatalog.SelectedOwnedWeapon,rank=WeaponProgression.Tier(rifle,WeaponUpgrade.Damage);
            float health=CampaignProgression.ExpectedHealth(node,DifficultyProfile.Selected);
            if(!CampaignProgression.Underpowered(node,health,rifle,rank)) return false;
            readinessNodeId=node.id;readinessHealth=health;readinessWeapon=rifle;
            readinessUpgradeRank=CampaignProgression.RequiredDamageRank(health,rifle,rank);
            readinessReplacement=CampaignProgression.RecommendedReplacement(health,rifle);
            readinessLiveLocation=liveLocation;readinessPrompt=true;
            return true;
        }

        void OpenRecommendedArmory(int rifle)
        {
            readinessPrompt=false;OpenArmoryWeapon(rifle);
            int rank=CampaignProgression.RequiredDamageRank(readinessHealth,rifle,WeaponProgression.Tier(rifle,WeaponUpgrade.Damage));
            weaponEconomyNotice=$"Level {readinessNodeId+1}: recommended damage rank {rank} for {WeaponCatalog.Get(rifle).Name}. Buy if locked, upgrade, then equip.";
            StopBriefingAudio();SwitchTab(LobbyTab.Armory);OpenArmoryWeapon(rifle);
        }

        void OfferCurrencyAd(bool gold,int cost)
        {
            if(PlayerPrefs.GetInt(gold?"GeoSniper.Gold":"GeoSniper.Credits",0)>=cost) return;
            currencyAdGold=gold;currencyAdCost=cost;currencyAdPrompt=true;
        }

        void DrawEconomyModal()
        {
            if(!EconomyModalOpen) return;
            Rect safe=Screen.safeArea;
            float scale=CommandGUI.GetCanvasScale(safe,out float w,out float h);
            GUI.matrix=Matrix4x4.TRS(new Vector3(safe.x,Screen.height-safe.yMax,0),Quaternion.identity,new Vector3(scale,scale,1));
            CommandGUI.DrawPanel(new Rect(0,0,w,h),new Color(0,0,0,.88f),Color.clear,0);
            float width=Mathf.Min(640,w-32),height=Mathf.Min(350,h-24);
            Rect panel=new Rect((w-width)/2,(h-height)/2,width,height);
            CommandGUI.DrawPanel(panel,CommandGUI.ThemeCard,CommandGUI.AccentGold,2);
            if(currencyAdPrompt)
            {
                int balance=Mathf.Max(0,PlayerPrefs.GetInt(currencyAdGold?"GeoSniper.Gold":"GeoSniper.Credits",0));
                int reward=currencyAdGold?WeaponProgression.GoldAdReward:WeaponProgression.CashAdReward;
                int remaining=WeaponProgression.RewardAdsRemaining(DateTime.UtcNow.ToString("yyyyMMdd"));
                string unit=currencyAdGold?"GOLD":"CREDITS";
                EconomyText(new Rect(panel.x+20,panel.y+18,width-40,34),"NOT ENOUGH "+unit,20,CommandGUI.AccentGold);
                EconomyText(new Rect(panel.x+20,panel.y+64,width-40,112),$"Price: {currencyAdCost:N0} {unit} · Balance: {balance:N0}\nYou need {Mathf.Max(0,currencyAdCost-balance):N0} more.\nWatch an optional rewarded ad to earn +{reward} {unit}.\n{remaining}/{WeaponProgression.DailyAdLimit} currency ads remaining today.",15,Color.white);
                EconomyText(new Rect(panel.x+20,panel.y+182,width-40,52),remaining==0?"Daily limit reached. Earn credits in campaign or return tomorrow.":"Rewards are granted only after completing the ad. Return here to purchase once you have enough.",12,CommandGUI.Muted);
                bool enabled=GUI.enabled;GUI.enabled=enabled && remaining>0 && !AdManager.Instance.IsBusy;
                if(CommandGUI.DrawButton(new Rect(panel.x+20,panel.yMax-102,width-40,40),$"WATCH AD · +{reward} {unit}",true,14))
                {currencyAdPrompt=false;RequestCurrencyAd(currencyAdGold);}
                GUI.enabled=enabled;
                if(CommandGUI.DrawButton(new Rect(panel.x+20,panel.yMax-52,width-40,34),"RETURN TO ARMORY",false,13)) currencyAdPrompt=false;
            }
            else if(readinessPrompt)
            {
                int rank=WeaponProgression.Tier(readinessWeapon,WeaponUpgrade.Damage);
                int hits=CampaignProgression.BodyHits(readinessHealth,readinessWeapon,rank);
                string upgrade=readinessUpgradeRank>=0?$"Upgrade {WeaponCatalog.Get(readinessWeapon).Name} damage to rank {readinessUpgradeRank}.":"This weapon cannot reach the recommended damage at maximum rank.";
                EconomyText(new Rect(panel.x+20,panel.y+16,width-40,36),"WEAPON BELOW RECOMMENDED POWER",18,CommandGUI.AccentGold);
                int next=WeaponCatalog.NextRankUnlock();
                string rankAdvice=readinessReplacement<0 && next>=0?"\nNext weapon unlock: "+WeaponCatalog.Get(next).Name+" at player rank "+WeaponCatalog.RequiredRank(next):"";
                EconomyText(new Rect(panel.x+20,panel.y+58,width-40,128),$"MISSION {readinessNodeId+1} · Typical enemy: {readinessHealth:0} HP\n{WeaponCatalog.Get(readinessWeapon).Name}: about {hits} torso hits at close range.\n{upgrade}\nArmored enemies take actual headshot damage; accuracy still helps.{rankAdvice}",13,Color.white);
                float buttonY=panel.yMax-156;
                if(readinessUpgradeRank>=0 && CommandGUI.DrawButton(new Rect(panel.x+20,buttonY,width-40,40),"UPGRADE CURRENT WEAPON",true,13)) OpenRecommendedArmory(readinessWeapon);
                if(readinessReplacement>=0 && CommandGUI.DrawButton(new Rect(panel.x+20,buttonY+48,width-40,40),"ARMORY · "+WeaponCatalog.Get(readinessReplacement).Name,false,13)) OpenRecommendedArmory(readinessReplacement);
                if(CommandGUI.DrawButton(new Rect(panel.x+20,panel.yMax-54,(width-48)/2,36),"PLAY WITH CURRENT LOADOUT",false,11))
                {readinessPrompt=false;readinessContinue=true;StartPreferredLocationMission(readinessLiveLocation);}
                if(CommandGUI.DrawButton(new Rect(panel.center.x+4,panel.yMax-54,(width-48)/2,36),"BACK TO CAMPAIGN",false,12))
                {readinessPrompt=false;SwitchTab(LobbyTab.Campaign);}
            }
        }

        void DismissRankUp()
        {PlayerPrefs.DeleteKey("GeoSniper.RankUpFrom");PlayerPrefs.DeleteKey("GeoSniper.RankUpTo");PlayerPrefs.Save();}
        

        void RequestCurrencyAd(bool gold)
        {
            if(WeaponProgression.RewardAdsRemaining(DateTime.UtcNow.ToString("yyyyMMdd"))==0)
            { weaponEconomyNotice="Daily currency ad limit reached. Return tomorrow."; return; }
            var ads=AdManager.Instance;
            if(ads.IsBusy) return;
            bool paid=false;
            ads.ShowRewardedAd(success=>{
                if(paid) return;
                if(!success) { weaponEconomyNotice="Ad unavailable or unfinished. No reward was issued."; return; }
                if(paid) return;
                paid=true;
                bool granted=WeaponProgression.GrantAdCurrency(gold,DateTime.UtcNow.ToString("yyyyMMdd"));
                weaponEconomyNotice=granted?(gold?"+"+WeaponProgression.GoldAdReward+" GOLD EARNED":"+"+WeaponProgression.CashAdReward+" CREDITS EARNED"):"Daily currency reward limit reached.";
            },gold?"weapon_upgrade_gold":"weapon_upgrade_credits");
        }

        void DrawWeaponProgressionArmory(float w,float h) => DrawCategorizedArmory(w,h);

        static string UpgradeReading(int rifle,WeaponUpgrade upgrade,int tier)
        {
            switch(upgrade)
            {
                case WeaponUpgrade.Damage: return WeaponProgression.Damage(rifle,tier).ToString("0")+" damage";
                case WeaponUpgrade.Magazine: return WeaponProgression.Magazine(rifle,tier)+" rounds";
                case WeaponUpgrade.Scope: return (65/WeaponProgression.ScopeFov(rifle,tier)*2).ToString("0.0")+"× zoom";
                case WeaponUpgrade.Reload: return WeaponProgression.ReloadSeconds(rifle,tier).ToString("0.00")+" seconds";
                default: return Mathf.RoundToInt((1-WeaponProgression.StabilityScale(tier))*100)+"% less sway/recoil";
            }
        }
        static void EconomyText(Rect rect,string text,int size,Color color,TextAnchor anchor=TextAnchor.MiddleLeft)
            => GUI.Label(rect,text,GUIStyleCache.Get(size,FontStyle.Bold,anchor,color,true));

        void DrawWeaponEconomyRewards(float w,float h)
        {
            Rect panel=new Rect((w-Mathf.Min(720,w-24))/2,10,Mathf.Min(720,w-24),Mathf.Min(570,h-20));
            CommandGUI.DrawPanel(panel,CommandGUI.ThemeCard,CommandGUI.AccentGold,2);
            EconomyText(new Rect(panel.x+18,panel.y+12,panel.width-36,30),"REWARDS // WEAPON PROGRESSION",18,Color.white);
            string day=DateTime.UtcNow.ToString("yyyyMMdd");
            int remaining=WeaponProgression.RewardAdsRemaining(day);
            EconomyText(new Rect(panel.x+18,panel.y+47,panel.width-36,32),$"{PlayerPrefs.GetInt("GeoSniper.Gold",0)} GOLD · {PlayerPrefs.GetInt("GeoSniper.Credits",0):N0} CR · {remaining}/{WeaponProgression.DailyAdLimit} currency ads left today",13,CommandGUI.AccentCyan);
            string[] headings={"REWARDED GOLD","REWARDED CREDITS","DAILY SUPPLIES","DUEL BOUNTY VAULT"};
            for(int n=0;n<4;n++)
            {
                Rect card=new Rect(panel.x+16,panel.y+90+n*86,panel.width-32,76);
                CommandGUI.DrawPanel(card,CommandGUI.ThemeBg,CommandGUI.ThemeBorder,1);
                EconomyText(new Rect(card.x+12,card.y+8,card.width-190,24),headings[n],14,CommandGUI.AccentGold);
                int bounty=PlayerPrefs.GetInt("GeoSniper.BountyStash",0);
                string subtitle=n==0?$"+{WeaponProgression.GoldAdReward} gold for optics, reload and stability":n==1?$"+{WeaponProgression.CashAdReward} credits for damage and magazines":n==2?$"+{WeaponProgression.DailySupplyCredits} credits · resets on the next UTC day":$"{bounty:N0} credits earned from duel wins";
                EconomyText(new Rect(card.x+12,card.y+34,card.width-190,30),subtitle,12,Color.white);
                bool ready=n<2?remaining>0:n==2?PlayerPrefs.GetString("GeoSniper.LastAirdropDate","")!=day:bounty>0;
                bool previous=GUI.enabled;GUI.enabled=previous&&ready;
                if(CommandGUI.DrawButton(new Rect(card.xMax-160,card.y+16,146,44),n<2?"WATCH AD":ready?"CLAIM":"CLAIMED / EMPTY",true,12))
                {
                    if(n<2) RequestCurrencyAd(n==0);
                    else if(n==2)
                    {PlayerPrefs.SetInt("GeoSniper.Credits",(int)Math.Min(int.MaxValue,(long)Mathf.Max(0,PlayerPrefs.GetInt("GeoSniper.Credits",0))+WeaponProgression.DailySupplyCredits));PlayerPrefs.SetString("GeoSniper.LastAirdropDate",day);PlayerPrefs.Save();}
                    else {PlayerPrefs.SetInt("GeoSniper.Credits",PlayerPrefs.GetInt("GeoSniper.Credits",0)+bounty);PlayerPrefs.SetInt("GeoSniper.BountyStash",0);PlayerPrefs.Save();}
                }
                GUI.enabled=previous;
            }
            EconomyText(new Rect(panel.x+16,panel.y+438,panel.width-32,44),weaponEconomyNotice,12,Color.white);
            if(CommandGUI.DrawButton(new Rect(panel.x+16,panel.yMax-70,(panel.width-44)/2,42),"OPTIONAL PLAY STORE REVIEW",false,12)) AdManager.OpenPlayStoreRating();
            if(CommandGUI.DrawButton(new Rect(panel.center.x+6,panel.yMax-70,(panel.width-44)/2,42),"BACK TO LOBBY",false,13)) SwitchTab(LobbyTab.Home);
        }
    }
}
