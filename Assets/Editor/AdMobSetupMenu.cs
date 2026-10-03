#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GeoSniper
{
    public class AdMobSetupMenu : EditorWindow
    {
        [MenuItem("Tools/GeoSniper/AdMob Configuration")]
        public static void OpenWindow()
        {
            var win = GetWindow<AdMobSetupMenu>("AdMob Setup");
            win.minSize = new Vector2(460, 420);
            win.Show();
        }

        private void OnGUI()
        {
            GUILayout.Label("GeoSniper - Google AdMob Configuration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Official Google AdMob test ad unit IDs are configured by default.\n" +
                "When building for Google Play / App Store, paste your real AdMob unit IDs below.",
                MessageType.Info);

            EditorGUILayout.Space(10);
            GUILayout.Label("Current AdMob Configuration", EditorStyles.boldLabel);

            var adManager = FindFirstObjectByType<AdManager>();
            if (adManager == null)
            {
                if (GUILayout.Button("Create AdManager in Scene", GUILayout.Height(32)))
                {
                    var go = new GameObject("GeoSniper_AdManager");
                    adManager = go.AddComponent<AdManager>();
                    Undo.RegisterCreatedObjectUndo(go, "Create AdManager");
                }
            }
            else
            {
                SerializedObject so = new SerializedObject(adManager);
                so.Update();
                EditorGUILayout.PropertyField(so.FindProperty("testMode"),new GUIContent("Use official test ads"));

                EditorGUILayout.PropertyField(so.FindProperty("customRewardedId"), new GUIContent("Production Rewarded ID"));
                EditorGUILayout.PropertyField(so.FindProperty("customInterstitialId"), new GUIContent("Production Interstitial ID"));
                EditorGUILayout.PropertyField(so.FindProperty("customBannerId"), new GUIContent("Production Banner ID"));

                EditorGUILayout.Space(6);
                EditorGUILayout.PropertyField(so.FindProperty("stagesPerInterstitial"), new GUIContent("Stages Between Interstitials"));
                EditorGUILayout.PropertyField(so.FindProperty("interstitialCooldownSeconds"), new GUIContent("Interstitial Cooldown (sec)"));

                so.ApplyModifiedProperties();
            }

            EditorGUILayout.Space(16);
            GUILayout.Label("Interactive Ad Testing", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Enter Play mode to test. The Editor displays the Google SDK preview; Android displays network test ads. A rewarded ad must finish before a reward is granted.",MessageType.Info);
            GUI.enabled=Application.isPlaying;
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Test Rewarded Ad", GUILayout.Height(30)))
            {
                AdManager.Instance.ShowRewardedAd((rewarded) =>
                {
                    Debug.Log($"[AdMob Test] Rewarded Ad Complete! User rewarded: {rewarded}");
                }, "editor_test_reward");
            }

            if (GUILayout.Button("Test Interstitial Ad", GUILayout.Height(30)))
            {
                AdManager.Instance.ShowInterstitial(() =>
                {
                    Debug.Log("[AdMob Test] Interstitial Ad Closed!");
                }, "editor_test_interstitial");
            }
            EditorGUILayout.EndHorizontal();
            GUI.enabled=true;

            EditorGUILayout.Space(12);
            EditorGUILayout.HelpBox(
                "Official Test Ad Units in use:\n" +
                "• Android Rewarded: " + AdManager.TestRewardedIdAndroid + "\n" +
                "• Android Interstitial: " + AdManager.TestInterstitialIdAndroid + "\n" +
                "• Android Banner: " + AdManager.TestBannerIdAndroid,
                MessageType.None);
        }
    }
}
#endif
