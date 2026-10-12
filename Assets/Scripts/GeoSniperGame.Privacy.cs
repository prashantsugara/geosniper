using System;
using System.IO;
using UnityEngine;

namespace GeoSniper
{
    public sealed partial class GeoSniperGame
    {
        bool showingPrivacy,showingLocationConsent,confirmClearLocations;
        bool showingAssetCredits;
        bool showingCivilianCredits;
        bool showingStrongholdCredits;
        bool showingLevelCredits;
        string privacyNotice="";
        void DrawAnalyticsConsentPanel()
        {
            Rect safe=Screen.safeArea;
            if(safe.width<=0 || safe.height<=0) safe=new Rect(0,0,Screen.width,Screen.height);
            float scale=Mathf.Max(.01f,Mathf.Min(safe.width/900f,safe.height/620f));
            float w=safe.width/scale,h=safe.height/scale;
            GUI.matrix=Matrix4x4.TRS(new Vector3(safe.x,Screen.height-safe.yMax,0),Quaternion.identity,new Vector3(scale,scale,1));
            CommandGUI.Fill(new Rect(0,0,w,h),CommandGUI.ThemeBg);
            Rect panel=new Rect((w-760)/2,(h-350)/2,760,350);
            CommandGUI.Fill(panel,CommandGUI.ThemeCard);
            var title=new GUIStyle(GUI.skin.label){fontSize=25,fontStyle=FontStyle.Bold,normal={textColor=CommandGUI.Text}};
            var body=new GUIStyle(GUI.skin.label){fontSize=17,wordWrap=true,normal={textColor=CommandGUI.Text}};
            GUI.Label(new Rect(panel.x+25,panel.y+20,710,40),"HELP IMPROVE GEO SNIPER",title);
            GUI.Label(new Rect(panel.x+25,panel.y+72,710,160),
                "Allow optional game analytics? Google Firebase and Unity Analytics can receive first opens, play sessions, mission results, upgrades and ad reward outcomes. This helps us understand retention and fix problems. You can turn analytics off later in Privacy & Support. Playing without it is fine.",body);
            if(CommandGUI.DrawButton(new Rect(panel.x+25,panel.y+245,220,48),"PRIVACY POLICY",false,13))
            {
                string url=ReleaseConfiguration.Current.privacyPolicyUrl;
                if(ReleaseConfiguration.ValidHttps(url)) Application.OpenURL(url);
            }
            if(CommandGUI.DrawButton(new Rect(panel.x+264,panel.y+245,220,48),"CONTINUE WITHOUT",false,13)) GameAnalyticsManager.SetAnalyticsConsent(false);
            if(CommandGUI.DrawButton(new Rect(panel.x+503,panel.y+245,232,48),"ALLOW ANALYTICS",true,13)) GameAnalyticsManager.SetAnalyticsConsent(true);
        }
        void DrawPrivacyPanel()
        {
            Rect safe=Screen.safeArea;
            if(safe.width<=0 || safe.height<=0) safe=new Rect(0,0,Screen.width,Screen.height);
            float scale=Mathf.Max(.01f,Mathf.Min(safe.width/900f,safe.height/620f));
            float w=safe.width/scale,h=safe.height/scale;
            GUI.matrix=Matrix4x4.TRS(new Vector3(safe.x,Screen.height-safe.yMax,0),Quaternion.identity,new Vector3(scale,scale,1));
            CommandGUI.Fill(new Rect(0,0,w,h),CommandGUI.ThemeBg);
            float x=(w-800)/2,y=(h-530)/2;
            CommandGUI.Fill(new Rect(x,y,800,530),CommandGUI.ThemeCard);
            var title=new GUIStyle(GUI.skin.label){fontSize=25,fontStyle=FontStyle.Bold,normal={textColor=CommandGUI.Text}};
            var body=new GUIStyle(GUI.skin.label){fontSize=17,wordWrap=true,normal={textColor=CommandGUI.Text}};
            if (showingAssetCredits && !showingLocationConsent)
            {
                if(showingLevelCredits)
                {
                    GUI.Label(new Rect(x+28,y+25,744,42),"LEVEL LOCATION ASSET CREDITS",title);
                    GUI.Label(new Rect(x+28,y+85,744,215),"Military Tent by Reinaw13.\nOld Apartment Building by jimbogies.\nPack - Realistic - 3 Warehouse by Islide.\nAbandoned Shopping Mall by Miguel Adão (shelves and fridge).\n\nSketchfab / Creative Commons Attribution 4.0.\nModels grounded and scaled. Warehouse centre building extracted; door/window inserts removed. Apartment ground and glass omitted. Maps resized to 1024px and packed for Unity. Walkable galleries and stairs authored for gameplay. Existing licensed CGTrader street props retain their source PBR materials and LODs.",new GUIStyle(body){fontSize=15});
                    string[] sources={"military-tent-d95296298b2a4caca8a5c3ce4450bb15","old-apartment-building-a2a27bec2e364496b34ae399ee93cb67","pack-realistic-3-warehouse-320c272513a94c0482dbb3c4e79d61f5","abandoned-shopping-mall-5ede6f812941428dae4b31a3487623fd"};
                    string[] labels={"MILITARY TENT","BRICK APARTMENTS","WAREHOUSE","MALL INTERIOR"};
                    for(int i=0;i<4;i++)if(CommandGUI.DrawButton(new Rect(x+28+(i%2)*378,y+320+(i/2)*54,366,44),labels[i],false,12))Application.OpenURL("https://sketchfab.com/3d-models/"+sources[i]);
                    if(CommandGUI.DrawButton(new Rect(x+28,y+432,744,36),"CC BY 4.0 LICENSE",false,14))Application.OpenURL("https://creativecommons.org/licenses/by/4.0/");
                    if(CommandGUI.DrawButton(new Rect(x+28,y+477,744,36),"BACK TO ASSET CREDITS",true,14))showingLevelCredits=false;
                    return;
                }
                if(showingStrongholdCredits)
                {
                    GUI.Label(new Rect(x+28,y+25,744,42),"STRONGHOLD ASSET CREDITS",title);
                    GUI.Label(new Rect(x+28,y+85,744,220),"Low Poly Afghanistan House Obj by omar.takwa0865.\nDesert house 01 FREE by filthycent (plaster, stone and timber textures).\nSandbag [Low Poly Realist] by Islide.\nArab Building by Usmanix3D (review candidate; wing no longer used).\n\nSketchfab / Creative Commons Attribution 4.0.\nAfghan shell grounded/scaled; window inserts removed and physical UVs added. Original open-gallery extension, stairs, arches, rails and roof cover. Licensed surface maps resized to 1024px and channels packed for Unity. Source stairs and sandbag collision retained.",new GUIStyle(body){fontSize=15});
                    if(CommandGUI.DrawButton(new Rect(x+28,y+328,240,44),"AFGHAN HOUSE",false,12))Application.OpenURL("https://sketchfab.com/3d-models/low-poly-afghanistan-house-obj-9f97af2ec2fd4379a65c0683b65d08c3");
                    if(CommandGUI.DrawButton(new Rect(x+280,y+328,240,44),"PLASTER SOURCE",false,12))Application.OpenURL("https://sketchfab.com/3d-models/desert-house-01-free-2f7b88bbcf1b454cad2c3e9dbc9dd18e");
                    if(CommandGUI.DrawButton(new Rect(x+532,y+328,240,44),"SANDBAG SOURCE",false,12))Application.OpenURL("https://sketchfab.com/3d-models/sandbag-low-poly-realist-7d52600a15c747749d845d9f906045cf");
                    if(CommandGUI.DrawButton(new Rect(x+28,y+390,744,44),"CC BY 4.0 LICENSE",false,14))Application.OpenURL("https://creativecommons.org/licenses/by/4.0/");
                    if(CommandGUI.DrawButton(new Rect(x+28,y+438,744,30),"ARAB BUILDING SOURCE",false,12))Application.OpenURL("https://sketchfab.com/3d-models/arab-building-a00d262125c245058f8ddd0e887bb833");
                    if(CommandGUI.DrawButton(new Rect(x+28,y+477,365,36),"LEVEL LOCATION CREDITS",false,14))showingLevelCredits=true;
                    if(CommandGUI.DrawButton(new Rect(x+407,y+477,365,36),"BACK TO ASSET CREDITS",true,14))showingStrongholdCredits=false;
                    return;
                }
                if(showingCivilianCredits)
                {
                    GUI.Label(new Rect(x+28,y+25,744,42),"CIVILIAN ASSET CREDITS",title);
                    GUI.Label(new Rect(x+28,y+85,744,220),"Carla Rigged 001 - Rigged 3D Business Women by Renderpeople.\nEric Rigged 001 - Rigged 3D Business Man by Renderpeople.\nDoctor by Nasser.\n\nDownloaded from Sketchfab under Creative Commons Attribution 4.0.\nConverted to FBX; textures resized to 1024px; rig aliases and poses adapted for gameplay. Reporter camera, badges and shopkeeper apron added for fictional roles.",body);
                    if(CommandGUI.DrawButton(new Rect(x+28,y+328,240,44),"CARLA SOURCE",false,12))Application.OpenURL("https://sketchfab.com/3d-models/carla-rigged-001-rigged-3d-business-women-acf520f450d14dd799f98a6fede3edf5");
                    if(CommandGUI.DrawButton(new Rect(x+280,y+328,240,44),"ERIC SOURCE",false,12))Application.OpenURL("https://sketchfab.com/3d-models/eric-rigged-001-rigged-3d-business-man-a46bc9f67aaa415bb4f3241eef900e7f");
                    if(CommandGUI.DrawButton(new Rect(x+532,y+328,240,44),"DOCTOR SOURCE",false,12))Application.OpenURL("https://sketchfab.com/3d-models/doctor-31c09ab8b098489b8c46712650e45bac");
                    if(CommandGUI.DrawButton(new Rect(x+28,y+390,744,44),"CC BY 4.0 LICENSE",false,14))Application.OpenURL("https://creativecommons.org/licenses/by/4.0/");
                    if(CommandGUI.DrawButton(new Rect(x+28,y+477,744,36),"BACK TO ASSET CREDITS",true,14))showingCivilianCredits=false;
                    return;
                }
                GUI.Label(new Rect(x+28,y+25,744,42),"ASSET CREDITS",title);
                GUI.Label(new Rect(x+28,y+78,744,218),"Concrete Barrier by YadroGames (Sketchfab, CC BY 4.0).\nStreet Props by syedraza (Sketchfab, CC BY 4.0): separated, grounded and scaled.\nCGTrader street props by ribrado (Royalty Free, no AI).\nHouse interior by Katydid (Sketchfab, CC BY 4.0); grounded and scaled.\nHouse floor plan by maxm77353 (CGTrader Royalty Free, no AI); grounded and scaled.\nVegetation: styropyro (trees), Coynese (bushes), connorwilding (grass). CGTrader Royalty Free, no AI.\nCGTrader guns: Desert Eagle by AmanDeep0707; M4A1 by mminhnghii02; MP5 by Fallxhore; M40 and SVD by Yahia-DdZ. Royalty Free License (no AI).\nModels optimized and fitted for gameplay. Textures adapted; M40 surfaces also used on the SVD.",new GUIStyle(body){fontSize=14});
                if(CommandGUI.DrawButton(new Rect(x+28,y+303,240,44),"DESERT EAGLE",false,12)) Application.OpenURL("https://www.cgtrader.com/free-3d-models/military/gun/desert-eagle-gun-model-4k");
                if(CommandGUI.DrawButton(new Rect(x+280,y+303,240,44),"M4A1",false,12)) Application.OpenURL("https://www.cgtrader.com/free-3d-models/military/gun/m4a1-rifle-fb11d84c-7353-4763-96a4-b1acd1c03513");
                if(CommandGUI.DrawButton(new Rect(x+532,y+303,240,44),"MP5",false,12)) Application.OpenURL("https://www.cgtrader.com/free-3d-models/military/gun/game-ready-mp5-submachine-gun-f50b9091-bb8e-41b1-bf34-e7bab69fb57c");
                if(CommandGUI.DrawButton(new Rect(x+28,y+363,240,44),"M40",false,12)) Application.OpenURL("https://www.cgtrader.com/free-3d-models/military/gun/m40-sniper");
                if(CommandGUI.DrawButton(new Rect(x+280,y+363,240,44),"SVD",false,12)) Application.OpenURL("https://www.cgtrader.com/free-3d-models/military/gun/dragunov-svd-sniper-rifle-7ec65bd7-6d3c-4039-b7a4-f5b0315cffe6");
                if(CommandGUI.DrawButton(new Rect(x+532,y+363,240,44),"GUN LICENSE",false,12)) Application.OpenURL("https://help.cgtrader.com/hc/en-us/articles/360015124437-Royalty-Free-License");
                if(CommandGUI.DrawButton(new Rect(x+28,y+415,240,35),"CONCRETE BARRIER",false,12)) Application.OpenURL("https://sketchfab.com/3d-models/concrete-barrier-afaab6285c484c36aab250e06727d471");
                if(CommandGUI.DrawButton(new Rect(x+280,y+415,240,35),"STREET PROPS",false,12)) Application.OpenURL("https://sketchfab.com/3d-models/street-props-741e544242a9481fa54c1f7bca6e92b2");
                if(CommandGUI.DrawButton(new Rect(x+532,y+415,240,35),"CC BY 4.0 LICENSE",false,12)) Application.OpenURL("https://creativecommons.org/licenses/by/4.0/");
                if(CommandGUI.DrawButton(new Rect(x+28,y+457,365,30),"HOUSE INTERIOR SOURCE",false,12)) Application.OpenURL("https://sketchfab.com/3d-models/house-interior-e910b8395ddc4b9886d87caffdbacc69");
                if(CommandGUI.DrawButton(new Rect(x+407,y+457,365,30),"HOUSE FLOOR PLAN SOURCE",false,12)) Application.OpenURL("https://www.cgtrader.com/free-3d-models/interior/living-room/free-low-poly-house-floor-plan-maya-ma-and-mb-and-fbx-format");
                if(CommandGUI.DrawButton(new Rect(x+28,y+495,240,28),"CIVILIAN CREDITS",false,14)) showingCivilianCredits=true;
                if(CommandGUI.DrawButton(new Rect(x+280,y+495,240,28),"STRONGHOLD CREDITS",false,14)) showingStrongholdCredits=true;
                if(CommandGUI.DrawButton(new Rect(x+532,y+495,240,28),"BACK",true,14)) showingAssetCredits=false;
                return;
            }
            GUI.Label(new Rect(x+28,y+25,744,42),showingLocationConsent?"SWITCH TO MY LIVE LOCATION":"PRIVACY & SUPPORT",title);
            string info=showingLocationConsent
                ? "This replaces your previously selected place with this phone's current GPS location.\n\n1. Turn on Location and internet on your phone.\n2. Allow location while using the app; enable Precise location if asked.\n3. Wait while we locate you and load your nearby map.\n\nMap providers receive the requested area and your IP address. Coordinates and map tiles are cached on this device. No background location is requested. You can choose a place instead."
                : "Progress and settings are stored on your device. World Map sends searches to Photon and the selected map area to map providers; coordinates and map tiles are cached locally.\n\nAds are provided by Google AdMob. Ad privacy choices are in Settings. Optional game analytics sends play sessions and gameplay events to Google Firebase and Unity Analytics only if you allow it below.\n\nFor data requests, contact "+ReleaseConfiguration.Current.supportEmail+". Map credits: OpenStreetMap contributors (ODbL), Overture Maps and the applicable source datasets.";
            GUI.Label(new Rect(x+28,y+85,744,showingLocationConsent?270:218),info,body);
            if(showingLocationConsent)
            {
                if(CommandGUI.DrawButton(new Rect(x+28,y+440,350,52),"CHOOSE A PLACE",false,15))
                {
                    showingLocationConsent=false;
                    // Keep the chosen mission when searching instead of granting GPS access.
                    SwitchTab(LobbyTab.Location);
                }
                if(CommandGUI.DrawButton(new Rect(x+422,y+440,350,52),"LOCATE ME & LOAD MY AREA",true,15))
                {
                    showingLocationConsent=false;
                    activeLocation=null;
                    if (duelLocationSelection)
                    {
                        resumeMissionAfterLocationConsent=false;
                        DeployPvPDuel(true, useLiveLocation: true);
                        return;
                    }
                    // Preserve the selected campaign/duel mode when this prompt came
                    // from Deploy. A direct World Map request stays a free-roam sector.
                    if(!resumeMissionAfterLocationConsent)
                    {
                        campaignNodeToStart=-1;
                        stageIndexToStart=-1;
                        isPvPDuel=false;
                    }
                    resumeMissionAfterLocationConsent=false;
                    StartCoroutine(Play(null,true));
                }
            }
            else
            {
                GUI.Label(new Rect(x+28,y+309,480,42),"OPTIONAL ANALYTICS: "+(GameAnalyticsManager.ConsentGranted?"ON":"OFF")+"  •  You can change this anytime.",new GUIStyle(body){fontSize=14});
                if(CommandGUI.DrawButton(new Rect(x+520,y+309,252,42),GameAnalyticsManager.ConsentGranted?"TURN ANALYTICS OFF":"ALLOW ANALYTICS",GameAnalyticsManager.ConsentGranted,12))
                {
                    GameAnalyticsManager.SetAnalyticsConsent(!GameAnalyticsManager.ConsentGranted);
                    privacyNotice=GameAnalyticsManager.ConsentGranted?"Analytics enabled. Game events can be sent to Google and Unity.":"Analytics disabled. No new analytics events will be sent.";
                }
                if(CommandGUI.DrawButton(new Rect(x+28,y+363,235,44),"PRIVACY POLICY",false,13))
                {
                    string url=ReleaseConfiguration.Current.privacyPolicyUrl;
                    if(ReleaseConfiguration.ValidHttps(url)) Application.OpenURL(url);
                    else privacyNotice="The public privacy policy has not been configured for this test build.";
                }
                if(CommandGUI.DrawButton(new Rect(x+282,y+363,235,44),"EMAIL SUPPORT",false,13))
                    Application.OpenURL("mailto:"+Uri.EscapeDataString(ReleaseConfiguration.Current.supportEmail));
                if(CommandGUI.DrawButton(new Rect(x+537,y+363,235,44),confirmClearLocations?"CONFIRM CLEAR":"CLEAR SAVED MAPS",false,13))
                {
                    if(!confirmClearLocations) {confirmClearLocations=true;privacyNotice="This removes saved map downloads and recent locations. Game progress is kept.";}
                    else ClearSavedLocations();
                }
                GUI.Label(new Rect(x+28,y+410,744,40),privacyNotice,GUIStyleCache.Get(12,FontStyle.Normal,TextAnchor.UpperLeft,CommandGUI.Muted,true));
                if(CommandGUI.DrawButton(new Rect(x+404,y+457,368,44),"ASSET CREDITS",false,14)) showingAssetCredits=true;
                if(CommandGUI.DrawButton(new Rect(x+28,y+457,360,44),"BACK TO SETTINGS",true,14))
                {showingPrivacy=false;confirmClearLocations=false;privacyNotice="";}
            }
        }
        void ClearSavedLocations()
        {
            StartCoroutine(ClearSavedLocationsRoutine());
        }

