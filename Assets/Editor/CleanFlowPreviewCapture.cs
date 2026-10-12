using System;
using System.IO;
using System.Reflection;
using GeoSniper;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
[InitializeOnLoad]
public static class CleanFlowPreviewCapture
{
    const string Key="GeoSniper.CleanFlowPreview";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static EditorWindow view;
    static int page;
    static double readyAt;
    static DateTime started,captured;
    static bool requested,prepared;
    static readonly string[] Pages={"Home","Mission","Location"};
    static CleanFlowPreviewCapture(){EditorApplication.update+=Tick;}
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/.utmp/review-unity/"))throw new Exception("Use the isolated review project");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        view.position=SessionState.GetBool(Key+".Compact",false)?new Rect(0,0,800,620):new Rect(0,0,1280,740);view.Show();
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    public static void RunCompact(){SessionState.SetBool(Key+".Compact",true);Run();}
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
            typeof(GeoSniperGame).GetField("selectedNodeId",Private).SetValue(game,0);
            game.SwitchTab(page==0?GeoSniperGame.LobbyTab.Home:page==1?GeoSniperGame.LobbyTab.Campaign:GeoSniperGame.LobbyTab.Location);
            readyAt=EditorApplication.timeSinceStartup+3;prepared=true;
        }
        if(EditorApplication.timeSinceStartup<readyAt)return;
        string path=Path.GetFullPath("Logs/Clean"+Pages[page]+(SessionState.GetBool(Key+".Compact",false)?"Compact":"")+".png");Directory.CreateDirectory("Logs");
        if(!requested){captured=DateTime.UtcNow;requested=true;ScreenCapture.CaptureScreenshot(path);}
        if(File.Exists(path) && File.GetLastWriteTimeUtc(path)>=captured)
        {
            if(++page<Pages.Length){requested=false;prepared=false;return;}
            SessionState.SetBool(Key,false);EditorApplication.Exit(0);
        }
    }
}
