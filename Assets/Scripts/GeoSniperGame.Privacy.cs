using System;
using System.IO;
using UnityEngine;

namespace GeoSniper
{
    public sealed partial class GeoSniperGame
    {
        bool showingPrivacy,showingLocationConsent,confirmClearLocations;
        bool showingAssetCredits;
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
                GUI.Label(new Rect(x+28,y+25,744,42),"ASSET CREDITS",title);
                GUI.Label(new Rect(x+28,y+90,744,230),"Concrete Barrier by YadroGames (Sketchfab)\nCreative Commons Attribution 4.0 International (CC BY 4.0).\n\nOrange and yellow variants adapted for Geo / Sniper: dimensions and orientation adjusted, textures reduced to 1024 pixels.\n\nSource and license are available below.",body);
                if(CommandGUI.DrawButton(new Rect(x+28,y+363,360,44),"VIEW ORIGINAL ASSET",false,13)) Application.OpenURL("https://sketchfab.com/3d-models/concrete-barrier-afaab6285c484c36aab250e06727d471");
                if(CommandGUI.DrawButton(new Rect(x+404,y+363,368,44),"VIEW CC BY 4.0 LICENSE",false,13)) Application.OpenURL("https://creativecommons.org/licenses/by/4.0/");
                if(CommandGUI.DrawButton(new Rect(x+28,y+457,744,44),"BACK",true,14)) showingAssetCredits=false;
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
                    resumeMissionAfterLocationConsent=false;
                    SwitchTab(LobbyTab.Location);
                }
                if(CommandGUI.DrawButton(new Rect(x+422,y+440,350,52),"LOCATE ME & LOAD MY AREA",true,15))
                {
                    showingLocationConsent=false;
                    activeLocation=null;
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
