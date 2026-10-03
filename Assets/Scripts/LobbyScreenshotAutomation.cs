using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace GeoSniper
{
    public class LobbyScreenshotAutomation : MonoBehaviour
    {
        private static LobbyScreenshotAutomation instance;
        private bool isCapturing = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (instance != null) return;
            var go = new GameObject("GeoSniper_LobbyScreenshotAutomation");
            instance = go.AddComponent<LobbyScreenshotAutomation>();
            DontDestroyOnLoad(go);
        }

        private static string GetTriggerPath()
        {
            return Path.Combine(Application.dataPath, "..", "capture_lobby.trigger");
        }

        private void Start()
        {
            CheckTrigger();
        }

        private void Update()
        {
            if (isCapturing) return;

            if (Input.GetKeyDown(KeyCode.F12) || Input.GetKeyDown(KeyCode.F9))
            {
                TriggerCapture();
                return;
            }

            CheckTrigger();
        }

        public void CheckTrigger()
        {
            if (isCapturing) return;
            string trig1 = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "capture_lobby.trigger"));
            string trig2 = Path.Combine(Application.dataPath, "capture_lobby.trigger");
            string trig3 = "capture_lobby.trigger";
            if (File.Exists(trig1) || File.Exists(trig2) || File.Exists(trig3))
            {
                TriggerCapture();
            }
        }

        public void TriggerCapture()
        {
            if (isCapturing) return;
            try
            {
                string trig1 = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "capture_lobby.trigger"));
                string trig2 = Path.Combine(Application.dataPath, "capture_lobby.trigger");
                if (File.Exists(trig1)) File.Delete(trig1);
                if (File.Exists(trig2)) File.Delete(trig2);
                if (File.Exists("capture_lobby.trigger")) File.Delete("capture_lobby.trigger");
            }
            catch {}

            StartCoroutine(CaptureAllTabsRoutine());
        }

        private IEnumerator CaptureAllTabsRoutine()
        {
            isCapturing = true;
            Debug.Log("[LobbyScreenshotAutomation] Starting capture of all 6 lobby tabs...");

            var game = FindAnyObjectByType<GeoSniperGame>();
            if (game == null)
            {
                Debug.LogWarning("[LobbyScreenshotAutomation] GeoSniperGame instance not found!");
                isCapturing = false;
                yield break;
            }

            string outDir = Path.Combine(Application.dataPath, "..", "Screenshots", "Lobby");
            Directory.CreateDirectory(outDir);

            // Tab metadata
            var tabs = new[]
            {
                (GeoSniperGame.LobbyTab.Home, "0_Home"),
                (GeoSniperGame.LobbyTab.Campaign, "1_Campaign"),
                (GeoSniperGame.LobbyTab.Armory, "2_Armory"),
                (GeoSniperGame.LobbyTab.Location, "3_Location"),
                (GeoSniperGame.LobbyTab.Rewards, "4_Rewards"),
                (GeoSniperGame.LobbyTab.Settings, "5_Settings")
            };

            yield return null;

            for (int i = 0; i < tabs.Length; i++)
            {
                var (tab, name) = tabs[i];
                Debug.Log($"[LobbyScreenshotAutomation] Capturing {name}...");

                game.SwitchTab(tab);

                // Wait 3 frames to ensure OnGUI layout + camera render passes complete
                yield return null;
                yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();

                string filePath = Path.Combine(outDir, $"{name}.png");
                try
                {
                    if (File.Exists(filePath)) File.Delete(filePath);
                }
                catch {}

                ScreenCapture.CaptureScreenshot(filePath);

                // Short delay to allow PNG encoder to flush
                yield return new WaitForSecondsRealtime(0.25f);
            }

            // Return to Home
            game.SwitchTab(GeoSniperGame.LobbyTab.Home);
            yield return new WaitForEndOfFrame();

            string doneMarker = Path.Combine(outDir, "capture_done.txt");
            File.WriteAllText(doneMarker, DateTime.UtcNow.ToString("o"));

            Debug.Log("[LobbyScreenshotAutomation] Successfully captured all tabs to Screenshots/Lobby/!");
            isCapturing = false;
        }

        private void OnGUI()
        {
            CheckTrigger();
            // Discreet capture button in top-right for easy manual trigger in Editor / Play mode
            if (Application.isEditor || Debug.isDebugBuild)
            {
                var prevColor = GUI.color;
                GUI.color = new Color(1, 1, 1, 0.45f);
                if (GUI.Button(new Rect(Screen.width - 95, 2, 90, 22), isCapturing ? "SAVING..." : "📸 SCREENSHOTS", GUI.skin.button))
                {
                    TriggerCapture();
                }
                GUI.color = prevColor;
            }
        }
    }
}
