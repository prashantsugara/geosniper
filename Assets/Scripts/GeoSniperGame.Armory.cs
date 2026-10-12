using UnityEngine;

namespace GeoSniper
{
    public sealed partial class GeoSniperGame
    {
        int armoryCategory=-1;
        bool armoryUpgrades;
        Vector2 armoryGunScroll, armoryUpgradeScroll;
        static readonly string[] ArmoryCategories={"SNIPER RIFLES","ASSAULT RIFLES","SMGs","SIDEARMS"};
        static readonly string[] ArmoryCategoryDescriptions={"Long-range precision · scoped rifles","Automatic fire · mid-range combat","Fast handling · close-range defense","Backup weapons · close-range emergencies"};
        static int GunCategory(int weapon)=>weapon<5?0:weapon==5?1:weapon==6?2:3;
        public void ViewAvailableWeapon(int weapon)
        {if(!WeaponCatalog.Valid(weapon))return;SwitchTab(LobbyTab.Armory);OpenArmoryWeapon(weapon);}
        void OpenArmoryWeapon(int weapon)
        {
            armoryRifle=Mathf.Clamp(weapon,0,WeaponCatalog.Count-1);
            armoryCategory=GunCategory(armoryRifle);
            armoryUpgrades=false;armoryGunScroll=Vector2.zero;
        }
        Texture2D ArmoryGunImage(int weapon)
            => Resources.Load<Texture2D>("Textures/"+WeaponCatalog.Get(weapon).Model+"Showcase") ?? Resources.Load<Texture2D>("Textures/"+WeaponCatalog.Get(weapon).Model+(weapon==0?"ModelCard":"Card")) ?? (weapon==0?barrettCardTex:weapon==1?m24CardTex:weapon==2?mk12CardTex:null);
        void ArmoryImage(Rect rect,int weapon)
        {
            var image=ArmoryGunImage(weapon);
            if(image!=null)
            {
                // Studio renders are already framed; legacy cards retain their existing crop.
                float cropHeight=weapon==7?.84f:weapon>=5?.68f:.42f;
                Rect uv=image.name.EndsWith("Showcase")?new Rect(0,0,1,1):new Rect(.07f,(1-cropHeight)/2,.86f,cropHeight);
                float aspect=image.width*uv.width/(image.height*uv.height);
                float width=Mathf.Min(rect.width,rect.height*aspect),height=width/aspect;
                GUI.DrawTextureWithTexCoords(new Rect(rect.center.x-width/2,rect.center.y-height/2,width,height),image,uv);
            }
            else EconomyText(rect,WeaponCatalog.Get(weapon).Name,18,CommandGUI.Muted,TextAnchor.MiddleCenter);
        }
        void SelectArmoryCategory(int category)
        {
            int weapon=WeaponCatalog.SelectedOwnedWeapon;
            if(GunCategory(weapon)!=category)
                for(int i=0;i<WeaponCatalog.Count;i++) if(GunCategory(i)==category){weapon=i;break;}
            OpenArmoryWeapon(weapon);
        }
        void DrawCategorizedArmory(float w,float h)
        {
            EnsureLobbyTextures();WeaponCatalog.EnsureStarterLoadout();
            float margin=20;
            EconomyText(new Rect(margin,8,w-360,32),armoryCategory<0?"ARMORY / CHOOSE A WEAPON TYPE":"ARMORY / "+ArmoryCategories[armoryCategory],20,Color.white);
            EconomyText(new Rect(w-340,8,320,32),$"RANK {PlayerProgression.Level}   ·   {PlayerPrefs.GetInt("GeoSniper.Gold",0):N0} GOLD",14,CommandGUI.AccentGold,TextAnchor.MiddleRight);
            if(armoryCategory<0)
            {
                EconomyText(new Rect(margin,44,w-40,26),"Choose a category to browse, equip and upgrade your guns.",14,CommandGUI.Muted);
                float gap=14,cardWidth=(w-2*margin-gap)/2,cardHeight=(h-126-gap)/2;
                for(int category=0;category<4;category++)
                {
                    Rect card=new Rect(margin+(category%2)*(cardWidth+gap),82+(category/2)*(cardHeight+gap),cardWidth,cardHeight);
                    CommandGUI.Fill(card,CommandGUI.ThemeCard);
                    int representative=category==0?1:category==1?5:category==2?6:7;
                    ArmoryImage(new Rect(card.x+12,card.y+8,card.width-24,Mathf.Max(50,card.height-86)),representative);
                    EconomyText(new Rect(card.x+16,card.yMax-76,card.width-32,28),ArmoryCategories[category]+"  →",20,Color.white);
                    EconomyText(new Rect(card.x+16,card.yMax-48,card.width-32,22),ArmoryCategoryDescriptions[category],12,CommandGUI.Muted);
                    int count=0,owned=0;for(int i=0;i<WeaponCatalog.Count;i++)if(GunCategory(i)==category){count++;if(WeaponCatalog.Owned(i))owned++;}
                    EconomyText(new Rect(card.x+16,card.yMax-26,card.width-32,22),$"{count} GUN{(count==1?"":"S")}   ·   {owned} OWNED",11,CommandGUI.AccentGold);
                    if(CommandGUI.IsClicked(card))SelectArmoryCategory(category);
                }
                return;
            }
            if(CommandGUI.DrawButton(new Rect(margin,48,150,34),"‹ CATEGORIES",false,12)){armoryCategory=-1;armoryUpgrades=false;return;}
            float tabWidth=(w-202)/4;
            for(int category=0;category<4;category++)
                if(CommandGUI.DrawButton(new Rect(182+category*(tabWidth+4),48,tabWidth,34),ArmoryCategories[category],category==armoryCategory,12))SelectArmoryCategory(category);

            float stripY=h-144,sidebarWidth=Mathf.Clamp(w*.28f,270,360);
            Rect hero=new Rect(margin,96,w-sidebarWidth-3*margin,stripY-108);
            Rect stats=new Rect(hero.xMax+margin,hero.y,sidebarWidth,hero.height);
            CommandGUI.Fill(hero,new Color(.045f,.06f,.075f));
            var gun=WeaponCatalog.Get(armoryRifle);
            EconomyText(new Rect(hero.x+18,hero.y+12,hero.width-36,34),gun.Name,25,Color.white);
            EconomyText(new Rect(hero.x+18,hero.y+48,hero.width-36,22),gun.Class+"   ·   "+gun.Caliber,12,CommandGUI.AccentCyan);
            ArmoryImage(new Rect(hero.x+16,hero.y+76,hero.width-32,Mathf.Max(50,hero.height-140)),armoryRifle);
            EconomyText(new Rect(hero.x+18,hero.yMax-56,hero.width-36,44),WeaponCatalog.BuyingAdvice(armoryRifle),13,CommandGUI.Muted);
            DrawArmoryStats(stats);
            EconomyText(new Rect(margin,stripY-4,w-40,24),"BROWSE "+ArmoryCategories[armoryCategory],11,CommandGUI.AccentCyan);
            int gunCount=0;for(int i=0;i<WeaponCatalog.Count;i++)if(GunCategory(i)==armoryCategory)gunCount++;
            float cardW=182,cardGap=10;
            armoryGunScroll=GUI.BeginScrollView(new Rect(margin,stripY+24,w-40,112),armoryGunScroll,new Rect(0,0,Mathf.Max(w-40,gunCount*(cardW+cardGap)),92));
            int slot=0;
            for(int i=0;i<WeaponCatalog.Count;i++)
            {
                if(GunCategory(i)!=armoryCategory)continue;
                Rect card=new Rect(slot++*(cardW+cardGap),0,cardW,92);
                CommandGUI.Fill(card,i==armoryRifle?new Color(.16f,.21f,.23f):CommandGUI.ThemeCard);
                CommandGUI.Fill(new Rect(card.x,card.y,card.width,2),i==armoryRifle?CommandGUI.AccentGold:CommandGUI.ThemeBorder);
                ArmoryImage(new Rect(card.x+6,card.y+4,card.width-12,48),i);
                EconomyText(new Rect(card.x+8,card.y+50,card.width-16,20),WeaponCatalog.Get(i).Name,12,Color.white);
                string status=WeaponCatalog.SelectedOwnedWeapon==i?"EQUIPPED":WeaponCatalog.Owned(i)?"OWNED":$"RANK {WeaponCatalog.RequiredRank(i)} · {WeaponCatalog.Get(i).UnlockCost:N0} "+(WeaponCatalog.Get(i).Gold?"GOLD":"CR");
                EconomyText(new Rect(card.x+8,card.y+70,card.width-16,18),status,10,CommandGUI.AccentGold);
                if(CommandGUI.IsClicked(card)){armoryRifle=i;armoryUpgrades=false;armoryUpgradeScroll=Vector2.zero;}
            }
            GUI.EndScrollView();
        }
        void DrawArmoryStats(Rect panel)
        {
            CommandGUI.Fill(panel,CommandGUI.ThemeCard);
            bool owned=WeaponCatalog.Owned(armoryRifle);
            if(CommandGUI.DrawButton(new Rect(panel.x+12,panel.y+10,panel.width-24,32),armoryUpgrades?"‹ WEAPON STATS":"UPGRADES",armoryUpgrades,13))armoryUpgrades=!armoryUpgrades;
            Rect list=new Rect(panel.x+12,panel.y+50,panel.width-24,Mathf.Max(80,panel.height-130));
            armoryUpgradeScroll=GUI.BeginScrollView(list,armoryUpgradeScroll,new Rect(0,0,list.width-18,armoryUpgrades?570:216));
            for(int n=0;n<5;n++)
            {
                var upgrade=(WeaponUpgrade)n;
                int tier=WeaponProgression.Tier(armoryRifle,upgrade);
                float rowY=n*(armoryUpgrades?114:36),rowWidth=list.width-22;
                if(armoryUpgrades){CommandGUI.Fill(new Rect(0,rowY,rowWidth,106),new Color(.055f,.075f,.095f));CommandGUI.Fill(new Rect(0,rowY,3,106),CommandGUI.AccentCyan);}
                EconomyText(new Rect(armoryUpgrades?10:0,rowY+ (armoryUpgrades?6:0),rowWidth-10,armoryUpgrades?18:15),upgrade.ToString().ToUpperInvariant()+$"   {tier}/{WeaponProgression.MaxTier}",11,CommandGUI.Muted);
                EconomyText(new Rect(armoryUpgrades?10:0,rowY+(armoryUpgrades?25:15),rowWidth-20,armoryUpgrades?22:18),UpgradeReading(armoryRifle,upgrade,tier),13,Color.white);
                float barY=rowY+(armoryUpgrades?48:34);
                CommandGUI.Fill(new Rect(0,barY,rowWidth,2),CommandGUI.ThemeBorder);
                CommandGUI.Fill(new Rect(0,barY,rowWidth*(tier+1)/(WeaponProgression.MaxTier+1),2),CommandGUI.AccentGreen);
                if(!armoryUpgrades)continue;
                bool enabled=GUI.enabled;GUI.enabled=enabled&&owned&&tier<WeaponProgression.MaxTier;
                int cost=WeaponProgression.Cost(upgrade,tier);
                bool maxed=tier>=WeaponProgression.MaxTier;
                if(!maxed)
                {
                    EconomyText(new Rect(10,rowY+53,rowWidth-20,18),"NEXT: "+UpgradeReading(armoryRifle,upgrade,tier+1),11,CommandGUI.AccentCyan);
                    EconomyText(new Rect(10,rowY+77,rowWidth-112,22),$"{cost:N0} "+(WeaponProgression.UsesGold(upgrade)?"GOLD":"CR"),12,CommandGUI.AccentGold);
                }
                if(GameButtonChrome.Draw(new Rect(rowWidth-100,rowY+72,90,30),maxed?"MAX":"BUY",GameButtonTone.Amber,13))
                {
                    bool bought=WeaponProgression.TryUpgrade(armoryRifle,upgrade);
                    weaponEconomyNotice=bought?"Upgrade purchased. Applies on your next deployment.":"Insufficient currency.";
                    if(!bought)OfferCurrencyAd(WeaponProgression.UsesGold(upgrade),cost);
                }
                GUI.enabled=enabled;
            }
            if(!armoryUpgrades)EconomyText(new Rect(0,184,list.width-22,30),$"EFFECTIVE RANGE   {WeaponCatalog.Get(armoryRifle).Range:0} m",12,CommandGUI.AccentCyan);
            GUI.EndScrollView();
            Rect action=new Rect(panel.x+12,panel.yMax-74,panel.width-24,36);
            var gun=WeaponCatalog.Get(armoryRifle);
            if(owned)
            {
                bool selected=WeaponCatalog.SelectedOwnedWeapon==armoryRifle;
                if(CommandGUI.DrawButton(action,selected?"EQUIPPED":"EQUIP",true,16))
                {PlayerPrefs.SetInt("GeoSniper.SelectedWeapon",armoryRifle);PlayerPrefs.Save();}
            }
            else
            {
                bool enabled=GUI.enabled;GUI.enabled=enabled&&WeaponCatalog.RankEligible(armoryRifle);
                if(CommandGUI.DrawButton(action,WeaponCatalog.RankEligible(armoryRifle)?$"BUY · {gun.UnlockCost:N0} "+(gun.Gold?"GOLD":"CR"):"REQUIRES RANK "+WeaponCatalog.RequiredRank(armoryRifle),true,13))
                {
                    bool bought=WeaponCatalog.TryUnlock(armoryRifle);
                    weaponEconomyNotice=bought?"Weapon unlocked. Tap EQUIP to deploy.":"Insufficient currency.";
                    if(!bought)OfferCurrencyAd(gun.Gold,gun.UnlockCost);
                }
                GUI.enabled=enabled;
            }
            EconomyText(new Rect(panel.x+12,panel.yMax-34,panel.width-24,30),weaponEconomyNotice.Length>0?weaponEconomyNotice:"Campaign upgrades apply. 1v1 uses base stats.",10,CommandGUI.Muted);
        }
    }
}

