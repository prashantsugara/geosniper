using System;
using System.IO;
using System.Reflection;
using GeoSniper;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
public static class StoryPreviewCapture
{
    const string Key="GeoSniper.StoryPreview";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static EditorWindow view;
    static int page;
    static double readyAt;
    static DateTime started,captured;
    static bool requested,prepared;
    static StoryPreviewCapture(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/.utmp/review-unity/"))throw new Exception("Use the isolated review project");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));view.position=new Rect(0,0,1280,740);view.Show();
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false))return;
        if(started==default)started=DateTime.UtcNow;
        if((DateTime.UtcNow-started).TotalSeconds>90){SessionState.SetBool(Key,false);EditorApplication.Exit(1);return;}
        if(!EditorApplication.isPlaying || EditorApplication.isCompiling)return;
        if(view==null)view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        view.Repaint();EditorApplication.QueuePlayerLoopUpdate();
        var game=UnityEngine.Object.FindAnyObjectByType<GeoSniperGame>();if(game==null)return;
        if(!prepared)
        {
            typeof(GeoSniperGame).GetField("showingPrivacy",Private).SetValue(game,false);
            typeof(GeoSniperGame).GetField("showingLocationConsent",Private).SetValue(game,false);
            game.SwitchTab(GeoSniperGame.LobbyTab.Campaign);
            typeof(GeoSniperGame).GetField("selectedNodeId",Private).SetValue(game,44);
            typeof(GeoSniperGame).GetField("showingCampaignStory",Private).SetValue(game,page==0);
            if(page==1)
            {
                var mission=new GameObject("Story aftermath preview").AddComponent<UrbanCombatMission>();
                typeof(UrbanCombatMission).GetField("campaignNode",Private).SetValue(mission,CampaignNodeGraph.GetNode(49));
                mission.currentMissionState=MissionState.Complete;
            }
            readyAt=EditorApplication.timeSinceStartup+3;prepared=true;
        }
        if(EditorApplication.timeSinceStartup<readyAt)return;
        string path=Path.GetFullPath(page==0?"Logs/StoryBriefing.png":"Logs/StoryEnding.png");Directory.CreateDirectory("Logs");
        if(!requested){captured=DateTime.UtcNow;requested=true;ScreenCapture.CaptureScreenshot(path);}
        if(File.Exists(path) && File.GetLastWriteTimeUtc(path)>=captured)
        {
            if(page++==0){requested=false;prepared=false;return;}
            SessionState.SetBool(Key,false);EditorApplication.Exit(0);
        }
    }
}
