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

        public static bool LobbyModalOpen => instance != null && instance.showLobbyModal;
        private bool showLobbyModal = false;
        public bool ShowLobbyModal
        {
            get => showLobbyModal;
            set
            {
                showLobbyModal = value;
                if (value)
                {
                    network = SniperDuelNetwork.Instance;
                    if (selectedTab == 1) network.PrepareToJoin();
                    if (selectedTab == 0 && network != null && !network.IsHost)
                    {
                        network.StartHost();
                    }
                }
            }
        }

        private SniperDuelNetwork network;
        private SniperDuelManager duelManager;

        private string inputRoomCode = "";
        private string joinNotice = "";
        private string shareNotice = "";
        private float shareNoticeUntil;
        private Vector2 lanScroll;
        private Vector2 hostScroll;
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

            int previousDepth = GUI.depth;
            GUI.depth = -300;
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
                else if (duelManager != null && duelManager.HasActiveDuel)
                {
                    if (DuelKillReplay.Active) DrawLastMoments(w, h, safe, scale);
                    else DrawInMatchHUD(w, h);
                }
            }
            finally
            {
                GUI.matrix = prev;
                GUI.depth = previousDepth;
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
                network?.Disconnect("Duel room closed.");
            }

            // Tab bar (CREATE MATCH / JOIN MATCH)
            float tabW = (modal.width - 40) / 2;
            if (CommandGUI.DrawButton(new Rect(modal.x + 20, modal.y + 54, tabW, 36), network != null && !network.IsHost && network.LastDisconnectReason.Length>0 ? "RETRY HOST ROOM" : "HOST DUEL ROOM", selectedTab == 0, 11))
            {
                selectedTab = 0;
                if (network != null && !network.IsHost) network.StartHost();
            }
            if (CommandGUI.DrawButton(new Rect(modal.x + 20 + tabW, modal.y + 54, tabW, 36), "JOIN DUEL ROOM", selectedTab == 1, 11))
            {
                if (selectedTab != 1)
                {
                    selectedTab = 1;
                    network?.PrepareToJoin();
                }
            }

            Rect content = new Rect(modal.x + 20, modal.y + 100, modal.width - 40, modal.height - 120);

            if (selectedTab == 0)
            {
                DrawHostTab(content);
            }
            else
            {
                DrawJoinTab(content);
            }
        }

        private void DrawHostTab(Rect r)
        {
            bool scroll = r.height < 360;
            if (scroll)
            {
                hostScroll = GUI.BeginScrollView(r, hostScroll, new Rect(r.x, r.y, r.width - 18, 360));
                r.width -= 18;
            }
            DrawRect(new Rect(r.x, r.y, r.width, 100), new Color(0.04f, 0.07f, 0.10f, 0.85f));
            DrawRect(new Rect(r.x, r.y, 3, 100), CommandGUI.AccentCyan);

            DrawLabel(new Rect(r.x + 16, r.y + 12, r.width - 32, 18), "YOUR DUEL ROOM CODE:", 10, CommandGUI.Muted, true);
            string code = (network != null && !string.IsNullOrEmpty(network.RoomCode)) ? network.RoomCode : network != null && network.LastDisconnectReason.Length>0 ? "ROOM UNAVAILABLE" : "INITIALIZING...";
            DrawLabel(new Rect(r.x + 16, r.y + 32, r.width - 32, 36), code, 24, CommandGUI.AccentCyan, true);

            DrawLabel(new Rect(r.x + 16, r.y + 72, r.width - 32, 24), network == null ? "" : (network.LastDisconnectReason.Length>0 ? network.LastDisconnectReason : network.ConnectionStatus), 8, CommandGUI.Muted);

            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && network != null && network.IsHost && !string.IsNullOrEmpty(network.RoomCode);
            if (CommandGUI.DrawButton(new Rect(r.x, r.y + 108, 170, 34), "SHARE INVITE", false, 11)) ShareHostInvite();
            GUI.enabled = previousEnabled;
            DrawLabel(new Rect(r.x + 180, r.y + 108, r.width - 180, 34), Time.unscaledTime < shareNoticeUntil ? shareNotice : "PLAY STORE LINK + YOUR JOIN CODE", 9, CommandGUI.AccentGold);

            // Radar search animation
            float pulse = 0.5f + Mathf.PingPong(Time.unscaledTime * 2f, 0.5f);
            Color radarCol = new Color(CommandGUI.AccentCyan.r, CommandGUI.AccentCyan.g, CommandGUI.AccentCyan.b, pulse);
            DrawLabel(new Rect(r.x, r.y + 149, r.width, 32), network != null && network.IsConnected
                ? "CHALLENGER CONNECTED — CHOOSE YOUR DUEL LOCATION" : "CHOOSE A LOCATION — YOUR OPPONENT WILL LOAD THE SAME MAP", 10, radarCol, true, TextAnchor.MiddleCenter);

            bool competitive = network != null && network.CompetitiveSpotting;
            bool roomLocked = network == null || network.RoomRulesLocked;
            GUI.enabled = previousEnabled && !roomLocked;
            if (CommandGUI.DrawButton(new Rect(r.x, r.y + 187, r.width, 38), competitive ? "COMPETITIVE / GUNFIRE PINGS" : "CASUAL / LIVE RIVAL TRACKING", competitive, 10))
                network.ConfigureRoom(!competitive);
            GUI.enabled = previousEnabled;
            DrawLabel(new Rect(r.x, r.y + 226, r.width, 20), roomLocked ? "COMPACT ARENA // 3-MINUTE ROUNDS // ROOM SETTINGS LOCKED" : "COMPACT ARENA // 3-MINUTE ROUNDS // TAP TO CHANGE TRACKING", 8, CommandGUI.Muted, false, TextAnchor.MiddleCenter);

            // Deploy to rooftop & wait button
            GUI.enabled = previousEnabled && network != null && network.IsHost && !network.IsRelayStarting && !string.IsNullOrEmpty(network.RoomCode);
            Rect waitBtn = new Rect(r.x, r.y + 254, (r.width - 12) * .5f, 44);
            if (CommandGUI.DrawButton(waitBtn, "MY LIVE LOCATION", true, 11))
            {
                ShowLobbyModal = false;
                GeoSniperGame.Instance?.ChooseDuelLocation(true);
            }
            if (CommandGUI.DrawButton(new Rect(waitBtn.xMax + 12, waitBtn.y, waitBtn.width, waitBtn.height), "CHOOSE A PLACE", false, 11))
                GeoSniperGame.Instance?.ChooseDuelLocation(false);
            GUI.enabled = previousEnabled;

            // Practice Bot button
            Rect botBtn = new Rect(r.x + (r.width - 280) * 0.5f, r.y + 312, 280, 36);
            if (CommandGUI.DrawButton(botBtn, "PRACTICE VS RIVAL BOT ➔", false, 10))
            {
                ShowLobbyModal = false;
                GeoSniperGame.Instance?.DeployPvPDuel(false, "", true);
            }
            if (scroll) GUI.EndScrollView();
        }

        private void ShareHostInvite()
        {
            if (network == null || !network.IsHost) return;
            shareNotice = DuelInviteShare.Share(network.RoomCode, network.LocalCallsign);
            shareNoticeUntil = Time.unscaledTime + 10f;
        }

        private void ExecuteJoinDuel()
        {
            if (string.IsNullOrWhiteSpace(inputRoomCode))
            {
                joinNotice = "Enter the 10-digit room code shared by the host.";
                return;
            }
            if (!DuelInviteShare.TryExtractJoinTarget(inputRoomCode, out string target))
            {
                joinNotice = "Enter a valid Relay code, or paste the complete invite.";
                return;
            }
            ShowLobbyModal = false;
            GeoSniperGame.Instance?.DeployPvPDuel(false, target);
        }

        private void DrawJoinTab(Rect r)
        {
            DrawLabel(new Rect(r.x, r.y, r.width, 18), "ENTER 10-DIGIT ROOM CODE OR PASTE INVITE:", 10, CommandGUI.Muted, true);

            GUI.SetNextControlName("RoomCodeInput");
            inputRoomCode = GUI.TextField(new Rect(r.x, r.y + 24, r.width - 160, 36), inputRoomCode, 1024);

            Rect joinBtn = new Rect(r.x + r.width - 150, r.y + 24, 150, 36);
            if (CommandGUI.DrawButton(joinBtn, "JOIN DUEL ROOM ➔", true, 10))
            {
                ExecuteJoinDuel();
            }

            // Clipboard access happens only when the player taps Paste.
            Rect helperRow = new Rect(r.x, r.y + 64, r.width, 24);
            string tipText = string.IsNullOrEmpty(joinNotice) ? "10-digit room code or a shared invite." : joinNotice;
            DrawLabel(new Rect(helperRow.x, helperRow.y, helperRow.width - 110, 22), tipText, 8, string.IsNullOrEmpty(joinNotice) ? CommandGUI.Muted : CommandGUI.AccentGold);
            if (CommandGUI.DrawButton(new Rect(helperRow.xMax - 100, helperRow.y, 100, 24), "PASTE INVITE", false, 9))
            {
                if (DuelInviteShare.TryExtractJoinTarget(GUIUtility.systemCopyBuffer, out string pasted))
                {
                    inputRoomCode = pasted;
                    joinNotice = "Host code pasted. Tap JOIN DUEL ROOM.";
                }
                else joinNotice = "Clipboard has no valid host code or invite.";
            }

            // LAN discovered hosts header
            DrawLabel(new Rect(r.x, r.y + 92, r.width, 18), "// CONNECT THROUGH UNITY RELAY //", 9, CommandGUI.AccentGold, true);

            Rect listRect = new Rect(r.x, r.y + 114, r.width, r.height - 124);
            DrawRect(listRect, new Color(0.03f, 0.05f, 0.07f, 0.90f));

            if (network == null || network.DiscoveredLANHosts.Count == 0)
            {
                DrawLabel(new Rect(listRect.x, listRect.y + 10, listRect.width, 36), "PLAY TOGETHER ON WI-FI OR MOBILE DATA.\nENTER THE RELAY CODE ABOVE, OR PRACTICE:", 9, CommandGUI.Muted, false, TextAnchor.MiddleCenter);

                Rect soloBtn = new Rect(listRect.x + (listRect.width - 240) * 0.5f, listRect.y + 54, 240, 36);
                if (CommandGUI.DrawButton(soloBtn, "PLAY VS RIVAL BOT ➔", false, 10))
                {
                    ShowLobbyModal = false;
                    GeoSniperGame.Instance?.DeployPvPDuel(false, "", true);
                }
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

        private float locatorSightCheckAt;
        private bool rivalInSight;

        // Project through the same safe-area transform as the HUD. Markers stay
        // within the visible scope circle and avoid the mobile control corners.
        private static Vector2 ProjectRivalMarker(Camera camera, Vector3 target, Rect safe, float scale, Rect bounds, bool scoped, out bool atEdge)
        {
            Vector3 screen = camera.WorldToScreenPoint(target);
            Vector2 point = new Vector2((screen.x - safe.x) / scale, (safe.yMax - screen.y) / scale);
            Vector2 center = bounds.center;
            Vector2 offset = point - center;
            if (screen.z <= 0) offset = -offset;
            if (offset.sqrMagnitude < .01f && screen.z <= 0) offset = Vector2.right;
            if (scoped)
            {
                float radius = Mathf.Min(bounds.width, bounds.height) * .5f;
                atEdge = screen.z <= 0 || offset.sqrMagnitude > radius * radius;
                return center + (atEdge ? offset.normalized * radius : offset);
            }
            atEdge = screen.z <= 0 || !bounds.Contains(point);
            if (!atEdge) return point;
            float factor = Mathf.Max(Mathf.Abs(offset.x) / (bounds.width * .5f), Mathf.Abs(offset.y) / (bounds.height * .5f));
            return center + offset / Mathf.Max(factor, .001f);
        }

        private void DrawRivalLocator(float w, float h)
        {
            var rival = duelManager.Opponent;
            if (!duelManager.CanShowRivalLocator || !duelManager.RivalTrackingVisible || rival == null || rival.IsDead || !rival.gameObject.activeInHierarchy) return;
            Camera camera = duelManager.GameplayCamera != null ? duelManager.GameplayCamera : Camera.main;
            if (camera == null) return;
            Vector3 target = rival.LocatorPosition;
            Vector3 delta = target - camera.transform.position;
            float bearing = Vector3.SignedAngle(new Vector3(camera.transform.forward.x, 0, camera.transform.forward.z), new Vector3(delta.x, 0, delta.z), Vector3.up);
            string direction = Mathf.Abs(bearing) < 10 ? "AHEAD" : Mathf.Abs(bearing) > 150 ? "BEHIND" : bearing < 0 ? "LEFT" : "RIGHT";
            if (Time.unscaledTime >= locatorSightCheckAt)
            {
                locatorSightCheckAt = Time.unscaledTime + .1f;
                rivalInSight = !Physics.Linecast(camera.transform.position, target, out var hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    || hit.collider.GetComponentInParent<DuelOpponentAvatar>() == rival;
            }
            string height = Mathf.Abs(delta.y) < 3 ? "SAME LEVEL" : delta.y > 0 ? $"{delta.y:F0}m ABOVE" : $"{-delta.y:F0}m BELOW";
            Rect guidance = new Rect((w - 380) * .5f, 118, 380, 45);
            CommandGUI.DrawPanel(guidance, new Color(.02f, .035f, .045f, .92f), CommandGUI.AccentRed, 1f);
            DrawLabel(new Rect(guidance.x + 5, guidance.y + 3, 370, 21), $"RIVAL {direction} // {delta.magnitude:F0}m // {height}", 13, Color.white, true, TextAnchor.MiddleCenter);
            DrawLabel(new Rect(guidance.x + 5, guidance.y + 24, 370, 17), rivalInSight ? "FOLLOW THE RED MARKER // AIM TO IDENTIFY" : "BEHIND COVER // FOLLOW THE RED MARKER", 10, CommandGUI.AccentGold, true, TextAnchor.MiddleCenter);
            bool scoped = duelManager.LocalIsScoped;
            Rect safe = Screen.safeArea;
            float scale = CommandGUI.GetCanvasScale(safe, out _, out _);
            Vector2 scopeCenter = new Vector2((Screen.width * .5f - safe.x) / scale, (safe.yMax - Screen.height * .5f) / scale);
            float scopeRadius = Mathf.Min(Screen.width, Screen.height) / scale * .30f;
            Rect bounds = scoped ? new Rect(scopeCenter.x - scopeRadius, scopeCenter.y - scopeRadius, scopeRadius * 2, scopeRadius * 2)
                : new Rect(70, 185, w - 140, h - 330);
            Vector2 point = ProjectRivalMarker(camera, target, safe, scale, bounds, scoped, out bool atEdge);
            Color color = CommandGUI.AccentRed;
            if (atEdge)
            {
                Vector2 outward = (point - bounds.center).normalized;
                Vector2 side = new Vector2(-outward.y, outward.x);
                DrawLocatorLine(point, point - outward * 17 + side * 9, 3, color);
                DrawLocatorLine(point, point - outward * 17 - side * 9, 3, color);
                point -= outward * 25;
            }
            else
            {
                // A compact diamond locates the actual head, without covering it.
                Vector2 badge = point + Vector2.up * 24;
                DrawLocatorLine(badge + Vector2.up * 8, badge + Vector2.right * 8, 2, color);
                DrawLocatorLine(badge + Vector2.right * 8, badge + Vector2.down * 8, 2, color);
                DrawLocatorLine(badge + Vector2.down * 8, badge + Vector2.left * 8, 2, color);
                DrawLocatorLine(badge + Vector2.left * 8, badge + Vector2.up * 8, 2, color);
                DrawLocatorLine(badge + Vector2.down * 8, point, 1, color);
                point = badge + Vector2.up * 14;
            }
            Rect label = new Rect(Mathf.Clamp(point.x - 65, 5, w - 135), Mathf.Clamp(point.y - 20, 165, h - 140), 130, 20);
            DrawRect(label, new Color(.015f, .025f, .035f, .85f));
            DrawLabel(label, $"RIVAL {delta.magnitude:F0}m", 12, color, true, TextAnchor.MiddleCenter);
        }

        private static void DrawLocatorLine(Vector2 start, Vector2 end, float thickness, Color color)
        {
            Matrix4x4 matrix = GUI.matrix;
            Color previous = GUI.color;
            Vector2 delta = end - start;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, start);
            GUI.color = color;
            GUI.DrawTexture(new Rect(start.x, start.y - thickness * .5f, delta.magnitude, thickness), Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.matrix = matrix;
        }

        private void DrawInMatchHUD(float w, float h)
        {
            // ── Exit Button during Lobby Wait ────────────────────────────────
            if (duelManager != null && duelManager.MatchState == DuelMatchState.LobbyWait)
            {
                Rect exitBtn = new Rect(16, 12, 130, 32);
                if (CommandGUI.DrawButton(exitBtn, "✕ EXIT DUEL", false, 9))
                {
                    duelManager.LeaveDuel();
                }
                if (network != null && network.IsHost && !string.IsNullOrEmpty(network.RoomCode))
                {
                    if (CommandGUI.DrawButton(new Rect(16, 52, 130, 32), "SHARE INVITE", false, 9)) ShareHostInvite();
                    if (Time.unscaledTime < shareNoticeUntil)
                        DrawLabel(new Rect(16, 88, 200, 42), shareNotice, 9, CommandGUI.AccentGold);
                }
            }

            // ── Top Center Duel Scoreboard ──────────────────────────────────
            float boardW = Mathf.Min(420f, w - 340f);
            float boardH = 68f;
            Rect board = new Rect((w - boardW) * 0.5f, 10, boardW, boardH);

            CommandGUI.DrawPanel(board, new Color(0.04f, 0.07f, 0.10f, 0.88f), CommandGUI.ThemeBorder, 1.0f);

            // Left (You) - Cyan
            DrawRect(new Rect(board.x, board.y, 4, board.height), CommandGUI.AccentCyan);
            DrawLabel(new Rect(board.x + 14, board.y + 5, 92, 20), "YOU", 12, CommandGUI.AccentCyan, true);
            int myScore = duelManager != null ? duelManager.LocalScore : 0;
            DrawLabel(new Rect(board.x + 14, board.y + 25, 92, 36), myScore.ToString(), 28, CommandGUI.AccentCyan, true);

            // Center Match Round info
            int rivalScore = duelManager != null ? duelManager.RivalScore : 0;
            int seconds = duelManager != null ? Mathf.CeilToInt(duelManager.RoundSecondsRemaining) : 0;
            bool inRound = duelManager != null && duelManager.MatchState == DuelMatchState.InRound;
            string phase = inRound ? $"ROUND {duelManager.RoundNumber}" : DuelPhaseLabel(duelManager.MatchState);
            Rect center = new Rect(board.x + 108, board.y + 5, board.width - 216, 22);
            DrawLabel(center, phase, 12, Color.white, true, TextAnchor.MiddleCenter);
            DrawLabel(new Rect(center.x, board.y + 27, center.width, 32), inRound ? $"{seconds / 60:00}:{seconds % 60:00}" : "FIRST TO 3", inRound ? 22 : 12, CommandGUI.Muted, true, TextAnchor.MiddleCenter);

            // Right (Rival) - Red
            DrawRect(new Rect(board.xMax - 4, board.y, 4, board.height), CommandGUI.AccentRed);
            string rivalName = (network != null && !string.IsNullOrEmpty(network.RemoteCallsign)) ? network.RemoteCallsign : "RIVAL";
            if(rivalName.Length > 12) rivalName = rivalName.Substring(0, 11) + "…";
            DrawLabel(new Rect(board.xMax - 106, board.y + 5, 92, 20), rivalName, 12, CommandGUI.AccentRed, true, TextAnchor.MiddleRight);
            DrawLabel(new Rect(board.xMax - 106, board.y + 25, 92, 36), rivalScore.ToString(), 28, CommandGUI.AccentRed, true, TextAnchor.MiddleRight);

            // Latency & Telemetry
            int ping = network != null ? network.PingMs : 0;
            if(CombatHudPreferences.TacticalDetails || ping >= 180 || duelManager.Reconnecting)
            {
                string pingStr = duelManager.Reconnecting ? "RECONNECTING…" : $"{ping} ms" + (ping >= 180 ? " // SLOW CONNECTION" : "");
                DrawLabel(new Rect(board.x, board.yMax + 3, boardW, 22), pingStr, 11, ping >= 180 || duelManager.Reconnecting ? CommandGUI.AccentGold : CommandGUI.Muted, false, TextAnchor.MiddleCenter);
            }

            DrawRivalLocator(w, h);

            if (duelManager != null && duelManager.MatchState == DuelMatchState.InRound && duelManager.LocalBoundarySecondsRemaining >= 0)
            {
                Rect boundary = new Rect((w - 340) * .5f, h - 174, 340, 35);
                CommandGUI.DrawPanel(boundary, new Color(.15f, .025f, .025f, .95f), CommandGUI.AccentRed, 1f);
                DrawLabel(boundary, $"RETURN TO DUEL AREA // {Mathf.CeilToInt(duelManager.LocalBoundarySecondsRemaining)}s", 12, CommandGUI.AccentGold, true, TextAnchor.MiddleCenter);
            }

            // ── Opponent Scope Glint Lock Warning ────────────────────────────
            if (duelManager != null && duelManager.Opponent != null && duelManager.Opponent.IsAimingAtPlayer)
            {
                float flash = 0.6f + Mathf.PingPong(Time.unscaledTime * 5f, 0.4f);
                Color alertCol = new Color(1.0f, 0.15f, 0.15f, flash);

                Rect alertRect = new Rect((w - 380) * 0.5f, 171, 380, 28);
                DrawRect(alertRect, new Color(0.20f, 0.02f, 0.02f, 0.85f));
                DrawRect(new Rect(alertRect.x, alertRect.y, alertRect.width, 1), alertCol);
                DrawRect(new Rect(alertRect.x, alertRect.yMax - 1, alertRect.width, 1), alertCol);
                DrawLabel(alertRect, "RIVAL AIMING AT YOU // TAKE COVER", 12, alertCol, true, TextAnchor.MiddleCenter);
            }

            // ── Center Match Notice (Countdown, Elimination, Victory) ──────
            if (duelManager != null && duelManager.NoticeTimer > 0 && !string.IsNullOrEmpty(duelManager.MatchNotice))
            {
                // Live-round notices leave the aiming point clear; round and connection
                // transitions remain prominent while action is paused.
                Rect noticeRect = inRound ? new Rect((w - 460) * .5f, h - 208, 460, 40)
                    : new Rect((w - 460) * .5f, h * .35f, 460, 70);
                DrawRect(noticeRect, new Color(0.02f, 0.04f, 0.06f, 0.92f));
                DrawRect(new Rect(noticeRect.x, noticeRect.y, 3, noticeRect.height), CommandGUI.AccentGold);
                DrawRect(new Rect(noticeRect.xMax - 3, noticeRect.y, 3, noticeRect.height), CommandGUI.AccentGold);
                DrawLabel(noticeRect, duelManager.MatchNotice, 13, CommandGUI.AccentGold, true, TextAnchor.MiddleCenter);
            }

            // ── Match Over Banner & Action Buttons ──────────────────────────
            if (duelManager != null && duelManager.MatchState == DuelMatchState.MatchOver)
            {
                DrawRematchActions(w, h);
            }

            if (DuelKillReplay.Recap.HasValue && duelManager != null
                && (duelManager.MatchState == DuelMatchState.RoundOver || duelManager.MatchState == DuelMatchState.MatchOver))
                DrawDamageRecap(new Rect((w - Mathf.Min(500, w - 32)) * .5f, h - 158, Mathf.Min(500, w - 32), 92));
        }

        private static string DuelPhaseLabel(DuelMatchState state)
        {
            switch(state)
            {
                case DuelMatchState.LobbyWait: return "WAITING FOR RIVAL";
                case DuelMatchState.Warmup: return "GET READY";
                case DuelMatchState.RoundOver: return "ROUND COMPLETE";
                case DuelMatchState.MatchOver: return "MATCH COMPLETE";
                default: return "1v1 DUEL";
            }
        }

        private void DrawRematchActions(float w, float h)
        {
            Rect panel = new Rect((w - 440) * .5f, h * .46f, 440, 190);
            CommandGUI.DrawPanel(panel, CommandGUI.ThemeCard, CommandGUI.AccentGold, 1.5f);
            bool incoming = duelManager.RematchIncomingPending;
            bool outgoing = duelManager.RematchOutgoingPending;
            string heading = incoming ? "RIVAL WANTS A REMATCH" : outgoing ? "REMATCH REQUEST SENT" : "PLAY AGAIN?";
            DrawLabel(new Rect(panel.x + 16, panel.y + 10, panel.width - 32, 26), heading, 16, Color.white, true, TextAnchor.MiddleCenter);
            string status = duelManager.RematchStatus;
            if (string.IsNullOrEmpty(status)) status = duelManager.CanRematch ? "Both players must agree before the next match." : duelManager.Reconnecting ? "Waiting for the connection to return." : "Your rival has left the room.";
            if (incoming || outgoing) status += $" ({Mathf.CeilToInt(duelManager.RematchSecondsRemaining)}s)";
            DrawLabel(new Rect(panel.x + 16, panel.y + 40, panel.width - 32, 42), status, 12, CommandGUI.Muted, false, TextAnchor.MiddleCenter);
            bool previouslyEnabled = GUI.enabled;
            try
            {
                GUI.enabled = previouslyEnabled && duelManager.CanRematch;
                Rect action = new Rect(panel.x + 20, panel.y + 88, panel.width - 40, 42);
                if(incoming)
                {
                    GUI.enabled = GUI.enabled && duelManager.CanCancelRematch;
                    if(CommandGUI.DrawButton(new Rect(action.x, action.y, (action.width - 12) * .5f, action.height), "ACCEPT", true, 14)) duelManager.AcceptRematch();
                    if(CommandGUI.DrawButton(new Rect(action.center.x + 6, action.y, (action.width - 12) * .5f, action.height), "DECLINE", false, 14)) duelManager.DeclineRematch();
                }
                else if(outgoing)
                {
                    GUI.enabled = GUI.enabled && duelManager.CanCancelRematch;
                    if(CommandGUI.DrawButton(action, duelManager.CanCancelRematch ? "CANCEL REQUEST" : "STARTING REMATCH…", false, 13)) duelManager.CancelRematch();
                }
                else if(CommandGUI.DrawButton(action, "REQUEST REMATCH", true, 14)) duelManager.RequestRematch();
            }
            finally { GUI.enabled = previouslyEnabled; }
            if(CommandGUI.DrawButton(new Rect(panel.x + 20, panel.y + 140, panel.width - 40, 36), "RETURN TO LOBBY", false, 12)) duelManager.LeaveDuel();
        }

        private void DrawLastMoments(float w, float h, Rect safe, float scale)
        {
            // Cover the full screen, including outside the safe area. The
            // image is frozen history; no current enemy state is drawn here.
            Rect fullScreen = new Rect(-safe.x / scale, -(Screen.height - safe.yMax) / scale,
                Screen.width / scale, Screen.height / scale);
            DrawRect(fullScreen, Color.black);
            Texture image = DuelKillReplay.PlaybackTexture;
            if (image != null) GUI.DrawTexture(fullScreen, image, ScaleMode.StretchToFill, false);

            Rect heading = new Rect(12, 12, Mathf.Min(460, w - 140), 58);
            CommandGUI.DrawPanel(heading, new Color(.015f, .025f, .035f, .95f), CommandGUI.AccentGold, 1f);
            DrawLabel(new Rect(heading.x + 12, heading.y + 6, heading.width - 24, 25), "LAST MOMENTS // YOUR VIEW", 15, CommandGUI.AccentGold, true);
            DrawLabel(new Rect(heading.x + 12, heading.y + 31, heading.width - 24, 19), "REPLAY // RECORDED BEFORE ELIMINATION", 10, Color.white);
            DrawRect(new Rect(heading.x, heading.yMax + 4, heading.width, 3), new Color(.03f, .05f, .06f, .9f));
            DrawRect(new Rect(heading.x, heading.yMax + 4, heading.width * DuelKillReplay.Progress, 3), CommandGUI.AccentGold);

            if (CommandGUI.DrawButton(new Rect(w - 122, 12, 110, 36), "SKIP REPLAY", false, 10)) DuelKillReplay.StopPlayback();
            DrawDamageRecap(new Rect((w - Mathf.Min(500, w - 32)) * .5f, h - 112, Mathf.Min(500, w - 32), 92));
        }

        private static void DrawDamageRecap(Rect panel)
        {
            DuelDamageRecap recap = DuelKillReplay.Recap;
            if (!recap.HasValue) return;
            CommandGUI.DrawPanel(panel, new Color(.025f, .025f, .035f, .96f), CommandGUI.AccentRed, 1f);
            DrawLabel(new Rect(panel.x + 12, panel.y + 7, panel.width - 24, 25), "ELIMINATED BY " + recap.Killer, 14, Color.white, true);
            DrawLabel(new Rect(panel.x + 12, panel.y + 34, panel.width - 24, 24), recap.Weapon + " // " + recap.BodyPart + " // " + recap.Distance.ToString("F0") + "m", 12, CommandGUI.AccentGold, true);
            DrawLabel(new Rect(panel.x + 12, panel.y + 60, panel.width - 24, 20), "FINAL HIT: " + recap.Damage.ToString("F0") + " DAMAGE // HOST CONFIRMED", 10, CommandGUI.Muted);
        }
    }
}