        System.Collections.IEnumerator ClearSavedLocationsRoutine()
        {
            Input.location.Stop(); searching = false;
            privacyNotice = "Clearing saved map downloads...";
            confirmClearLocations = false;

            string persistentPath = Application.persistentDataPath;
            bool success = true;

            var task = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    string root = Path.GetFullPath(persistentPath) + Path.DirectorySeparatorChar;
                    foreach (string name in new[] { "osm-sectors-v2", "overture-direct-sectors-v1", "overture-sectors-v1", "overture-public-tiles-v1", "elevation-tiles" })
                    {
                        string path = Path.GetFullPath(Path.Combine(root, name));
                        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                        if (!Directory.Exists(path)) continue;
                        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) continue;
                        foreach (var file in Directory.GetFiles(path))
                        {
                            try
                            {
                                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0) File.Delete(file);
                            }
                            catch { }
                        }
                    }
                    string legacy = Path.Combine(root, "last-osm-sector.json");
                    if (File.Exists(legacy) && (File.GetAttributes(legacy) & FileAttributes.ReparsePoint) == 0)
                    {
                        try { File.Delete(legacy); } catch { }
                    }
                }
                catch
                {
                    success = false;
                }
            });

            while (!task.IsCompleted)
            {
                yield return null;
            }

            foreach (string key in new[] { "GeoSniper.SavedLat", "GeoSniper.SavedLon", "GeoSniper.SavedLabel", "GeoSniper.RecentLocations", "GeoSniper.LocationSource" })
                PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            activeLocation = null;
            locations?.Clear();
            locationSearch = "";

            privacyNotice = success 
                ? "Saved maps and recent locations cleared. Progress is unchanged." 
                : "Some map files could not be removed. Please try again.";
        }
    }
}
