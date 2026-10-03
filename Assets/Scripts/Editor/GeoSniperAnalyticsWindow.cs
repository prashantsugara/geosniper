#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

namespace GeoSniper.Editor
{
    public sealed class GeoSniperAnalyticsWindow : EditorWindow
    {
        private string firebaseAppId = "1:676740822554:android:88356df370653949b9c3a3";
        private string firebaseProjectId = "geosniper-e58dc";
        private bool analyticsEnabled = true;
        private Vector2 scrollPos;
        private string testEventStatus = "Ready";

        [MenuItem("Tools/GeoSniper/Analytics & Telemetry Dashboard", false, 10)]
        public static void Open()
        {
            var win = GetWindow<GeoSniperAnalyticsWindow>("GeoSniper Analytics");
            win.minSize = new Vector2(480, 520);
            win.Show();
        }

        private void OnEnable()
        {
            LoadConfig();
        }

        private void LoadConfig()
        {
            var cfg = ReleaseConfiguration.Current;
            if (cfg != null)
            {
                firebaseAppId = string.IsNullOrEmpty(cfg.firebaseAppId) ? "1:676740822554:android:88356df370653949b9c3a3" : cfg.firebaseAppId;
                firebaseProjectId = string.IsNullOrEmpty(cfg.firebaseProjectId) ? "geosniper-e58dc" : cfg.firebaseProjectId;
                analyticsEnabled = cfg.analyticsEnabled;
            }
        }

        private void SaveConfig()
        {
            var cfg = ReleaseConfiguration.Current;
            if (cfg != null)
            {
                cfg.firebaseAppId = firebaseAppId.Trim();
                cfg.firebaseProjectId = firebaseProjectId.Trim();
                cfg.analyticsEnabled = analyticsEnabled;

                string path = Path.Combine(Application.dataPath, "Resources/ReleaseConfiguration.json");
                if (File.Exists(path))
                {
                    string json = JsonUtility.ToJson(cfg, true);
                    File.WriteAllText(path, json);
                    AssetDatabase.Refresh();
                    EditorUtility.DisplayDialog("Saved", "Google Analytics & Firebase configuration saved to ReleaseConfiguration.json", "OK");
                }
            }
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("GEOSNIPER ANALYTICS & TELEMETRY", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Local playtime is separate from cloud analytics. Firebase events need an Android build and player consent. Unity events additionally need a linked Cloud project and matching event schemas.", MessageType.Info);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("FIREBASE & GOOGLE SERVICES STATUS", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Firebase Project ID:", firebaseProjectId);
            EditorGUILayout.LabelField("Firebase App ID:", firebaseAppId);
            EditorGUILayout.LabelField("Package Name:", "com.geosniper.game");
            EditorGUILayout.LabelField("google-services.json:", File.Exists(Path.Combine(Application.dataPath, "google-services.json")) ? "Configuration file present" : "Missing");
            EditorGUILayout.LabelField("Firebase SDK:", GameAnalyticsManager.FirebaseReady ? "Initialized" : "Not initialized on Android");
            EditorGUILayout.LabelField("Unity Cloud project:", string.IsNullOrEmpty(Application.cloudProjectId) ? "Not linked" : Application.cloudProjectId);
            EditorGUILayout.LabelField("Unity Analytics SDK:", GameAnalyticsManager.UnityReady ? "Initialized" : "Not initialized");
            EditorGUILayout.LabelField("Player analytics consent:", GameAnalyticsManager.ConsentGranted ? "Allowed" : "Off");
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("ANALYTICS CONFIGURATION", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            analyticsEnabled = EditorGUILayout.Toggle("Analytics Enabled", analyticsEnabled);
            EditorGUILayout.HelpBox("Firebase Analytics uses the official Unity SDK. Do not put a Measurement Protocol API secret in a client build.", MessageType.None);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Save Configuration", GUILayout.Height(28)))
            {
                SaveConfig();
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("LOCAL LIFETIME ENGAGEMENT METRICS", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Total Sessions on Device:", GameAnalyticsManager.TotalSessions.ToString());
            EditorGUILayout.LabelField("Average Session Playtime:", GameAnalyticsManager.AverageSessionFormatted);
            EditorGUILayout.LabelField("Total Cumulative Playtime:", GameAnalyticsManager.TotalPlaytimeFormatted);
            EditorGUILayout.LabelField("Client Anonymous UUID:", GameAnalyticsManager.ClientId);
            EditorGUILayout.LabelField("Last Event Status:", GameAnalyticsManager.LastStatus);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("QUICK ACTIONS & TESTING", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Send Test Event", GUILayout.Height(30)))
            {
                if (EditorApplication.isPlaying)
                {
                    GameAnalyticsManager.TrackCustomEvent("editor_test_ping");
                    testEventStatus = GameAnalyticsManager.ConsentGranted ? "Event queued by initialized SDKs; verify on Android and in each dashboard." : "Analytics is off. Allow it in Privacy & Support first.";
                }
                else
                {
                    testEventStatus = "Enter Play Mode in Unity to test live transmission!";
                    EditorUtility.DisplayDialog("Notice", "Enter Play Mode in the Unity Editor to test live analytics transmission.", "OK");
                }
            }

            if (GUILayout.Button("Open Firebase Console", GUILayout.Height(30)))
            {
                Application.OpenURL("https://console.firebase.google.com/project/" + firebaseProjectId + "/analytics");
            }

            if (GUILayout.Button("Open GA4 Realtime", GUILayout.Height(30)))
            {
                Application.OpenURL("https://analytics.google.com/");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Status: " + testEventStatus, EditorStyles.miniLabel);

            EditorGUILayout.EndScrollView();
        }
    }
}
#endif
