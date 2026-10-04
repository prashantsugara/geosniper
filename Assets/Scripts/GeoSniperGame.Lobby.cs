using UnityEngine;

namespace GeoSniper
{
    public sealed partial class GeoSniperGame
    {
        static readonly Color LobbyInk = CommandGUI.ThemeBg;
        static readonly Color LobbyPanel = CommandGUI.ThemeCard;
        static readonly Color LobbyLine = CommandGUI.ThemeBorder;
        static readonly Color LobbyMuted = CommandGUI.Muted;
        static readonly Color LobbyWhite = CommandGUI.Text;
        static readonly Color LobbyAmber = CommandGUI.AccentGold;
        static readonly string[] LobbyModes = { "CAMPAIGN", "WORLD SECTOR", "GPS RANGE", "SNIPER DUEL" };
        static readonly LobbyTab[] LobbyFooterTabs = { LobbyTab.Home, LobbyTab.Armory, LobbyTab.Location, LobbyTab.Rewards };
        static readonly string[] LobbyFooterIcons = { "▲", "†", "◉", "★" };
        static readonly string[] LobbyFooterLabels = { "OVERVIEW", "ARMORY", "SECTOR", "REWARDS" };
        static readonly string[] LobbyWeapons = { "BARRETT .50", "M24 TACTICAL", "MK12 SPR" };
        static readonly string[] LobbyDescriptions = {
            "Work through tactical contracts. Find your target, plan the shot, and complete the objective.",
            "Deploy into a real-world city. Use your location or choose a sector on the map.",
            "Practice movement and ballistics against non-engaging patrol targets in a live GPS sector.",
            "A rooftop standoff against an AI marksman. Find the rival before they lock onto you."
        };
        GUIStyle lobbyText;
        Texture2D brandLogo;
        bool rotatingLobby;
        readonly System.Collections.Generic.List<Material> lobbyStageMaterials = new System.Collections.Generic.List<Material>();

        readonly System.Collections.Generic.List<Material> lobbyRifleMaterials = new System.Collections.Generic.List<Material>();

        void BuildLobbyBackdrop()
        {
            // Unlit architecture keeps the staging bay dark even when mission sunlight is present.
            var wall = new Material(Shader.Find("Unlit/Color")) { color = new Color(.075f, .095f, .105f) };
            var edge = new Material(Shader.Find("Unlit/Color")) { color = new Color(.038f, .05f, .057f) };
            var light = new Material(Shader.Find("Unlit/Color")) { color = LobbyAmber * .65f };
            lobbyStageMaterials.Add(wall); lobbyStageMaterials.Add(edge); lobbyStageMaterials.Add(light);
            LobbyBlock("Hangar floor", new Vector3(0, -.08f, 4), new Vector3(20, .1f, 14), edge);
            LobbyBlock("Hangar rear wall", new Vector3(0, 2, 5.8f), new Vector3(20, 6, .2f), wall);
            for (int i = -6; i <= 6; i++)
            {
                LobbyBlock("Wall seam", new Vector3(i * .9f, 2, 5.65f), new Vector3(.025f, 6, .1f), edge);
                if (i % 3 == 0) LobbyBlock("Warm strip light", new Vector3(i * .9f + .08f, 1.9f, 5.58f), new Vector3(.018f, 2.5f, .035f), light);
            }
            LobbyBlock("Rear wall cross rail", new Vector3(0, .45f, 5.5f), new Vector3(20, .055f, .13f), edge);
        }

