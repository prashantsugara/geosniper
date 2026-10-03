using System;
using UnityEngine;

namespace GeoSniper.Duel
{
    public sealed class SniperDuelHUD : MonoBehaviour
    {
        private static SniperDuelHUD instance;
        public static SniperDuelHUD Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<SniperDuelHUD>();
                    if (instance == null)
                    {
                        var go = new GameObject("SniperDuelHUD");
                        instance = go.AddComponent<SniperDuelHUD>();
                    }
                }
                return instance;
            }
        }

        public bool ShowLobbyModal { get; set; } = false;

        private SniperDuelNetwork network;
        private SniperDuelManager duelManager;

        private string inputRoomCode = "";
        private string joinNotice = "";
        private Vector2 lanScroll;
        private int selectedTab = 0; // 0=Create, 1=Join

        private static void DrawRect(Rect r, Color c) => CommandGUI.Fill(r, c);
        private static void DrawLabel(Rect r, string text, int size, Color color, bool bold = false, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            GUI.Label(r, text, GUIStyleCache.Get(size, bold ? FontStyle.Bold : FontStyle.Normal, anchor, color, true));
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            network = SniperDuelNetwork.Instance;
            duelManager = SniperDuelManager.Instance;
        }

        private void OnGUI()
        {
            network = SniperDuelNetwork.Instance;
            duelManager = SniperDuelManager.Instance;

            Matrix4x4 prev = GUI.matrix;
            var safe = Screen.safeArea;
            float scale = CommandGUI.GetCanvasScale(safe, out float w, out float h);
            GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, Screen.height - safe.yMax, 0), Quaternion.identity, new Vector3(scale, scale, 1));

            try
            {
                if (ShowLobbyModal)
                {
                    DrawLobbyModal(w, h);
                }
                else if (duelManager != null && duelManager.MatchState != DuelMatchState.Idle)
                {
                    DrawInMatchHUD(w, h);
                }
            }
            finally
            {
                GUI.matrix = prev;
            }
        }

        private void DrawLobbyModal(float w, float h)
        {
            // Dark dim background
            DrawRect(new Rect(0, 0, w, h), new Color(0.01f, 0.02f, 0.03f, 0.88f));

            float modalW = Mathf.Min(640f, w - 40f);
            float modalH = Mathf.Min(480f, h - 40f);
            Rect modal = new Rect((w - modalW) * 0.5f, (h - modalH) * 0.5f, modalW, modalH);

            CommandGUI.DrawPanel(modal, CommandGUI.ThemeCard, CommandGUI.AccentCyan, 2.0f);

            // Header
            Rect header = new Rect(modal.x, modal.y, modal.width, 44);
            DrawRect(new Rect(modal.x, modal.y + 43, modal.width, 1), CommandGUI.ThemeBorder);
            DrawLabel(new Rect(modal.x + 20, modal.y + 10, modal.width - 100, 24), "1v1 ROOFTOP SNIPER DUEL // MULTIPLAYER", 12, CommandGUI.AccentCyan, true);

            // Close button
            if (CommandGUI.DrawButton(new Rect(modal.xMax - 48, modal.y + 8, 36, 28), "✕", false, 11))
            {
                ShowLobbyModal = false;
                network?.StopLANDiscovery();
            }

            // Tab bar (CREATE MATCH / JOIN MATCH)
            float tabW = (modal.width - 40) / 2;
            if (CommandGUI.DrawButton(new Rect(modal.x + 20, modal.y + 54, tabW, 36), "HOST DUEL ROOM", selectedTab == 0, 11))
            {
                selectedTab = 0;
                if (network != null && !network.IsHost) network.StartHost();
            }
            if (CommandGUI.DrawButton(new Rect(modal.x + 20 + tabW, modal.y + 54, tabW, 36), "JOIN DUEL ROOM", selectedTab == 1, 11))
            {
                selectedTab = 1;
                network?.StartLANDiscovery();
            }

            Rect content = new Rect(modal.x + 20, modal.y + 100, modal.width - 40, modal.height - 120);

            if (selectedTab == 0)
            {
                if (network != null && !network.IsHost) network.StartHost();
                if (network != null && network.IsConnected)
                {
                    ShowLobbyModal = false;
                    GeoSniperGame.Instance?.DeployPvPDuel(true, "");
                    return;
                }
                DrawHostTab(content);
            }
            else
            {
                DrawJoinTab(content);
            }
        }

        private void DrawHostTab(Rect r)
        {
            DrawRect(new Rect(r.x, r.y, r.width, 100), new Color(0.04f, 0.07f, 0.10f, 0.85f));
            DrawRect(new Rect(r.x, r.y, 3, 100), CommandGUI.AccentCyan);

            DrawLabel(new Rect(r.x + 16, r.y + 12, r.width - 32, 18), "YOUR DUEL ROOM CODE:", 10, CommandGUI.Muted, true);
            string code = (network != null && !string.IsNullOrEmpty(network.RoomCode)) ? network.RoomCode : "INITIALIZING...";
            DrawLabel(new Rect(r.x + 16, r.y + 32, r.width - 32, 36), code, 24, CommandGUI.AccentCyan, true);

            string ep = (network != null && !string.IsNullOrEmpty(network.PublicEndpointString)) 
                ? network.PublicEndpointString 
                : SniperDuelNetwork.GetLocalIPAddress() + ":7777";
            DrawLabel(new Rect(r.x + 16, r.y + 72, r.width - 32, 16), "LOCAL IP: " + ep + "  //  BROADCASTING ON LAN", 8, CommandGUI.Muted);

            // Radar search animation
            float pulse = 0.5f + Mathf.PingPong(Time.unscaledTime * 2f, 0.5f);
            Color radarCol = new Color(CommandGUI.AccentCyan.r, CommandGUI.AccentCyan.g, CommandGUI.AccentCyan.b, pulse);
            DrawLabel(new Rect(r.x, r.y + 112, r.width, 22), "◉ WAITING FOR CHALLENGER TO CONNECT...", 11, radarCol, true, TextAnchor.MiddleCenter);

            // Deploy to rooftop & wait button
            Rect waitBtn = new Rect(r.x + (r.width - 280) * 0.5f, r.y + 144, 280, 40);
            if (CommandGUI.DrawButton(waitBtn, "ENTER ROOFTOP & WAIT ➔", true, 11))
            {
                ShowLobbyModal = false;
                GeoSniperGame.Instance?.DeployPvPDuel(true, "");
            }

            // Practice Bot button
            Rect botBtn = new Rect(r.x + (r.width - 280) * 0.5f, r.y + 192, 280, 36);
            if (CommandGUI.DrawButton(botBtn, "PRACTICE VS RIVAL BOT ➔", false, 10))
            {
                ShowLobbyModal = false;
                GeoSniperGame.Instance?.DeployPvPDuel(false, "", true);
            }
        }

        private void DrawJoinTab(Rect r)
        {
            DrawLabel(new Rect(r.x, r.y, r.width, 18), "ENTER HOST ROOM CODE OR DIRECT IP:", 10, CommandGUI.Muted, true);

            GUI.SetNextControlName("RoomCodeInput");
            inputRoomCode = GUI.TextField(new Rect(r.x, r.y + 24, r.width - 140, 36), inputRoomCode, 32);

            Rect joinBtn = new Rect(r.x + r.width - 130, r.y + 24, 130, 36);
            bool hasCode = !string.IsNullOrWhiteSpace(inputRoomCode);
            if (CommandGUI.DrawButton(joinBtn, "ENGAGE ➔", hasCode, 11))
            {
                if (hasCode)
                {
                    ShowLobbyModal = false;
                    GeoSniperGame.Instance?.DeployPvPDuel(false, inputRoomCode.Trim());
                }
                else
                {
                    joinNotice = "ENTER HOST ROOM CODE (e.g. D-0105) OR DIRECT IP";
                }
            }

            // Status message and quick localhost button
            Rect helperRow = new Rect(r.x, r.y + 64, r.width, 24);
            string tipText = string.IsNullOrEmpty(joinNotice) ? "Format: 'D-XXXX' room code, '192.168.x.x', or '127.0.0.1'" : joinNotice;
            DrawLabel(new Rect(helperRow.x, helperRow.y, helperRow.width - 170, 22), tipText, 8, string.IsNullOrEmpty(joinNotice) ? CommandGUI.Muted : CommandGUI.AccentGold);

            Rect localBtn = new Rect(helperRow.xMax - 165, helperRow.y, 165, 22);
            if (CommandGUI.DrawButton(localBtn, "TEST LOCALHOST (127.0.0.1)", false, 8))
            {
                inputRoomCode = "127.0.0.1";
                ShowLobbyModal = false;
                GeoSniperGame.Instance?.DeployPvPDuel(false, "127.0.0.1");
            }

            // LAN discovered hosts header
            DrawLabel(new Rect(r.x, r.y + 92, r.width, 18), "// DETECTED LOCAL NETWORK DUELS //", 9, CommandGUI.AccentGold, true);

            Rect listRect = new Rect(r.x, r.y + 114, r.width, r.height - 124);
            DrawRect(listRect, new Color(0.03f, 0.05f, 0.07f, 0.90f));

            if (network == null || network.DiscoveredLANHosts.Count == 0)
            {
                DrawLabel(listRect, "NO DUELS FOUND ON LOCAL WI-FI / HOTSPOT.\nHOST A DUEL OR ENTER IP / CODE ABOVE.", 10, CommandGUI.Muted, false, TextAnchor.MiddleCenter);
            }
            else
            {
                float itemY = listRect.y + 6;
                foreach (var host in network.DiscoveredLANHosts)
                {
                    Rect hostItem = new Rect(listRect.x + 8, itemY, listRect.width - 16, 42);
                    DrawRect(hostItem, CommandGUI.ThemeCard);
                    DrawRect(new Rect(hostItem.x, hostItem.y, 2, hostItem.height), CommandGUI.AccentCyan);

                    DrawLabel(new Rect(hostItem.x + 12, hostItem.y + 4, hostItem.width - 120, 18), host.callsign + " // ROOFTOP MATCH", 10, Color.white, true);
                    DrawLabel(new Rect(hostItem.x + 12, hostItem.y + 22, hostItem.width - 120, 14), host.endpoint.ToString(), 8, CommandGUI.Muted);

                    if (CommandGUI.DrawButton(new Rect(hostItem.xMax - 95, hostItem.y + 6, 85, 30), "JOIN ➔", true, 9))
                    {
                        ShowLobbyModal = false;
                        GeoSniperGame.Instance?.DeployPvPDuel(false, host.endpoint.ToString());
                    }
                    itemY += 48;
                }
            }
        }

        private void DrawInMatchHUD(float w, float h)
        {
            // ── Top Center Duel Scoreboard ──────────────────────────────────
            float boardW = 340f;
            float boardH = 54f;
            Rect board = new Rect((w - boardW) * 0.5f, 10, boardW, boardH);

            CommandGUI.DrawPanel(board, new Color(0.04f, 0.07f, 0.10f, 0.88f), CommandGUI.ThemeBorder, 1.0f);

            // Left (You) - Cyan
            DrawRect(new Rect(board.x, board.y, 4, board.height), CommandGUI.AccentCyan);
            DrawLabel(new Rect(board.x + 12, board.y + 6, 90, 14), "OPERATOR", 8, CommandGUI.AccentCyan, true);
            int myScore = duelManager != null ? duelManager.LocalScore : 0;
            DrawLabel(new Rect(board.x + 12, board.y + 20, 90, 28), myScore.ToString(), 20, CommandGUI.AccentCyan, true);

            // Center Match Round info
            int rivalScore = duelManager != null ? duelManager.RivalScore : 0;
            int target = duelManager != null ? duelManager.TargetScore : 3;
            string phase = (duelManager != null && duelManager.MatchState == DuelMatchState.InRound) 
                ? $"ROUND {myScore + rivalScore + 1} / {target * 2 - 1}"
                : (duelManager != null ? duelManager.MatchState.ToString().ToUpper() : "DUEL");
            DrawLabel(new Rect(board.x + 100, board.y + 8, 140, 16), "1v1 SNIPER DUEL", 9, Color.white, true, TextAnchor.MiddleCenter);
            DrawLabel(new Rect(board.x + 100, board.y + 26, 140, 16), phase, 8, CommandGUI.Muted, true, TextAnchor.MiddleCenter);

            // Right (Rival) - Red
            DrawRect(new Rect(board.xMax - 4, board.y, 4, board.height), CommandGUI.AccentRed);
            string rivalName = (network != null && !string.IsNullOrEmpty(network.RemoteCallsign)) ? network.RemoteCallsign : "RIVAL";
            DrawLabel(new Rect(board.xMax - 102, board.y + 6, 90, 14), rivalName, 8, CommandGUI.AccentRed, true, TextAnchor.MiddleRight);
            DrawLabel(new Rect(board.xMax - 102, board.y + 20, 90, 28), rivalScore.ToString(), 20, CommandGUI.AccentRed, true, TextAnchor.MiddleRight);

            // Latency & Telemetry
            int ping = network != null ? network.PingMs : 0;
            string pingStr = $"RTT: {ping}ms // UDP";
            DrawLabel(new Rect(board.x, board.yMax + 4, boardW, 14), pingStr, 7, CommandGUI.Muted, false, TextAnchor.MiddleCenter);

            // ── Opponent Scope Glint Lock Warning ────────────────────────────
            if (duelManager != null && duelManager.Opponent != null && duelManager.Opponent.IsAimingAtPlayer)
            {
                float flash = 0.6f + Mathf.PingPong(Time.unscaledTime * 5f, 0.4f);
                Color alertCol = new Color(1.0f, 0.15f, 0.15f, flash);

                Rect alertRect = new Rect((w - 380) * 0.5f, 85, 380, 28);
                DrawRect(alertRect, new Color(0.20f, 0.02f, 0.02f, 0.85f));
                DrawRect(new Rect(alertRect.x, alertRect.y, alertRect.width, 1), alertCol);
                DrawRect(new Rect(alertRect.x, alertRect.yMax - 1, alertRect.width, 1), alertCol);
                DrawLabel(alertRect, "⚠ WARNING: ENEMY OPTIC LOCKED // BREAK SIGHTLINE! ⚠", 9, alertCol, true, TextAnchor.MiddleCenter);
            }

            // ── Center Match Notice (Countdown, Elimination, Victory) ──────
            if (duelManager != null && duelManager.NoticeTimer > 0 && !string.IsNullOrEmpty(duelManager.MatchNotice))
            {
                Rect noticeRect = new Rect((w - 460) * 0.5f, h * 0.35f, 460, 70);
                DrawRect(noticeRect, new Color(0.02f, 0.04f, 0.06f, 0.92f));
                DrawRect(new Rect(noticeRect.x, noticeRect.y, 3, noticeRect.height), CommandGUI.AccentGold);
                DrawRect(new Rect(noticeRect.xMax - 3, noticeRect.y, 3, noticeRect.height), CommandGUI.AccentGold);
                DrawLabel(noticeRect, duelManager.MatchNotice, 13, CommandGUI.AccentGold, true, TextAnchor.MiddleCenter);
            }

            // ── Match Over Banner & Action Buttons ──────────────────────────
            if (duelManager != null && duelManager.MatchState == DuelMatchState.MatchOver)
            {
                Rect overModal = new Rect((w - 360) * 0.5f, h * 0.52f, 360, 110);
                CommandGUI.DrawPanel(overModal, CommandGUI.ThemeCard, CommandGUI.AccentGold, 1.5f);

                Rect rematchBtn = new Rect(overModal.x + 20, overModal.y + 16, overModal.width - 40, 36);
                if (CommandGUI.DrawButton(rematchBtn, "REMATCH ➔", true, 11))
                {
                    duelManager.RequestRematch();
                }

                Rect leaveBtn = new Rect(overModal.x + 20, overModal.y + 60, overModal.width - 40, 34);
                if (CommandGUI.DrawButton(leaveBtn, "RETURN TO LOBBY", false, 10))
                {
                    duelManager.LeaveDuel();
                }
            }
        }
    }
}
