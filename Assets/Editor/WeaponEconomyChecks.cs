using System;
using System.Collections.Generic;
using System.IO;
using GeoSniper;
using UnityEngine;

public static class WeaponEconomyChecks
{
    static void Check(bool value,string message) { if(!value) throw new Exception(message); }
    public static void Run()
    {
        var keys=new List<string>{"GeoSniper.Gold","GeoSniper.Credits","GeoSniper.LastRewardAdDate","GeoSniper.RewardAdsToday","GeoSniper.WeaponUnlocked_2","GeoSniper.SelectedWeapon","GeoSniper.XP","GeoSniper.PlayerRank","GeoSniper.RankUpFrom","GeoSniper.RankUpTo",WeaponProgression.Key(1,WeaponUpgrade.Damage)};
        for(int n=0;n<5;n++) keys.Add(WeaponProgression.Key(2,(WeaponUpgrade)n));
        var ints=new Dictionary<string,int>(); var exists=new HashSet<string>();
        string date=PlayerPrefs.GetString("GeoSniper.LastRewardAdDate","");
        foreach(string k in keys) { if(PlayerPrefs.HasKey(k)) exists.Add(k); ints[k]=PlayerPrefs.GetInt(k); }
        var root=new GameObject("Economy combat fixture");root.SetActive(false);
        var ballistic=ScriptableObject.CreateInstance<BallisticsConfig>();
        var weapon=ScriptableObject.CreateInstance<WeaponConfig>();
        var menuRoot=new GameObject("Readiness menu fixture");menuRoot.SetActive(false);
        try
        {
            foreach(string k in keys) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.SetInt("GeoSniper.WeaponUnlocked_2",1);
            PlayerPrefs.SetInt("GeoSniper.Credits",1499);
            Check(!WeaponProgression.TryUpgrade(2,WeaponUpgrade.Damage),"Insufficient funds purchased damage");
            Check(PlayerPrefs.GetInt("GeoSniper.Credits")==1499,"Failed purchase debited currency");
            PlayerPrefs.SetInt("GeoSniper.Credits",200000);
            for(int i=0;i<6;i++) Check(WeaponProgression.TryUpgrade(2,WeaponUpgrade.Damage),"Damage purchase failed");
            int balance=PlayerPrefs.GetInt("GeoSniper.Credits");
            Check(!WeaponProgression.TryUpgrade(2,WeaponUpgrade.Damage)&&PlayerPrefs.GetInt("GeoSniper.Credits")==balance,"Max tier charged currency");
            for(int i=1;i<6;i++) Check(WeaponProgression.Cost(WeaponUpgrade.Damage,i)>WeaponProgression.Cost(WeaponUpgrade.Damage,i-1),"Cost does not grow");
            PlayerPrefs.SetInt("GeoSniper.Gold",25);
            Check(WeaponProgression.TryUpgrade(2,WeaponUpgrade.Reload)&&PlayerPrefs.GetInt("GeoSniper.Gold")==0,"Reload must use gold");
            Check(PlayerPrefs.GetInt("GeoSniper.Credits")==balance,"Gold upgrade charged credits");
            PlayerPrefs.SetInt("GeoSniper.Gold",-150);
            Check(WeaponProgression.GrantAdCurrency(true,"fixture-day"),"Old negative gold blocks recovery");
            Check(PlayerPrefs.GetInt("GeoSniper.Gold")==50,"Gold reward wrong");
            Check(WeaponProgression.GrantAdCurrency(false,"fixture-day"),"Credit ad failed");
            Check(WeaponProgression.GrantAdCurrency(true,"fixture-day"),"Third ad failed");
            Check(!WeaponProgression.GrantAdCurrency(true,"fixture-day"),"Shared daily limit bypassed");
            Check(WeaponProgression.RewardAdsRemaining("next-day")==3,"Day rollover failed");
            for(int n=0;n<5;n++) PlayerPrefs.SetInt(WeaponProgression.Key(2,(WeaponUpgrade)n),6);
            WeaponProgression.Apply(2,ballistic,weapon,false);
            Check(ballistic.baseDamage==105&&weapon.magazineSize==60,"Campaign stats not applied");
            Check(Mathf.Abs(weapon.reloadTime-1.1f*.65f)<.001f&&Mathf.Abs(weapon.recoilPitch-5.4f)<.001f,"Reload/recoil caps wrong");
            root.SetActive(true);
            var actor=root.AddComponent<CombatActor>();root.AddComponent<EnemyBot>();var collider=root.AddComponent<BoxCollider>();
            actor.Initialize(100);
            var initialHit=DamageSystem.ProcessHit(collider,WeaponProgression.Damage(2,0),root.transform.position+Vector3.up);
            Check(!actor.IsDead&&actor.Health==55,"Base MK12 torso fixture: HP="+actor.Health+", part="+initialHit.bodyPart+", damage="+initialHit.TotalDamage);
            DamageSystem.ProcessHit(collider,45,Vector3.up);
            Check(!actor.IsDead&&actor.Health==10,"Base second torso hit must not kill");
            DamageSystem.ProcessHit(collider,45,Vector3.up); Check(actor.IsDead,"Base third hit should kill");
            actor.Initialize(100);DamageSystem.ProcessHit(collider,ballistic.baseDamage,Vector3.up);
            Check(actor.IsDead,"Upgraded MK12 torso shot must kill standard enemy");
            actor.Initialize(150);DamageSystem.ProcessHit(collider,ballistic.baseDamage,Vector3.up);
            Check(!actor.IsDead,"Tougher enemies should survive one upgraded torso hit");
            var baseline=ScriptableObject.CreateInstance<DifficultyProfile>();
            float previousHealth=0;
            try
            {
                foreach(var node in CampaignNodeGraph.Nodes)
                {
                    var profile=CampaignProgression.CreateProfile(baseline,node);
                    try
                    {
                        float hp=100*profile.enemyHealth;
                        Check(hp>previousHealth,"Enemy durability must increase at every campaign level");
                        previousHealth=hp;
                        int rank=CampaignProgression.RequiredDamageRank(hp,1,0);
                        Check(rank>=0 && CampaignProgression.BodyHits(hp,1,rank)<=2,"Starter upgrade path cannot meet campaign recommendation");
                    }
                    finally {UnityEngine.Object.DestroyImmediate(profile);}
                }
            }
            finally {UnityEngine.Object.DestroyImmediate(baseline);}
            Check(Mathf.Abs(previousHealth-210)<.01f,"Standard final-level health should be 210");
            Check(!CampaignProgression.Underpowered(CampaignNodeGraph.GetNode(0),60,1,0),"Fresh starter incorrectly fails training readiness");
            Check(CampaignProgression.Underpowered(CampaignNodeGraph.GetNode(49),210,1,0),"Late campaign does not warn about weak starter");
            PlayerPrefs.SetInt("GeoSniper.XP",0);PlayerPrefs.SetInt("GeoSniper.PlayerRank",1);
            int lowRankRecommendation=CampaignProgression.RecommendedReplacement(210,1);
            Check(lowRankRecommendation<0 || WeaponCatalog.RankEligible(lowRankRecommendation),"Recommendation suggests a rank-locked gun");
            PlayerProgression.AddXP(14000);
            int recommendation=CampaignProgression.RecommendedReplacement(210,1);
            Check(recommendation>=0 && recommendation!=1,"Late campaign lacks a replacement recommendation");
            PlayerPrefs.SetInt("GeoSniper.SelectedWeapon",1);
            var menu=menuRoot.AddComponent<GeoSniperGame>();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(GeoSniperGame).GetField("campaignNodeToStart",flags).SetValue(menu,49);
            Check((bool)typeof(GeoSniperGame).GetMethod("CheckCampaignWeaponReadiness",flags).Invoke(menu,new object[]{false}),"Weak loadout did not open pre-deployment warning");
            typeof(GeoSniperGame).GetMethod("OpenRecommendedArmory",flags).Invoke(menu,new object[]{recommendation});
            Check(menu.currentTab==GeoSniperGame.LobbyTab.Armory && (int)typeof(GeoSniperGame).GetField("armoryRifle",flags).GetValue(menu)==recommendation,"Recommendation did not open the relevant armory weapon");
            typeof(GeoSniperGame).GetMethod("OfferCurrencyAd",flags).Invoke(menu,new object[]{false,PlayerPrefs.GetInt("GeoSniper.Credits")+1});
            Check((bool)typeof(GeoSniperGame).GetField("currencyAdPrompt",flags).GetValue(menu) && !(bool)typeof(GeoSniperGame).GetField("currencyAdGold",flags).GetValue(menu),"Credit shortage did not offer correct rewarded currency");
            typeof(GeoSniperGame).GetMethod("OfferCurrencyAd",flags).Invoke(menu,new object[]{true,PlayerPrefs.GetInt("GeoSniper.Gold")+1});
            Check((bool)typeof(GeoSniperGame).GetField("currencyAdGold",flags).GetValue(menu),"Gold shortage offers credits instead");
            actor.AllowExecutionHeadshots=false;actor.Initialize(210);
            DamageSystem.ProcessHit(collider,20,Vector3.up*1.7f,4);
            Check(!actor.IsDead && actor.Health==130,"Armored target incorrectly allows a weak execution headshot");
            actor.Initialize(210);DamageSystem.ProcessHit(collider,WeaponProgression.Damage(1,3),Vector3.up*1.7f,4);
            Check(actor.IsDead,"Upgraded starter headshot should defeat armored final-level enemy");
            actor.Initialize(210);DamageSystem.ProcessHit(collider,WeaponProgression.Damage(0,6),Vector3.up);
            Check(actor.IsDead,"Fully upgraded Barrett should defeat final-level enemy with one torso shot");
            actor.AllowExecutionHeadshots=true;
            WeaponProgression.Apply(2,ballistic,weapon,true);
            Check(ballistic.baseDamage==45&&weapon.magazineSize==30&&Mathf.Abs(weapon.reloadTime-1.1f)<.001f&&weapon.recoilPitch==9,"Duel upgrades grant unfair power");
            Directory.CreateDirectory("Logs");File.WriteAllText("Logs/WeaponEconomyChecks.txt","PASS: currency purchases, caps, rewarded limits, recovery, 50-level durability progression, upgrade/replacement recommendations, armored campaign hit pipeline and normalized duel stats.\n");
        }
        finally
        {
            foreach(string k in keys) { if(!exists.Contains(k)) PlayerPrefs.DeleteKey(k); else if(k=="GeoSniper.LastRewardAdDate") PlayerPrefs.SetString(k,date); else PlayerPrefs.SetInt(k,ints[k]); }
            PlayerPrefs.Save();UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(menuRoot);UnityEngine.Object.DestroyImmediate(ballistic);UnityEngine.Object.DestroyImmediate(weapon);
        }
    }
}
