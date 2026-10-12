using System;
using UnityEngine;

namespace GeoSniper
{
    public static class PlayerProgression
    {
        public const int MaxLevel=50, XPPerLevel=1000;
        public static int XP => Mathf.Max(0,PlayerPrefs.GetInt("GeoSniper.XP",0));
        public static int Level => Mathf.Clamp(Mathf.Max(PlayerPrefs.GetInt("GeoSniper.PlayerRank",1),1+XP/XPPerLevel),1,MaxLevel);
        public static int XPInLevel => Mathf.Clamp(XP-(Level-1)*XPPerLevel,0,XPPerLevel);
        public static float Progress => Level==MaxLevel?1:XPInLevel/(float)XPPerLevel;
        public static string Title => Level>=30?"ELITE SNIPER":Level>=15?"VETERAN":Level>=6?"MARKSMAN":"RECRUIT";
        public static void EnsureRank()
        {int rank=Level;if(PlayerPrefs.GetInt("GeoSniper.PlayerRank",0)!=rank){PlayerPrefs.SetInt("GeoSniper.PlayerRank",rank);PlayerPrefs.Save();}}
        public static void AddXP(int amount)
        {
            if(amount<=0) return;
            int previous=Level;
            PlayerPrefs.SetInt("GeoSniper.XP",(int)System.Math.Min(int.MaxValue,(long)XP+amount));
            int current=Level;PlayerPrefs.SetInt("GeoSniper.PlayerRank",current);
            if(current>previous)
            {
                if(PlayerPrefs.GetInt("GeoSniper.RankUpTo",0)==0) PlayerPrefs.SetInt("GeoSniper.RankUpFrom",previous);
                PlayerPrefs.SetInt("GeoSniper.RankUpTo",current);
            }
            PlayerPrefs.Save();
        }
    }
    public enum WeaponUpgrade { Damage, Magazine, Scope, Reload, Stability }

