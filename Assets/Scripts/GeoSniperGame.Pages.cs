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

            if(currentTab==LobbyTab.Location && !loading && !showingModeSelector){DrawLocationPicker(w,h);return;}

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
            if (GameButtonChrome.Draw(credRect,"$"+credits.ToString("N0")+" CR",GameButtonTone.Steel,11)) SwitchTab(LobbyTab.Rewards);

            // Settings gear
            Rect settRect = new Rect(w - 145, 10, 48, 34);
            if (GameButtonChrome.Draw(settRect,"⚙",currentTab==LobbyTab.Settings?GameButtonTone.Cyan:GameButtonTone.Steel,15)) SwitchTab(LobbyTab.Settings);

            // Back-to-home button
            Rect closeRect = new Rect(w - 85, 10, 70, 34);
            if (GameButtonChrome.Draw(closeRect,currentTab==LobbyTab.Armory && returnToCampaignFromArmory?"MISSION":"✕ LOBBY",GameButtonTone.Steel,10))
            {if(currentTab==LobbyTab.Armory && returnToCampaignFromArmory){returnToCampaignFromArmory=false;SwitchTab(LobbyTab.Campaign);}else SwitchTab(LobbyTab.Home);}

            // ── CONTENT AREA (between header and bottom nav) ──────────────
            float navH = 62f;
            Rect content = new Rect(0, headerH, w, h - headerH - navH);

            commandPageScale = scale;
            commandPageOffset = new Vector2(safe.x + content.x * scale, Screen.height - safe.yMax + content.y * scale);

            GUI.BeginGroup(content);
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !loading;
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
            int activeTab = loading ? -1 : System.Array.IndexOf(LobbyFooterTabs, currentTab);
            GUI.enabled = previousEnabled && !loading;
            int navClicked = CommandGUI.DrawNavBar(new Rect(0, h - navH, w, navH), LobbyFooterIcons, LobbyFooterLabels, activeTab);
            GUI.enabled = previousEnabled;
            if (navClicked >= 0 && navClicked < LobbyFooterTabs.Length) SwitchTab(LobbyFooterTabs[navClicked]);
        }
    }
}
