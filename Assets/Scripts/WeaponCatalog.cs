using UnityEngine;
namespace GeoSniper
{
    public sealed class WeaponDefinition
    {
        public readonly string Name, Class, Caliber, Model;
        public readonly float Damage, DamageStep, Reload, Fov, Cadence, Recoil, Speed, Range, Length, Noise;
        public readonly int Magazine, MagazineStep, UnlockCost;
        public readonly bool Gold, Automatic;
        public WeaponDefinition(string name,string role,string caliber,string model,float damage,float step,int magazine,int magazineStep,float reload,float fov,float cadence,float recoil,float speed,float range,float length,int price,bool gold=false,bool automatic=false,float noise=130)
        {Name=name;Class=role;Caliber=caliber;Model=model;Damage=damage;DamageStep=step;Magazine=magazine;MagazineStep=magazineStep;Reload=reload;Fov=fov;Cadence=cadence;Recoil=recoil;Speed=speed;Range=range;Length=length;UnlockCost=price;Gold=gold;Automatic=automatic;Noise=noise;}
    }
    public static class WeaponCatalog
    {
        // Stable IDs: the first three match existing saves and network loadouts.
        public static readonly WeaponDefinition[] All={
            new WeaponDefinition("BARRETT .50","ANTI-MATERIEL",".50 BMG","Barrett50",120,24,5,2,2.2f,10,.55f,140,853,1200,1.25f,300,true),
            new WeaponDefinition("M24 TACTICAL","STARTER BOLT-ACTION",".338 MATCH","M24Tactical",40,12,5,1,1.8f,14,1.05f,95,790,1200,1.08f,0),
            new WeaponDefinition("MK12 SPR","SUPPRESSED MARKSMAN","5.56 NATO","MK12SPR",45,10,30,5,1.1f,18,.32f,60,905,1200,1.02f,7500,noise:25),
            new WeaponDefinition("M40 SCOUT","LIGHT BOLT-ACTION","7.62 MATCH","M40Scout",90,12,5,1,1.8f,16,.85f,80,800,1100,1.12f,5500),
            new WeaponDefinition("SVD MARKSMAN","SEMI-AUTO MARKSMAN","7.62 R","SVDMarksman",65,10,10,2,1.65f,20,.42f,72,830,1000,1.22f,9000),
            new WeaponDefinition("M4A1 ASSAULT","AUTOMATIC RIFLE","5.56 NATO","M16Assault",28,4,30,3,1.7f,32,.12f,34,900,650,1f,4500,automatic:true),
            new WeaponDefinition("MP5 COMPACT","CLOSE-RANGE SMG","9mm","MP5Compact",22,3,30,3,1.35f,42,.09f,24,400,220,.68f,3500,automatic:true,noise:90),
            new WeaponDefinition("EAGLE SIDEARM","STARTER SEMI-AUTO PISTOL",".50 AE","EagleSidearm",18,14,7,1,1.25f,45,.38f,90,470,300,.3f,0,noise:100)
        };
        public static int Count=>All.Length;
        public static bool Valid(int index)=>index>=0&&index<Count;
        public static WeaponDefinition Get(int index)=>All[Mathf.Clamp(index,0,Count-1)];
        public const int StarterPrimary=1, StarterSecondary=7;
        public static bool IsStarter(int index)=>index==StarterPrimary || index==StarterSecondary;
        public static int RequiredRank(int index)
        {
            switch(index) {case 0:return 15;case 2:return 6;case 3:return 4;case 4:return 9;case 5:return 3;case 6:return 2;default:return 1;}
        }
        public static bool RankEligible(int index)=>Valid(index) && (Owned(index)||PlayerProgression.Level>=RequiredRank(index));
        public static int NextRankUnlock()
        {
            int next=-1;
            for(int i=0;i<Count;i++)
                if(!Owned(i) && RequiredRank(i)>PlayerProgression.Level && (next<0 || RequiredRank(i)<RequiredRank(next))) next=i;
            return next;
        }
        public static string BuyingAdvice(int index)
        {
            switch(index)
            {
                case 0:return "Heavy armor and distant targets. Highest damage, slow reload.";
                case 1:return "Starter sniper. Upgrade damage for affordable precision kills.";
                case 2:return "Stealth contracts. Suppressed shots and quick follow-ups.";
                case 3:return "First sniper upgrade. High damage; small magazine.";
                case 4:return "Moving targets and groups. Fast semi-auto follow-up shots.";
                case 5:return "Close-to-mid-range groups. Automatic fire; multiple hits.";
                case 6:return "Close-range defense. Fast fire, limited long-range reach.";
                default:return "Backup sidearm. Close-range emergencies; limited reach.";
            }
        }
        public static bool Owned(int index)=>Valid(index)&&(IsStarter(index)||PlayerPrefs.GetInt("GeoSniper.WeaponUnlocked_"+index,0)==1);
        public static int SelectedOwnedWeapon
        {
            get { int selected=PlayerPrefs.GetInt("GeoSniper.SelectedWeapon",StarterPrimary);return Owned(selected)?selected:StarterPrimary; }
        }
        public static int StartingSniper
        {
            get { int selected=SelectedOwnedWeapon;return selected<5?selected:StarterPrimary; }
        }
        public static void EnsureStarterLoadout()
        {
            PlayerProgression.EnsureRank();
            // Preserve magazine capacity bought before the starter balance update.
            if(!PlayerPrefs.HasKey("GeoSniper.StarterBalanceV2"))
            {
                int tier=WeaponProgression.Tier(StarterPrimary,WeaponUpgrade.Magazine);
                if(tier>0){PlayerPrefs.SetInt("GeoSniper.StarterLegacyMagazine",20+5*tier);PlayerPrefs.SetInt("GeoSniper.StarterLegacyMagazineTier",tier);}
                PlayerPrefs.SetInt("GeoSniper.StarterBalanceV2",1);PlayerPrefs.Save();
            }
            bool changed=false;
            foreach(int index in new[]{StarterPrimary,StarterSecondary})
                if(PlayerPrefs.GetInt("GeoSniper.WeaponUnlocked_"+index,0)!=1)
                {PlayerPrefs.SetInt("GeoSniper.WeaponUnlocked_"+index,1);changed=true;}
            int selected=SelectedOwnedWeapon;
            if(!PlayerPrefs.HasKey("GeoSniper.SelectedWeapon") || PlayerPrefs.GetInt("GeoSniper.SelectedWeapon")!=selected)
            {PlayerPrefs.SetInt("GeoSniper.SelectedWeapon",selected);changed=true;}
            if(changed)PlayerPrefs.Save();
        }
        public static bool TryUnlock(int index)
        {
            if(!Valid(index)||Owned(index)||!RankEligible(index))return false;
            var gun=Get(index);string key=gun.Gold?"GeoSniper.Gold":"GeoSniper.Credits";
            int balance=PlayerPrefs.GetInt(key,0);if(balance<gun.UnlockCost)return false;
            PlayerPrefs.SetInt(key,balance-gun.UnlockCost);PlayerPrefs.SetInt("GeoSniper.WeaponUnlocked_"+index,1);PlayerPrefs.Save();return true;
        }
    }
}