        void LobbyBlock(string name, Vector3 position, Vector3 scale, Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(armoryRoom.transform, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
            var collider = block.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
        }

        void ReleaseLobbyStageMaterials()
        {
            foreach (var material in lobbyStageMaterials) if (material != null) Destroy(material);
            lobbyStageMaterials.Clear();
            foreach (var material in lobbyRifleMaterials) if (material != null) Destroy(material);
            lobbyRifleMaterials.Clear();
        }

        void OnDestroy()
        {
            ReleaseLobbyStageMaterials();
            if (armoryRoom != null) Destroy(armoryRoom);
            if (lobbyMusic != null) Destroy(lobbyMusic);
            lobbyMusicClip=null;
        }

        void LobbyFill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void LobbyLabel(Rect rect, string text, int size, Color color, bool bold = false,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            if (lobbyText == null) lobbyText = new GUIStyle(GUI.skin.label) { padding = new RectOffset(0, 0, 0, 0) };
            lobbyText.fontSize = size;
            lobbyText.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            lobbyText.normal.textColor = color;
            lobbyText.alignment = alignment;
            lobbyText.wordWrap = true;
            GUI.Label(rect, text, lobbyText);
        }

        bool LobbyButton(Rect rect, string text, bool active = false, bool primary = false)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            LobbyFill(rect, primary ? (hover ? new Color(1f, .77f, .4f) : LobbyAmber) :
                active ? new Color(.21f, .20f, .15f) : hover ? new Color(.14f, .17f, .18f) : LobbyPanel);
            LobbyFill(new Rect(rect.x, rect.yMax - 1, rect.width, 1), active ? LobbyAmber : LobbyLine);
            if (active && !primary) LobbyFill(new Rect(rect.x, rect.y, 3, rect.height), LobbyAmber);
            LobbyLabel(new Rect(rect.x + 14, rect.y, rect.width - 28, rect.height), text,
                primary ? 19 : 12, primary ? LobbyInk : active ? LobbyAmber : LobbyWhite, true);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        void DrawVignette(float w, float h, float topY, float botY)
        {
            int steps = 12;
            for (int i = 0; i < steps; i++)
            {
                float t = (float)(steps - i) / steps;
                float a = t * t * 0.65f;
                LobbyFill(new Rect(i * 4f, topY, 4f, botY - topY), new Color(0, 0, 0, a));
                LobbyFill(new Rect(w - (i + 1) * 4f, topY, 4f, botY - topY), new Color(0, 0, 0, a));
            }
        }

        void DrawMainMenu(float ignoredWidth, float ignoredHeight)
        {
            // ─── AAA Landscape Widescreen Lobby ───────────────────────────
            Matrix4x4 previous = GUI.matrix;
            var safe = Screen.safeArea;
            float scale = CommandGUI.GetCanvasScale(safe, out float w, out float h);
            GUI.matrix = Matrix4x4.TRS(new Vector3(safe.x, Screen.height - safe.yMax, 0),
                Quaternion.identity, new Vector3(scale, scale, 1));
            try
            {
                float navH = 62f;
                float topBarH = 56f;

                // Subtle cinematic edge vignettes (leaves 3D soldier bright & visible)
                for (int i = 0; i < 12; i++)
                {
                    float a = (float)(12 - i) / 12f * 0.65f;
                    LobbyFill(new Rect(0, i * 4.5f, w, 4.5f), new Color(0.01f, 0.015f, 0.02f, a));
                    LobbyFill(new Rect(0, h - navH - (12 - i) * 6f, w, 6f), new Color(0.01f, 0.015f, 0.02f, a * 0.85f));
                }
                DrawVignette(w, h, topBarH, h - navH);

                // Animated tactical scan line (subtle)
                float scanT = Mathf.Repeat(Time.realtimeSinceStartup * 0.20f, 1f);
                float scanY = topBarH + scanT * (h - navH - topBarH);
                LobbyFill(new Rect(0, scanY, w, 1), new Color(0.20f, 0.85f, 0.35f, 0.04f));

                // ── 1. TOP HUD BAR ──────────────────────────────────────────
                LobbyFill(new Rect(0, 0, w, 2f), CommandGUI.AccentCyan);
                LobbyFill(new Rect(0, topBarH - 1, w, 1), CommandGUI.ThemeBorder);

                // Operative Profile Pill (Top Left)
                int xp = Mathf.Max(0, PlayerPrefs.GetInt("GeoSniper.XP", 0));
                int level = xp / 1000 + 1;
                float xpFrac = (xp % 1000) / 1000f;
                string pName = PlayerPrefs.GetString("GeoSniper.PlayerName", "SPECTRE-01").ToUpperInvariant();

                Rect profPill = new Rect(16, 8, 300, 42);
                LobbyFill(profPill, CommandGUI.ThemeCard);
                LobbyFill(new Rect(profPill.x, profPill.y, 3, profPill.height), CommandGUI.AccentCyan);
                LobbyFill(new Rect(profPill.x, profPill.yMax - 1, profPill.width, 1), CommandGUI.ThemeBorder);

                // Level Badge
                Rect lvlBadge = new Rect(profPill.x + 8, profPill.y + 6, 30, 30);
                LobbyFill(lvlBadge, new Color(0.00f, 0.20f, 0.25f, 0.90f));
                LobbyFill(new Rect(lvlBadge.x, lvlBadge.y, lvlBadge.width, 1), CommandGUI.AccentCyan);
                LobbyFill(new Rect(lvlBadge.x, lvlBadge.yMax - 1, lvlBadge.width, 1), CommandGUI.AccentCyan);
                LobbyLabel(lvlBadge, level.ToString(), 13, CommandGUI.AccentCyan, true, TextAnchor.MiddleCenter);

                // Name & Rank
                LobbyLabel(new Rect(profPill.x + 46, profPill.y + 4, 240, 18), pName + " // TIER-1 MARKS MAN", 10, Color.white, true);

                // XP Bar
                Rect xpBarRect = new Rect(profPill.x + 46, profPill.y + 24, 160, 6);
                LobbyFill(xpBarRect, new Color(1, 1, 1, 0.08f));
                LobbyFill(new Rect(xpBarRect.x, xpBarRect.y, xpBarRect.width * xpFrac, xpBarRect.height), CommandGUI.AccentCyan);
                LobbyLabel(new Rect(profPill.x + 212, profPill.y + 20, 80, 14), (xp % 1000) + "/1000 XP", 8, CommandGUI.Muted);

                // Contract Stamina Battery (Center-Right)
                Rect staminaPill = new Rect(w - 530, 10, 120, 34);
                LobbyFill(staminaPill, CommandGUI.ThemeCard);
                LobbyFill(new Rect(staminaPill.x, staminaPill.yMax - 1, staminaPill.width, 1), CommandGUI.ThemeBorder);
                LobbyLabel(new Rect(staminaPill.x + 8, staminaPill.y + 4, 104, 14), "12/12 CONTRACTS", 8, CommandGUI.AccentGreen, true, TextAnchor.MiddleCenter);
                for (int b = 0; b < 6; b++)
                {
                    LobbyFill(new Rect(staminaPill.x + 10 + b * 17, staminaPill.y + 20, 13, 6), CommandGUI.AccentGreen);
                }

                // Top Right: Currencies & Controls
                int credits = PlayerPrefs.GetInt("GeoSniper.Credits", 0);
                int gold = PlayerPrefs.GetInt("GeoSniper.Gold", 0);

                bool isLive = LocationSelectionPolicy.RequiresLiveFix(PlayerPrefs.GetString("GeoSniper.LocationSource", ""));
                if (isLive)
                {
                    Rect livePill = new Rect(w - 650, 10, 110, 34);
                    LobbyFill(livePill, new Color(0.75f, 0.10f, 0.10f, 0.90f));
                    LobbyLabel(livePill, "● SATELLITE LIVE", 9, Color.white, true, TextAnchor.MiddleCenter);
                }

                // Credits Badge (Ballistic Amber)
                Rect credPill = new Rect(w - 395, 10, 130, 34);
                LobbyFill(credPill, CommandGUI.ThemeCard);
                LobbyFill(new Rect(credPill.x, credPill.y, 2, credPill.height), CommandGUI.AccentGold);
                LobbyFill(new Rect(credPill.x, credPill.yMax - 1, credPill.width, 1), CommandGUI.ThemeBorder);
                LobbyLabel(credPill, "  $" + credits.ToString("N0") + " Cr", 12, CommandGUI.AccentGold, true, TextAnchor.MiddleCenter);
                if (GUI.Button(credPill, GUIContent.none, GUIStyle.none)) SwitchTab(LobbyTab.Rewards);

                // Gold Badge
                Rect goldPill = new Rect(w - 250, 10, 105, 34);
                LobbyFill(goldPill, CommandGUI.ThemeCard);
                LobbyFill(new Rect(goldPill.x, goldPill.y, 2, goldPill.height), CommandGUI.AccentGold);
                LobbyFill(new Rect(goldPill.x, goldPill.yMax - 1, goldPill.width, 1), CommandGUI.ThemeBorder);
                LobbyLabel(goldPill, "  ◆ " + gold.ToString("N0"), 12, CommandGUI.AccentGold, true, TextAnchor.MiddleCenter);
                if (GUI.Button(goldPill, GUIContent.none, GUIStyle.none)) SwitchTab(LobbyTab.Rewards);

                // Settings Gear Button
                Rect settBtn = new Rect(w - 130, 10, 44, 34);
                LobbyFill(settBtn, CommandGUI.ThemeCard);
                LobbyFill(new Rect(settBtn.x, settBtn.yMax - 1, settBtn.width, 1), CommandGUI.ThemeBorder);
                LobbyLabel(settBtn, "⚙", 15, CommandGUI.Muted, false, TextAnchor.MiddleCenter);
                if (GUI.Button(settBtn, GUIContent.none, GUIStyle.none)) SwitchTab(LobbyTab.Settings);

                // ── 2. LEFT PANEL: LOADOUT & WEAPON TELEMETRY ───────────────
                int selectedWeapon = Mathf.Clamp(PlayerPrefs.GetInt("GeoSniper.SelectedWeapon", 1), 0, 2);
                string[] rifleNames = { "BARRETT M82A1 .50", "M24 TACTICAL SWS", "MK12 SPR DMR" };
                string[] rifleClasses = { "ANTI-MATERIEL // HIGH VELOCITY", "PRECISION BOLT-ACTION // SUB-MOA", "RAPID RECON MARKSMAN // DMR" };
                string[] rifleAmmoChips = { "ARMOR PIERCING .50 BMG (W-CORE)", ".338 LAPUA MAGNUM (MATCH)", "5.56x45mm NATO (OTM)" };

                Rect loadoutCard = new Rect(16, 70, 310, 260);
                LobbyFill(loadoutCard, CommandGUI.ThemeCard);
                LobbyFill(new Rect(loadoutCard.x, loadoutCard.y, 3, loadoutCard.height), CommandGUI.AccentCyan);
                LobbyFill(new Rect(loadoutCard.x, loadoutCard.y, loadoutCard.width, 1), new Color(CommandGUI.AccentCyan.r, CommandGUI.AccentCyan.g, CommandGUI.AccentCyan.b, 0.40f));
                LobbyFill(new Rect(loadoutCard.x, loadoutCard.yMax - 1, loadoutCard.width, 1), CommandGUI.ThemeBorder);

                LobbyLabel(new Rect(loadoutCard.x + 14, loadoutCard.y + 8, 160, 16), "// LOADOUT // PRIMARY CHASSIS", 8, CommandGUI.AccentCyan, true);
                // Tier Chip
                Rect tierChip = new Rect(loadoutCard.xMax - 110, loadoutCard.y + 6, 96, 18);
                LobbyFill(tierChip, new Color(0.15f, 0.10f, 0.02f, 0.90f));
                LobbyLabel(tierChip, "TIER V SPEC-ISSUE", 7, CommandGUI.AccentGold, true, TextAnchor.MiddleCenter);

                LobbyLabel(new Rect(loadoutCard.x + 14, loadoutCard.y + 26, 280, 22), rifleNames[selectedWeapon], 14, Color.white, true);
                LobbyLabel(new Rect(loadoutCard.x + 14, loadoutCard.y + 46, 280, 14), rifleClasses[selectedWeapon], 8, CommandGUI.Muted);

                // Caliber Badge Chip
                Rect ammoChip = new Rect(loadoutCard.x + 14, loadoutCard.y + 64, loadoutCard.width - 28, 20);
                LobbyFill(ammoChip, new Color(0.05f, 0.08f, 0.10f, 0.90f));
                LobbyFill(new Rect(ammoChip.x, ammoChip.y, 2, ammoChip.height), CommandGUI.AccentCyan);
                LobbyLabel(ammoChip, "  " + rifleAmmoChips[selectedWeapon], 8, CommandGUI.AccentCyan, true, TextAnchor.MiddleLeft);

                // Weapon Telemetry Stat Bars
                int dmgLvl = PlayerPrefs.GetInt("GeoSniper.WpnDmgLvl_" + selectedWeapon, 0);
                int scpLvl = PlayerPrefs.GetInt("GeoSniper.WpnScopeLvl_" + selectedWeapon, 0);
                int magLvl = PlayerPrefs.GetInt("GeoSniper.WpnMagLvl_" + selectedWeapon, 0);

                float statRowY = loadoutCard.y + 94;
                string[] statLabels = { "DAMAGE", "RANGE", "ACCURACY", "STABILITY" };
                float[] statValues = { 0.70f + dmgLvl * 0.12f, 0.65f + scpLvl * 0.14f, 0.85f, 0.60f + magLvl * 0.15f };
                for (int s = 0; s < 4; s++)
                {
                    LobbyLabel(new Rect(loadoutCard.x + 14, statRowY, 80, 14), statLabels[s], 8, CommandGUI.Muted, true);
                    Rect bRect = new Rect(loadoutCard.x + 95, statRowY + 3, 140, 6);
                    LobbyFill(bRect, new Color(1, 1, 1, 0.08f));
                    LobbyFill(new Rect(bRect.x, bRect.y, bRect.width * statValues[s], bRect.height), (s == 0 || s == 3) ? CommandGUI.AccentGold : CommandGUI.AccentCyan);
                    LobbyLabel(new Rect(loadoutCard.x + 242, statRowY, 50, 14), ((int)(statValues[s] * 100)).ToString(), 8, Color.white, true, TextAnchor.MiddleRight);
                    statRowY += 20;
                }

                // Upgrade / Armory Button
                Rect armoryBtn = new Rect(loadoutCard.x + 14, loadoutCard.y + 198, loadoutCard.width - 28, 44);
                if (CommandGUI.DrawButton(armoryBtn, "CUSTOMIZE IN ARMORY ⇲", false, 11))
                {
                    SwitchTab(LobbyTab.Armory);
                }

                // Headshot Bounty Vault Card (Below Loadout)
                int bountyStash = PlayerPrefs.GetInt("GeoSniper.BountyStash", 0);
                Rect bountyCard = new Rect(16, 342, 310, 120);
                LobbyFill(bountyCard, CommandGUI.ThemeCard);
                LobbyFill(new Rect(bountyCard.x, bountyCard.y, 3, bountyCard.height), CommandGUI.AccentGold);
                LobbyFill(new Rect(bountyCard.x, bountyCard.yMax - 1, bountyCard.width, 1), CommandGUI.ThemeBorder);

                LobbyLabel(new Rect(bountyCard.x + 14, bountyCard.y + 8, 270, 14), "// HEADSHOT BOUNTY VAULT //", 8, CommandGUI.AccentGold, true);
                LobbyLabel(new Rect(bountyCard.x + 14, bountyCard.y + 24, 270, 26), "$" + bountyStash.ToString("N0") + " Cr", 16, CommandGUI.AccentGold, true);
                LobbyLabel(new Rect(bountyCard.x + 14, bountyCard.y + 50, 270, 16), "Earned from precision elimination shots.", 8, CommandGUI.Muted);

                Rect claimVaultBtn = new Rect(bountyCard.x + 14, bountyCard.y + 72, bountyCard.width - 28, 36);
                if (CommandGUI.DrawButton(claimVaultBtn, bountyStash > 0 ? "CLAIM TO WAR CHEST ➔" : "SUPPLY VAULT", bountyStash > 0, 10))
                {
                    SwitchTab(LobbyTab.Rewards);
                }

                // ── 3. CENTER HERO 3D ROTATION ZONE ────────────────────────
                Rect rotZone = new Rect(340, topBarH, w - 740, h - navH - topBarH);

                // Tactical Corner Reticles framing operative
                float cx = rotZone.x + rotZone.width * 0.5f;
                float cy = rotZone.y + rotZone.height * 0.45f;
                LobbyFill(new Rect(cx - 40, cy, 25, 1), new Color(CommandGUI.AccentCyan.r, CommandGUI.AccentCyan.g, CommandGUI.AccentCyan.b, 0.45f));
                LobbyFill(new Rect(cx + 15, cy, 25, 1), new Color(CommandGUI.AccentCyan.r, CommandGUI.AccentCyan.g, CommandGUI.AccentCyan.b, 0.45f));
                LobbyFill(new Rect(cx, cy - 40, 1, 25), new Color(CommandGUI.AccentCyan.r, CommandGUI.AccentCyan.g, CommandGUI.AccentCyan.b, 0.45f));
                LobbyFill(new Rect(cx, cy + 15, 1, 25), new Color(CommandGUI.AccentCyan.r, CommandGUI.AccentCyan.g, CommandGUI.AccentCyan.b, 0.45f));
                LobbyFill(new Rect(cx - 2, cy - 2, 4, 4), CommandGUI.AccentCyan);

                // Rotation Drag Prompt
                Rect rotPrompt = new Rect(cx - 100, rotZone.yMax - 30, 200, 20);
                LobbyFill(rotPrompt, new Color(0.05f, 0.08f, 0.10f, 0.65f));
                LobbyLabel(rotPrompt, "[ DRAG 360° // INSPECT OPERATIVE ]", 8, CommandGUI.Muted, true, TextAnchor.MiddleCenter);

                // Drag to rotate soldier
                if (Event.current.type == EventType.MouseDown && GUI.enabled && rotZone.Contains(Event.current.mousePosition) && !loadFailed)
                { rotatingLobby = true; Event.current.Use(); }
                if (!GUI.enabled || Event.current.type == EventType.MouseUp) rotatingLobby = false;
                if (Event.current.type == EventType.MouseDrag && GUI.enabled && rotatingLobby)
                { lobbyYaw -= Event.current.delta.x * 0.6f; Event.current.Use(); }

                // ── 4. RIGHT PANEL: DEPLOYMENT CLUSTER ──────────────────────
                float rightW = 380f;
                float rightX = w - rightW - 20f;
                float rightY = topBarH + 16f;

                // ── A. CAMPAIGN OPERATIONS CARD ──
                float campH = 196f;
                Rect campCard = new Rect(rightX, rightY, rightW, campH);
                LobbyFill(campCard, CommandGUI.ThemeCard);
                LobbyFill(new Rect(campCard.x, campCard.y, 3, campCard.height), CommandGUI.AccentGold);
                LobbyFill(new Rect(campCard.x, campCard.y, campCard.width, 1), new Color(CommandGUI.AccentGold.r, CommandGUI.AccentGold.g, CommandGUI.AccentGold.b, 0.40f));
                LobbyFill(new Rect(campCard.x, campCard.yMax - 1, campCard.width, 1), CommandGUI.ThemeBorder);

                LobbyLabel(new Rect(campCard.x + 14, campCard.y + 8, 220, 14), "// CAMPAIGN // SINGLE PLAYER", 8, CommandGUI.AccentGold, true);

                // Threat Level badge
                Rect campThreatBadge = new Rect(campCard.xMax - 120, campCard.y + 6, 106, 18);
                LobbyFill(campThreatBadge, new Color(0.20f, 0.05f, 0.05f, 0.90f));
                LobbyLabel(campThreatBadge, "THREAT: CRITICAL", 8, CommandGUI.AccentRed, true, TextAnchor.MiddleCenter);

                string campTitle = "OPERATION ARCHON";
                string campSub = "SECTOR 7 // ELIMINATE SYNDICATE VIP";
                string campReward = "+$45,000 Cr   +120 XP";
                var cNode = CampaignNodeGraph.GetNode(GetDefaultSelectedNodeId());
                if (cNode != null)
                {
                    campTitle = cNode.codeName;
                    campSub = cNode.title + " // " + cNode.enemyCount + " HOSTILES";
                    campReward = "+$" + cNode.rewardCash.ToString("N0") + " Cr   +100 XP";
                }

                LobbyLabel(new Rect(campCard.x + 14, campCard.y + 26, rightW - 28, 22), campTitle, 15, Color.white, true);
                LobbyLabel(new Rect(campCard.x + 14, campCard.y + 48, rightW - 28, 16), campSub, 9, CommandGUI.AccentCyan, true);
                LobbyLabel(new Rect(campCard.x + 14, campCard.y + 66, rightW - 28, 16), "REWARD: " + campReward, 9, CommandGUI.AccentGold, true);

                // Dedicated PLAY CAMPAIGN Button
                Rect campPlayBtn = new Rect(campCard.x + 12, campCard.y + 92, campCard.width - 24, 54);
                if (CommandGUI.DrawGoldPlayButton(campPlayBtn, "PLAY CAMPAIGN  ▶"))
                {
                    PlayerPrefs.SetInt("GeoSniper.SelectedMode", 0);
                    PlayerPrefs.Save();
                    ExecuteDeploy();
                }

                // Mission Map & Briefing sub-action
                Rect campMapBtn = new Rect(campCard.x + 12, campPlayBtn.yMax + 8, campCard.width - 24, 32);
                if (CommandGUI.DrawButton(campMapBtn, "MISSION MAP & BRIEFING ➔", false, 9))
                {
                    PlayerPrefs.SetInt("GeoSniper.SelectedMode", 0);
                    PlayerPrefs.Save();
                    SwitchTab(LobbyTab.Campaign);
                }

                // ── B. 1v1 SNIPER DUEL CARD ──
                float duelH = 196f;
                Rect duelCard = new Rect(rightX, campCard.yMax + 12f, rightW, duelH);
                LobbyFill(duelCard, CommandGUI.ThemeCard);
                LobbyFill(new Rect(duelCard.x, duelCard.y, 3, duelCard.height), CommandGUI.AccentCyan);
                LobbyFill(new Rect(duelCard.x, duelCard.y, duelCard.width, 1), new Color(CommandGUI.AccentCyan.r, CommandGUI.AccentCyan.g, CommandGUI.AccentCyan.b, 0.40f));
                LobbyFill(new Rect(duelCard.x, duelCard.yMax - 1, duelCard.width, 1), CommandGUI.ThemeBorder);

                LobbyLabel(new Rect(duelCard.x + 14, duelCard.y + 8, 220, 14), "// MULTIPLAYER // 1v1 DUEL", 8, CommandGUI.AccentCyan, true);

                // P2P badge
                Rect duelBadge = new Rect(duelCard.xMax - 120, duelCard.y + 6, 106, 18);
                LobbyFill(duelBadge, new Color(0.04f, 0.16f, 0.18f, 0.90f));
                LobbyLabel(duelBadge, "P2P LIVE COMBAT", 8, CommandGUI.AccentCyan, true, TextAnchor.MiddleCenter);

                LobbyLabel(new Rect(duelCard.x + 14, duelCard.y + 26, rightW - 28, 22), "SNIPER DUEL 1v1", 15, Color.white, true);
                LobbyLabel(new Rect(duelCard.x + 14, duelCard.y + 48, rightW - 28, 16), "ROOFTOP STANDOFF // HOST OR JOIN ROOM", 9, CommandGUI.AccentCyan, true);
                LobbyLabel(new Rect(duelCard.x + 14, duelCard.y + 66, rightW - 28, 16), "REWARD: +$60,000 Cr   +200 XP", 9, CommandGUI.AccentGold, true);

                // Dedicated ENTER 1v1 DUEL Button
                Rect duelPlayBtn = new Rect(duelCard.x + 12, duelCard.y + 92, duelCard.width - 24, 54);
                if (CommandGUI.DrawGreenPlayButton(duelPlayBtn, "ENTER 1v1 DUEL  ⚔"))
                {
                    OpenDuelLobby();
                }

                // Duel Room & Practice sub-action
                Rect duelRoomBtn = new Rect(duelCard.x + 12, duelPlayBtn.yMax + 8, duelCard.width - 24, 32);
                if (CommandGUI.DrawButton(duelRoomBtn, "HOST / JOIN / PRACTICE BOT ➔", false, 9))
                {
                    OpenDuelLobby();
                }

                // ── C. QUICK UTILITY MODES STRIP ──
                float utilY = duelCard.yMax + 10f;
                float halfW = (rightW - 10f) * 0.5f;
                Rect worldSectorBtn = new Rect(rightX, utilY, halfW, 36f);
                Rect rangeBtn = new Rect(rightX + halfW + 10f, utilY, halfW, 36f);

                if (CommandGUI.DrawButton(worldSectorBtn, "🌐 GPS SECTOR", false, 9))
                {
                    PlayerPrefs.SetInt("GeoSniper.SelectedMode", 1);
                    PlayerPrefs.Save();
                    ExecuteDeploy();
                }

                if (CommandGUI.DrawButton(rangeBtn, "🎯 FIRING RANGE", false, 9))
                {
                    PlayerPrefs.SetInt("GeoSniper.SelectedMode", 2);
                    PlayerPrefs.Save();
                    ExecuteDeploy();
                }

                // Corner Mil-Spec Telemetry Stamps
                LobbyLabel(new Rect(16, h - navH - 24, 300, 16), "AZIMUTH 042° // LAT 35.6762° N // ELEV +184m", 7, CommandGUI.Muted);
                LobbyLabel(new Rect(w - 320, h - navH - 24, 300, 16), "SYS.STATUS: ONLINE // ENCRYPTED MIL-NET v4.9", 7, CommandGUI.Muted, false, TextAnchor.MiddleRight);

                // ── 5. LOAD FAILURE OVERLAY ─────────────────────────────────
                if (loadFailed)
                {
                    Rect failRect = new Rect(rightX, h - navH - 370, rightW, 90);
                    LobbyFill(failRect, new Color(0.12f, 0.02f, 0.02f, 0.96f));
                    LobbyFill(new Rect(failRect.x, failRect.y, 3, failRect.height), CommandGUI.AccentRed);
                    LobbyLabel(new Rect(failRect.x + 12, failRect.y + 6, failRect.width - 24, 30), "DEPLOYMENT FAILED / " + mapNotice, 11, CommandGUI.AccentGold);
                    if (CommandGUI.DrawButton(new Rect(failRect.x + 12, failRect.y + 44, failRect.width / 2 - 16, 34), "RETRY", true, 11)) RetryDeployment();
                    if (CommandGUI.DrawButton(new Rect(failRect.x + failRect.width / 2 + 4, failRect.y + 44, failRect.width / 2 - 16, 34), "OFFLINE OPS", false, 11)) StartOffline();
                }

                // ── 6. BOTTOM NAVIGATION BAR ────────────────────────────────
                int navClicked = CommandGUI.DrawNavBar(new Rect(0, h - navH, w, navH), LobbyFooterIcons, LobbyFooterLabels, 0);
                if (navClicked > 0 && navClicked < LobbyFooterTabs.Length) SwitchTab(LobbyFooterTabs[navClicked]);
            }
            finally { GUI.matrix = previous; }
        }

        void FrameLobbyOperator()
        {
            // AAA Landscape framing: Operative standing proudly in armory staging bay.
            // Soldier center is at Vector3(0.05f, 0.04f, 3.1f).
            float verticalSpan = 2.45f;
            view.fieldOfView = 36f;
            float depth = verticalSpan / (2f * Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * 0.5f));
            // Camera position: offset to center-left so soldier is framed between left dossier and right deploy cluster
            float camOffsetX = -0.25f;
            float yawRad = lobbyYaw * Mathf.Deg2Rad;
            float orbitRadius = 0.04f;
            view.transform.position = new Vector3(
                camOffsetX + Mathf.Sin(yawRad) * orbitRadius,
                1.15f,
                (3.1f - depth) + Mathf.Cos(yawRad) * orbitRadius);
            view.transform.LookAt(new Vector3(camOffsetX * 0.6f, 0.95f, 3.1f));
            view.backgroundColor = new Color(0.012f, 0.016f, 0.022f);
        }
    }
}
