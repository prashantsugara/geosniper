using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GeoSniper
{
    [InitializeOnLoad]
    public static class EditorLobbyCaptureBridge
    {
        private static bool triggered = false;

        static EditorLobbyCaptureBridge()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if (File.Exists("capture_lobby.trigger") && !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.Log("[EditorLobbyCaptureBridge] Trigger detected! Launching play mode...");
                var scene = EditorSceneManager.GetActiveScene();
                if (scene.path != "Assets/Scenes/GeoSniper.unity")
                {
                    EditorSceneManager.OpenScene("Assets/Scenes/GeoSniper.unity");
                }
                EditorApplication.isPlaying = true;
            }
        }

        [MenuItem("GeoSniper/📸 Capture All Lobby Tabs")]
        public static void MenuCapture()
        {
            File.WriteAllText("capture_lobby.trigger", "TRIGGER");
        }
    }
}
