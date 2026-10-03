using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run only in the isolated review project; never changes the user's open scene.
[InitializeOnLoad]
public static class LobbyPreviewCapture
{
    const string Key = "GeoSniper.CaptureLobby";
    static double started;
    static bool requested;
    static EditorWindow gameView;
    static DateTime captureStarted;
    static int page;
    static double pageReady;
    static bool pageSet;
    static readonly string[] Pages = { "Home", "Campaign", "Armory", "Location", "Rewards", "Settings", "Loading", "Modes" };
    static string CapturePath => Path.GetFullPath(SessionState.GetBool(Key + ".All", false)
        ? "Logs/Page-" + Pages[page] + (SessionState.GetBool(Key + ".Compact", false) ? "-Compact" : "") + ".png"
        : SessionState.GetBool(Key + ".Compact", false) ? "Logs/LobbyPreviewCompact.png" : "Logs/LobbyPreview.png");
    static LobbyPreviewCapture()
    {
        if (SessionState.GetBool(Key, false))
        { started = EditorApplication.timeSinceStartup; EditorApplication.update += Tick; }
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project for capture.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        gameView = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        gameView.position = SessionState.GetBool(Key + ".Compact", false) ? new Rect(0, 0, 800, 620) : new Rect(0, 0, 1280, 740);
        gameView.Show();
        Directory.CreateDirectory("Logs");
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    public static void RunCompact()
    {
        SessionState.SetBool(Key + ".Compact", true);
        Run();
    }

    public static void RunAll() { SessionState.SetBool(Key + ".All", true); Run(); }
    public static void RunAllCompact() { SessionState.SetBool(Key + ".Compact", true); RunAll(); }

    static void Tick()
    {
        double elapsed = EditorApplication.timeSinceStartup - started;
        if (gameView == null) gameView = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        gameView.Repaint();
        EditorApplication.QueuePlayerLoopUpdate();
        if (elapsed > 90)
        { SessionState.SetBool(Key, false); Debug.LogError("Lobby capture timed out"); EditorApplication.Exit(1); return; }
        if (!EditorApplication.isPlaying || elapsed < 8) return;
        // Capture the lobby itself without changing saved consent preferences.
        var lobby = UnityEngine.Object.FindAnyObjectByType<GeoSniper.GeoSniperGame>();
        if (lobby == null) return;
        var privateFields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(GeoSniper.GeoSniperGame).GetField("showingPrivacy", privateFields)?.SetValue(lobby, false);
        typeof(GeoSniper.GeoSniperGame).GetField("showingLocationConsent", privateFields)?.SetValue(lobby, false);
        if (SessionState.GetBool(Key + ".All", false) && !pageSet)
        {
            var game = UnityEngine.Object.FindAnyObjectByType<GeoSniper.GeoSniperGame>();
            if (game == null) return;
            game.SwitchTab(page < 6 ? (GeoSniper.GeoSniperGame.LobbyTab)page : GeoSniper.GeoSniperGame.LobbyTab.Home);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(GeoSniper.GeoSniperGame).GetField("loading", flags).SetValue(game, page == 6);
            typeof(GeoSniper.GeoSniperGame).GetField("loadingDeadline", flags).SetValue(game, Time.realtimeSinceStartup + 60);
            typeof(GeoSniper.GeoSniperGame).GetField("showingModeSelector", flags).SetValue(game, page == 7);
            pageReady = elapsed + 1; pageSet = true;
        }
        if (elapsed < pageReady) return;
        if (!requested)
        {
            requested = true;
            captureStarted = DateTime.UtcNow;
            ScreenCapture.CaptureScreenshot(CapturePath);
            Debug.Log("Lobby capture requested at " + Screen.width + "x" + Screen.height);
        }
        if (File.Exists(CapturePath) && File.GetLastWriteTimeUtc(CapturePath) >= captureStarted)
        {
            Debug.Log("Captured " + CapturePath);
            if (SessionState.GetBool(Key + ".All", false) && ++page < Pages.Length)
            { requested = false; pageSet = false; return; }
            SessionState.SetBool(Key, false); Debug.Log("Lobby capture complete"); EditorApplication.Exit(0);
        }
    }
}
