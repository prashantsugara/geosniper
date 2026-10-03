using UnityEngine;

namespace GeoSniper
{
    public sealed partial class GeoSniperGame
    {
        float commandPageScale = 1;
        Vector2 commandPageOffset;

        void DrawCommandPages()
        {
            Rect safe = Screen.safeArea;
            float scale = CommandGUI.GetCanvasScale(safe, out float w, out float h);
            GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, Screen.height - safe.yMax, 0),
                Quaternion.identity, new Vector3(scale, scale, 1));

            LobbyFill(new Rect(0, 0, w, h), LobbyInk);

            if (currentTab == LobbyTab.Campaign && !loading && !showingModeSelector)
            {
                DrawCampaignScreen(w, h);
                return;
            }

            // ── TOP HEADER BAR (54px) ─────────────────────────────────────
            float headerH = 54f;
            LobbyFill(new Rect(0, 0, w, headerH), CommandGUI.ThemeCard);
            LobbyFill(new Rect(0, 4, 3, headerH - 8), CommandGUI.AccentCyan);
            LobbyFill(new Rect(0, headerH - 1, w, 1), CommandGUI.ThemeBorder);

            string pageTitle = loading ? "DEPLOYMENT IN PROGRESS" : showingModeSelector ? "MISSION DEPLOYMENT SELECTOR"
                : currentTab == LobbyTab.Campaign ? "CAMPAIGN // TACTICAL CONTRACTS"
                : currentTab == LobbyTab.Armory   ? "ARMORY // WEAPONS & FIELD MODS"
                : currentTab == LobbyTab.Location ? "GLOBAL SECTOR // REAL-WORLD INTEL"
                : currentTab == LobbyTab.Rewards  ? "SUPPLY VAULT // FIELD REWARDS"
                : currentTab == LobbyTab.Settings ? "SETTINGS // SYSTEM & CONTROLS" : "";

            LobbyLabel(new Rect(16, 0, w * 0.50f, headerH), pageTitle, 13, CommandGUI.AccentCyan, true);

            // Header Right: Balance + Settings + Close (properly spaced, zero collision)
            int credits = PlayerPrefs.GetInt("GeoSniper.Credits", 0);
            Rect credRect = new Rect(w - 290, 10, 135, 34);
            LobbyFill(credRect, new Color(0.04f, 0.08f, 0.04f, 0.90f));
            LobbyFill(new Rect(credRect.x, credRect.y, 2, credRect.height), CommandGUI.AccentGold);
            LobbyLabel(credRect, "  $" + credits.ToString("N0") + " Cr", 11, CommandGUI.AccentGold, true, TextAnchor.MiddleCenter);
            if (GUI.Button(credRect, GUIContent.none, GUIStyle.none)) SwitchTab(LobbyTab.Rewards);

            // Settings gear
            Rect settRect = new Rect(w - 145, 10, 48, 34);
            LobbyFill(settRect, CommandGUI.ThemeCard);
            LobbyFill(new Rect(settRect.x, settRect.yMax - 1, settRect.width, 1), CommandGUI.ThemeBorder);
            LobbyLabel(settRect, "⚙", 15, currentTab == LobbyTab.Settings ? CommandGUI.AccentGold : CommandGUI.Muted, false, TextAnchor.MiddleCenter);
            if (GUI.Button(settRect, GUIContent.none, GUIStyle.none)) SwitchTab(LobbyTab.Settings);

            // Back-to-home button
            Rect closeRect = new Rect(w - 85, 10, 70, 34);
            LobbyFill(closeRect, new Color(0.12f, 0.04f, 0.04f, 0.90f));
            LobbyFill(new Rect(closeRect.x, closeRect.yMax - 1, closeRect.width, 1), CommandGUI.AccentRed);
            LobbyLabel(closeRect, "✕ LOBBY", 10, Color.white, true, TextAnchor.MiddleCenter);
            if (GUI.Button(closeRect, GUIContent.none, GUIStyle.none)) SwitchTab(LobbyTab.Home);

            // ── CONTENT AREA (between header and bottom nav) ──────────────
            float navH = 62f;
            Rect content = new Rect(0, headerH, w, h - headerH - navH);

            commandPageScale = scale;
            commandPageOffset = new Vector2(safe.x + content.x * scale, Screen.height - safe.yMax + content.y * scale);

            GUI.BeginGroup(content);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = !loading;
            try
            {
                if (loading) DrawLoadingScreen(content.width, content.height);
                else if (showingModeSelector) DrawModeSelectorModal(content.width, content.height);
                else switch (currentTab)
                {
                    case LobbyTab.Campaign:  DrawStageSelector(content.width, content.height); break;
                    case LobbyTab.Armory:    DrawArmoryModal(content.width, content.height); break;
                    case LobbyTab.Location:  DrawLocationPicker(content.width, content.height); break;
                    case LobbyTab.Rewards:   DrawRewardsModal(content.width, content.height); break;
                    case LobbyTab.Settings:  DrawSettingsModal(content.width, content.height); break;
                }
            }
            finally
            {
                GUI.EndGroup();
                GUI.enabled = previousEnabled;
            }

            // ── BOTTOM NAV BAR ────────────────────────────────────────────
            int activeTab = loading ? -1 : (int)currentTab;
            string[] navIcons  = { "▲", "≡", "†", "◉", "★" };
            string[] navLabels = { "OVERVIEW", "CAMPAIGN", "ARMORY", "SECTOR", "REWARDS" };
            GUI.enabled = !loading;
            int navClicked = CommandGUI.DrawNavBar(new Rect(0, h - navH, w, navH), navIcons, navLabels, activeTab);
            GUI.enabled = true;
            if (navClicked >= 0) SwitchTab((LobbyTab)navClicked);
        }
    }
}