    public static class WeaponProgression
    {
        public const int MaxTier = 6;
        public const int GoldAdReward = 5;
        public const int CashAdReward = 200;
        public const int DailyAdLimit = 2;
        public const int DailySupplyCredits = 150;
        static readonly string[] keys = { "WpnDmgLvl_", "WpnMagLvl_", "WpnScopeLvl_", "WpnReloadLvl_", "WpnStabilityLvl_" };
        public static string Key(int rifle, WeaponUpgrade upgrade) => "GeoSniper." + keys[(int)upgrade] + Mathf.Clamp(rifle,0,WeaponCatalog.Count-1);
        public static int Tier(int rifle, WeaponUpgrade upgrade) => Mathf.Clamp(PlayerPrefs.GetInt(Key(rifle,upgrade),0),0,MaxTier);
        public static bool UsesGold(WeaponUpgrade upgrade) => upgrade==WeaponUpgrade.Scope || upgrade==WeaponUpgrade.Reload || upgrade==WeaponUpgrade.Stability;
        public static int Cost(WeaponUpgrade upgrade, int currentTier)
        {
            if(currentTier<0 || currentTier>=MaxTier) return 0;
            int initial=upgrade==WeaponUpgrade.Damage?1500:upgrade==WeaponUpgrade.Magazine?1000:upgrade==WeaponUpgrade.Scope?40:25;
            float growth=upgrade==WeaponUpgrade.Magazine?1.65f:1.45f;
            return Mathf.CeilToInt(initial*Mathf.Pow(growth,currentTier));
        }
        public static float Damage(int rifle,int tier)
        {
            tier=Mathf.Clamp(tier,0,MaxTier);
            // Preserve the old two tiers' power for existing players, then grow linearly.
            var gun=WeaponCatalog.Get(rifle); return rifle==WeaponCatalog.StarterPrimary?Mathf.Max(gun.Damage+gun.DamageStep*tier,20+17*tier):gun.Damage+gun.DamageStep*tier;
        }
        public static int Magazine(int rifle,int tier) => Mathf.Max(WeaponCatalog.Get(rifle).Magazine+Mathf.Clamp(tier,0,MaxTier)*WeaponCatalog.Get(rifle).MagazineStep,rifle==WeaponCatalog.StarterPrimary && tier>0?PlayerPrefs.GetInt("GeoSniper.StarterLegacyMagazine",0)+Mathf.Max(0,tier-PlayerPrefs.GetInt("GeoSniper.StarterLegacyMagazineTier",tier))*WeaponCatalog.Get(rifle).MagazineStep:0);
        public static float ReloadSeconds(int rifle,int tier) => WeaponCatalog.Get(rifle).Reload*(1-Reduction(tier,.35f));
        public static float StabilityScale(int tier) => 1-Reduction(tier,.4f);
        static float Reduction(int tier,float cap) => cap*Mathf.Log(1+Mathf.Clamp(tier,0,MaxTier))/Mathf.Log(1+MaxTier);
        public static float ScopeFov(int rifle,int tier)
        {
            float original=WeaponCatalog.Get(rifle).Fov;
            if(rifle>=5) return original-5f*Mathf.Log(1+Mathf.Clamp(tier,0,MaxTier))/Mathf.Log(1+MaxTier);
            // Match or improve the scopes already purchased under the two-tier system.
            tier=Mathf.Clamp(tier,0,MaxTier);
            float firstTwo=Mathf.Max(3,original-Mathf.Min(tier,2)*3.5f);
            return tier<=2?firstTwo:firstTwo/(1+.12f*(tier-2));
        }
        public static bool TryUpgrade(int rifle,WeaponUpgrade upgrade)
        {
            if(!WeaponCatalog.Owned(rifle)) return false;
            int tier=Tier(rifle,upgrade), cost=Cost(upgrade,tier);
            string currency=UsesGold(upgrade)?"GeoSniper.Gold":"GeoSniper.Credits";
            int balance=PlayerPrefs.GetInt(currency,0);
            if(cost<=0 || balance<cost) return false;
            PlayerPrefs.SetInt(currency,balance-cost);
            PlayerPrefs.SetInt(Key(rifle,upgrade),tier+1);
            PlayerPrefs.Save();
            GameAnalyticsManager.TrackWeaponUpgrade(rifle,upgrade.ToString(),tier+1,cost);
            return true;
        }
        public static void Apply(int rifle,BallisticsConfig ballistic,WeaponConfig weapon,bool duel)
        {
            int damage=duel?0:Tier(rifle,WeaponUpgrade.Damage), scope=duel?0:Tier(rifle,WeaponUpgrade.Scope);
            int reload=duel?0:Tier(rifle,WeaponUpgrade.Reload), stability=duel?0:Tier(rifle,WeaponUpgrade.Stability);
            ballistic.baseDamage=Damage(rifle,damage);
            ballistic.lethalHeadshots=rifle<5;ballistic.headshotMultiplier=rifle<5?4:2;
            var gun=WeaponCatalog.Get(rifle);
            ballistic.noiseRadius=gun.Noise;ballistic.bulletSpeed=gun.Speed;ballistic.maxRange=gun.Range;
            float fov=ScopeFov(rifle,scope);
            weapon.zoomLevels=new[]{fov,fov*.5f};
            weapon.magazineSize=Magazine(rifle,duel?0:Tier(rifle,WeaponUpgrade.Magazine));
            weapon.reloadTime=ReloadSeconds(rifle,reload);
            float steady=StabilityScale(stability);
            weapon.recoilPitch=9*steady; weapon.recoilYaw=steady;
            weapon.idleSwayAmount=.5f*steady; weapon.moveSwayAmount=2*steady;
            weapon.fireRate=gun.Cadence;
        }
        public static int RewardAdsRemaining(string day)
        {
            int used=PlayerPrefs.GetString("GeoSniper.LastRewardAdDate","")==day?PlayerPrefs.GetInt("GeoSniper.RewardAdsToday",0):0;
            return Mathf.Clamp(DailyAdLimit-used,0,DailyAdLimit);
        }
        public static bool GrantAdCurrency(bool gold,string day)
        {
            if(RewardAdsRemaining(day)==0) return false;
            string key=gold?"GeoSniper.Gold":"GeoSniper.Credits";
            int amount=gold?GoldAdReward:CashAdReward;
            int balance=Mathf.Max(0,PlayerPrefs.GetInt(key,0));
            if(balance>int.MaxValue-amount) return false;
            int used=DailyAdLimit-RewardAdsRemaining(day);
            PlayerPrefs.SetInt(key,balance+amount);
            PlayerPrefs.SetString("GeoSniper.LastRewardAdDate",day);
            PlayerPrefs.SetInt("GeoSniper.RewardAdsToday",used+1);
            PlayerPrefs.Save(); return true;
        }
    }
}
