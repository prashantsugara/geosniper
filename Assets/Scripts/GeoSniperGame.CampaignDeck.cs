using System;
using UnityEngine;

namespace GeoSniper
{
    public sealed partial class GeoSniperGame
    {
        private AudioSource briefingAudioSource;
        private int currentBriefingNodeId = -1;
        private static Texture2D[] cachedMissionThumbs;

        // Stage Carousel Swipe & Offset Animation State
        private static float campaignCarouselOffset = 0f;
        private static bool isDraggingCampaign = false;
        private static float dragStartX = 0f;
        private static float dragCurrentX = 0f;
        private static float dragStartTime = 0f;
        private static float touchStartX = 0f;
        private static float touchStartTime = 0f;
        private static bool touchSwiped = false;

        private static readonly string[] MissionLocations = new string[]
        {
            "ADRIATIC COAST // WATCHTOWER",
            "CENTRAL PLAZA // OLD TOWN",
            "INDUSTRIAL DEPOT // DOCKS",
            "REBEL OUTPOST // MOUNTAIN",
            "METRO SKYLINE // HELIPAD",
            "DESERT CANYON // CHECKPOINT",
            "PORT TERMINAL // WAREHOUSE",
            "TRANSIT CORRIDOR // OVERPASS",
            "PERIMETER BUNKER // BORDER",
            "NAVAL SHIPYARD // DRY DOCK",
            "BLACKSITE COMPOUND // SECTOR 7",
            "APEX EMBASSY // ROOFTOP SUMMIT"
        };

        private static readonly string[] MissionTimes = new string[]
        {
            "2:15 MIN", "3:00 MIN", "2:45 MIN", "3:30 MIN",
            "2:30 MIN", "4:00 MIN", "3:15 MIN", "2:50 MIN",
            "3:40 MIN", "4:15 MIN", "4:30 MIN", "5:00 MIN"
        };

        private static AudioClip LoadWavFromFile(string filePath)
        {
            if (!System.IO.File.Exists(filePath)) return null;
            try
            {
                byte[] bytes = System.IO.File.ReadAllBytes(filePath);
                int pos = 12;
                while (pos < bytes.Length - 8)
                {
                    string chunkId = System.Text.Encoding.ASCII.GetString(bytes, pos, 4);
                    int chunkSize = System.BitConverter.ToInt32(bytes, pos + 4);
                    if (chunkId == "data")
                    {
                        pos += 8;
                        int sampleCount = chunkSize / 2;
                        float[] samples = new float[sampleCount];
                        for (int i = 0; i < sampleCount; i++)
                        {
                            short val = System.BitConverter.ToInt16(bytes, pos + i * 2);
                            samples[i] = val / 32768f;
                        }
                        var clip = AudioClip.Create(System.IO.Path.GetFileNameWithoutExtension(filePath), sampleCount, 1, 44100, false);
                        clip.SetData(samples, 0);
                        return clip;
                    }
                    pos += 8 + chunkSize;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[CampaignDeck] LoadWavFromFile: " + ex.Message);
            }
            return null;
        }

        void EnsureMissionThumbnails()
        {
            if (cachedMissionThumbs != null && cachedMissionThumbs.Length == 12 && cachedMissionThumbs[0] != null) return;
            cachedMissionThumbs = new Texture2D[12];
            for (int i = 0; i < 12; i++)
            {
                cachedMissionThumbs[i] = CommandGUI.LoadTexture("Resources/Textures/Missions/Mission_" + i + ".jpg")
                    ?? CommandGUI.LoadTexture("Textures/Missions/Mission_" + i)
                    ?? CommandGUI.LoadTexture("Resources/Textures/Missions/coastal_tower.jpg")
                    ?? CommandGUI.LoadTexture("Textures/EventCampaign")
                    ?? CommandGUI.LoadTexture("Textures/LobbyBackground");
            }
        }

        public void PlayBriefingAudio(int nodeId, bool forceRestart = false)
        {
            if (briefingAudioSource == null)
            {
                briefingAudioSource = gameObject.AddComponent<AudioSource>();
                briefingAudioSource.playOnAwake = false;
                briefingAudioSource.loop = false;
                briefingAudioSource.spatialBlend = 0f;
            }

            if (FindAnyObjectByType<AudioListener>() == null)
            {
                if (view != null) view.gameObject.AddComponent<AudioListener>();
                else gameObject.AddComponent<AudioListener>();
            }

            int clipIndex = Mathf.Clamp(nodeId, 0, 11);
            if (!forceRestart && currentBriefingNodeId == clipIndex && briefingAudioSource.isPlaying)
            {
                return;
            }

            currentBriefingNodeId = clipIndex;
            var clip = Resources.Load<AudioClip>("Audio/Briefings/Briefing_" + clipIndex);
            if (clip == null)
            {
                string wavPath = System.IO.Path.Combine(Application.dataPath, "Resources", "Audio", "Briefings", "Briefing_" + clipIndex + ".wav");
                clip = LoadWavFromFile(wavPath);
            }
            if (clip != null)
            {
                briefingAudioSource.clip = clip;
                float master = PlayerPrefs.GetFloat("GeoSniper.MasterVolume", 1f);
                float sfx = PlayerPrefs.GetFloat("GeoSniper.SFXVolume", 1f);
                briefingAudioSource.volume = Mathf.Clamp01(master * sfx * 0.95f);
                briefingAudioSource.Play();
            }
        }

        public void StopBriefingAudio()
        {
            if (briefingAudioSource != null && briefingAudioSource.isPlaying)
            {
                briefingAudioSource.Stop();
            }
        }

        public void ToggleBriefingAudio(int nodeId)
        {
            if (briefingAudioSource != null && briefingAudioSource.isPlaying)
            {
                briefingAudioSource.Stop();
            }
            else
            {
                PlayBriefingAudio(nodeId, true);
            }
        }

        void DrawBackgroundStageCard(Rect r, int nodeId, bool isLeft)
        {
            var node = CampaignNodeGraph.GetNode(nodeId);
            if (node == null) return;

            bool isUnlocked = CampaignNodeGraph.IsNodeUnlocked(node.id);
            bool isCompleted = CampaignNodeGraph.IsNodeCompleted(node.id);

            // Card Panel Backing - Translucent war-room look recessed in background
            Color bgCol = new Color(0.015f, 0.022f, 0.018f, 0.88f);
            Color borderCol = isUnlocked ? new Color(0.24f, 0.34f, 0.28f, 0.70f) : new Color(0.18f, 0.16f, 0.16f, 0.60f);

            LobbyFill(r, bgCol);
            CommandGUI.DrawPanel(r, bgCol, borderCol, 1.2f);

            // Subtle Corner Brackets
            float bLen = 10f;
            float bThk = 1.5f;
            Color bCol = isUnlocked ? new Color(0.35f, 0.50f, 0.40f, 0.6f) : new Color(0.30f, 0.25f, 0.25f, 0.4f);
            LobbyFill(new Rect(r.x, r.y, bLen, bThk), bCol);
            LobbyFill(new Rect(r.x, r.y, bThk, bLen), bCol);
            LobbyFill(new Rect(r.xMax - bLen, r.y, bLen, bThk), bCol);
            LobbyFill(new Rect(r.xMax - bThk, r.y, bThk, bLen), bCol);
            LobbyFill(new Rect(r.x, r.yMax - bThk, bLen, bThk), bCol);
            LobbyFill(new Rect(r.x, r.yMax - bLen, bThk, bLen), bCol);
            LobbyFill(new Rect(r.xMax - bLen, r.yMax - bThk, bLen, bThk), bCol);
            LobbyFill(new Rect(r.xMax - bThk, r.yMax - bLen, bThk, bLen), bCol);

            float pad = 12f;
            float innerX = r.x + pad;
            float innerW = r.width - pad * 2f;
            float cy = r.y + 10f;

            // Stage Direction Tag
            string dirTag = isLeft ? "◀ PREVIOUS STAGE" : "NEXT STAGE ▶";
            LobbyLabel(new Rect(innerX, cy, innerW, 14), dirTag, 9, CommandGUI.AccentGold, true);
            cy += 16f;

            // Mission Number & Title
            string missionNum = "MISSION " + (node.id + 1);
            LobbyLabel(new Rect(innerX, cy, innerW, 14), missionNum, 10, isUnlocked ? CommandGUI.AccentGold : CommandGUI.Muted, true);
            cy += 15f;

            string mTitle = node.title.ToUpperInvariant();
            LobbyLabel(new Rect(innerX, cy, innerW, 20), mTitle, 13, isUnlocked ? Color.white : CommandGUI.Muted, true);
            cy += 22f;

            // Location
            string loc = (node.id >= 0 && node.id < MissionLocations.Length) ? MissionLocations[node.id] : "SECTOR // CLASSIFIED";
            LobbyLabel(new Rect(innerX, cy, innerW, 14), loc, 8, CommandGUI.Muted, false);
            cy += 18f;

            // Thumbnail Image
            float thumbH = Mathf.Clamp(r.height * 0.40f, 90f, 150f);
            Rect thumbR = new Rect(innerX, cy, innerW, thumbH);
            Texture2D thumb = (cachedMissionThumbs != null && node.id >= 0 && node.id < cachedMissionThumbs.Length) 
                ? cachedMissionThumbs[node.id] : null;

            if (thumb != null)
            {
                var prevC = GUI.color;
                GUI.color = isUnlocked ? new Color(0.70f, 0.75f, 0.72f, 0.85f) : new Color(0.35f, 0.35f, 0.35f, 0.50f);
                GUI.DrawTexture(thumbR, thumb, ScaleMode.ScaleAndCrop);
                GUI.color = prevC;
            }
            else
            {
                LobbyFill(thumbR, new Color(0.04f, 0.06f, 0.05f, 0.95f));
            }
            CommandGUI.DrawPanel(thumbR, Color.clear, new Color(0.20f, 0.28f, 0.22f, 0.70f), 1f);

            // Subdued radar crosshair on background card thumbnail
            Vector2 thumbCenter = new Vector2(thumbR.x + thumbR.width * 0.5f, thumbR.y + thumbR.height * 0.5f);
            CommandGUI.DrawLine(new Vector2(thumbCenter.x - 22, thumbCenter.y), new Vector2(thumbCenter.x + 22, thumbCenter.y), new Color(0.30f, 0.85f, 0.40f, 0.25f), 1f);
            CommandGUI.DrawLine(new Vector2(thumbCenter.x, thumbCenter.y - 22), new Vector2(thumbCenter.x, thumbCenter.y + 22), new Color(0.30f, 0.85f, 0.40f, 0.25f), 1f);

            cy += thumbH + 10f;

            // Status Badge
            string statusStr = isCompleted ? "☑ COMPLETED" : (isUnlocked ? "◉ DEPLOYABLE" : "🔒 LOCKED");
            Color statusCol = isCompleted ? new Color(0.35f, 0.88f, 0.45f) : (isUnlocked ? CommandGUI.AccentGold : new Color(0.85f, 0.30f, 0.30f));
            LobbyLabel(new Rect(innerX, cy, innerW, 16), statusStr, 10, statusCol, true);
            cy += 18f;

            // Intel Stars
            int stars = PlayerPrefs.GetInt("GeoSniper.NodeStars_" + node.id, 0);
            string starStr = stars >= 3 ? "★★★" : (stars == 2 ? "★★☆" : (stars == 1 ? "★☆☆" : "☆☆☆"));
            LobbyLabel(new Rect(innerX, cy, innerW, 16), starStr, 11, isCompleted ? CommandGUI.AccentGold : CommandGUI.Muted, true);

            // Tap prompt at bottom
            string tapPrompt = isLeft ? "[ TAP TO VIEW PREV ]" : "[ TAP TO VIEW NEXT ]";
            LobbyLabel(new Rect(innerX, r.yMax - 24, innerW, 16), tapPrompt, 9, new Color(0.45f, 0.60f, 0.50f, 0.7f), true, TextAnchor.MiddleCenter);

            // Dark vignette overlay on the background card to push it into distance
            LobbyFill(r, new Color(0.01f, 0.02f, 0.015f, 0.22f));

            // Whole card is a big button to select it
            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                selectedNodeId = node.id;
                campaignCarouselOffset = isLeft ? -130f : 130f;
                PlayBriefingAudio(selectedNodeId);
            }
        }

        void DrawCampaignScreen(float w, float h)
        {
            EnsureMissionThumbnails();

            // Tactical atmospheric dark war-room backdrop
            Texture2D bgTex = lobbyBgTex ?? CommandGUI.LoadTexture("Textures/LobbyBackground");
            if (bgTex != null)
            {
                var prevC = GUI.color;
                GUI.color = new Color(0.18f, 0.24f, 0.22f, 0.35f);
                GUI.DrawTexture(new Rect(0, 0, w, h), bgTex, ScaleMode.ScaleAndCrop);
                GUI.color = prevC;
            }
            else
            {
                LobbyFill(new Rect(0, 0, w, h), new Color(0.015f, 0.020f, 0.016f, 0.98f));
            }

            // Top and bottom atmospheric gradient vignettes
            LobbyFill(new Rect(0, 0, w, 100), new Color(0.01f, 0.015f, 0.012f, 0.82f));
            LobbyFill(new Rect(0, h - 110, w, 110), new Color(0.01f, 0.015f, 0.012f, 0.88f));

            // Subtle topographic radar grid lines in background
            float cX = w * 0.5f;
            Color gridLineCol = new Color(0.20f, 0.85f, 0.35f, 0.055f);
            for (float gy = 40; gy < h; gy += 48)
                CommandGUI.DrawLine(new Vector2(0, gy), new Vector2(w, gy), gridLineCol, 1f);
            for (float gx = 40; gx < w; gx += 64)
                CommandGUI.DrawLine(new Vector2(gx, 0), new Vector2(gx, h), gridLineCol, 1f);

            // Subtle concentric radar rings centered on active card
            Color radarCol = new Color(0.25f, 0.85f, 0.35f, 0.08f);
            CommandGUI.DrawRing(new Rect(cX - 280, h * 0.48f - 280, 560, 560), radarCol);
            CommandGUI.DrawRing(new Rect(cX - 140, h * 0.48f - 140, 280, 280), radarCol);

            int totalNodes = CampaignNodeGraph.Nodes != null ? CampaignNodeGraph.Nodes.Length : 12;
            if (selectedNodeId < 0 || selectedNodeId >= totalNodes)
            {
                selectedNodeId = Mathf.Clamp(GetDefaultSelectedNodeId(), 0, totalNodes - 1);
            }

            var sel = CampaignNodeGraph.GetNode(selectedNodeId) ?? CampaignNodeGraph.Nodes[0];
            int cleared = CampaignNodeGraph.GetCompletedCount();

            // Smooth animation spring/lerp for carousel transitions
            if (!isDraggingCampaign)
            {
                campaignCarouselOffset = Mathf.Lerp(campaignCarouselOffset, 0f, Time.deltaTime * 16f);
                if (Mathf.Abs(campaignCarouselOffset) < 0.5f) campaignCarouselOffset = 0f;
            }

            // ── TOUCH & MOUSE SWIPE GESTURE HANDLING ──
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    touchStartX = touch.position.x;
                    touchStartTime = Time.realtimeSinceStartup;
                    touchSwiped = false;
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    float dX = touch.position.x - touchStartX;
                    float dt = Mathf.Max(0.01f, Time.realtimeSinceStartup - touchStartTime);
                    if (Mathf.Abs(dX) > 40f && dt < 0.6f && !touchSwiped)
                    {
                        touchSwiped = true;
                        if (dX < -40f && selectedNodeId < totalNodes - 1)
                        {
                            selectedNodeId++;
                            campaignCarouselOffset = 130f;
                            PlayBriefingAudio(selectedNodeId);
                        }
                        else if (dX > 40f && selectedNodeId > 0)
                        {
                            selectedNodeId--;
                            campaignCarouselOffset = -130f;
                            PlayBriefingAudio(selectedNodeId);
                        }
                    }
                }
            }

            // Mouse Drag / Keyboard / Wheel Navigation
            var ev = Event.current;
            float topH = 70f;
            float bottomH = 74f;
            if (ev != null)
            {
                if (ev.type == EventType.MouseDown && ev.button == 0 && ev.mousePosition.y > topH && ev.mousePosition.y < h - bottomH)
                {
                    isDraggingCampaign = true;
                    dragStartX = ev.mousePosition.x;
                    dragCurrentX = ev.mousePosition.x;
                    dragStartTime = Time.realtimeSinceStartup;
                }
                else if (ev.type == EventType.MouseDrag && isDraggingCampaign)
                {
                    dragCurrentX = ev.mousePosition.x;
                }
                else if (ev.type == EventType.MouseUp && isDraggingCampaign)
                {
                    float dX = dragCurrentX - dragStartX;
                    float dt = Mathf.Max(0.01f, Time.realtimeSinceStartup - dragStartTime);
                    isDraggingCampaign = false;
                    if (dX < -45f && selectedNodeId < totalNodes - 1)
                    {
                        selectedNodeId++;
                        campaignCarouselOffset = 130f;
                        PlayBriefingAudio(selectedNodeId);
                    }
                    else if (dX > 45f && selectedNodeId > 0)
                    {
                        selectedNodeId--;
                        campaignCarouselOffset = -130f;
                        PlayBriefingAudio(selectedNodeId);
                    }
                }
                else if (ev.type == EventType.ScrollWheel)
                {
                    if (ev.delta.y > 0.1f && selectedNodeId < totalNodes - 1)
                    {
                        selectedNodeId++;
                        campaignCarouselOffset = 110f;
                        PlayBriefingAudio(selectedNodeId);
                        ev.Use();
                    }
                    else if (ev.delta.y < -0.1f && selectedNodeId > 0)
                    {
                        selectedNodeId--;
                        campaignCarouselOffset = -110f;
                        PlayBriefingAudio(selectedNodeId);
                        ev.Use();
                    }
                }
                else if (ev.type == EventType.KeyDown)
                {
                    if ((ev.keyCode == KeyCode.DownArrow || ev.keyCode == KeyCode.RightArrow || ev.keyCode == KeyCode.D) && selectedNodeId < totalNodes - 1)
                    {
                        selectedNodeId++;
                        campaignCarouselOffset = 120f;
                        PlayBriefingAudio(selectedNodeId);
                        ev.Use();
                    }
                    else if ((ev.keyCode == KeyCode.UpArrow || ev.keyCode == KeyCode.LeftArrow || ev.keyCode == KeyCode.A) && selectedNodeId > 0)
                    {
                        selectedNodeId--;
                        campaignCarouselOffset = -120f;
                        PlayBriefingAudio(selectedNodeId);
                        ev.Use();
                    }
                }
            }

            // ── TOP HEADER STRIP (Matches reference: SILENT ECHOES: CAMPAIGN / SELECT MISSION) ──
            LobbyFill(new Rect(0, 0, w, topH), new Color(0.018f, 0.026f, 0.020f, 0.96f));
            LobbyFill(new Rect(0, topH - 1, w, 1), CommandGUI.ThemeBorder);

            // Title
            LobbyLabel(new Rect(28, 8, 420, 26), "GEOSNIPER // CAMPAIGN", 19, Color.white, true);

            // Subtitle
            string subText = "SELECT MISSION (" + cleared + "/" + totalNodes + " COMPLETE)";
            LobbyLabel(new Rect(28, 34, 340, 18), subText, 11, CommandGUI.Muted, true);

            // Progress bar directly below subtitle
            float barW = 320f;
            float barH = 5f;
            float barY = 54f;
            float pct = Mathf.Clamp01((float)cleared / Mathf.Max(1, totalNodes));
            LobbyFill(new Rect(28, barY, barW, barH), new Color(0.08f, 0.11f, 0.09f, 1f));
            LobbyFill(new Rect(28, barY, barW * pct, barH), CommandGUI.AccentGold);
            if (pct > 0.01f)
            {
                CommandGUI.DrawGlow(new Rect(28 + barW * pct - 4, barY - 2, 8, 8), CommandGUI.AccentGold);
            }

            // Top Right: LOADOUT Button + HOME Button
            Rect loadoutBtn = new Rect(w - 210, 16, 110, 36);
            LobbyFill(loadoutBtn, new Color(0.05f, 0.07f, 0.06f, 0.95f));
            CommandGUI.DrawPanel(loadoutBtn, new Color(0.05f, 0.07f, 0.06f, 0.95f), CommandGUI.ThemeBorder, 1.2f);
            LobbyLabel(loadoutBtn, "LOADOUT", 11, Color.white, true, TextAnchor.MiddleCenter);
            if (GUI.Button(loadoutBtn, GUIContent.none, GUIStyle.none))
            {
                StopBriefingAudio();
                SwitchTab(LobbyTab.Armory);
            }

            Rect homeBtn = new Rect(w - 88, 16, 72, 36);
            LobbyFill(homeBtn, new Color(0.12f, 0.04f, 0.04f, 0.90f));
            CommandGUI.DrawPanel(homeBtn, new Color(0.12f, 0.04f, 0.04f, 0.90f), new Color(0.85f, 0.25f, 0.25f, 0.6f), 1f);
            LobbyLabel(homeBtn, "✕ LOBBY", 10, Color.white, true, TextAnchor.MiddleCenter);
            if (GUI.Button(homeBtn, GUIContent.none, GUIStyle.none))
            {
                StopBriefingAudio();
                SwitchTab(LobbyTab.Home);
            }

            // ── HORIZONTAL CAROUSEL LAYOUT ────────────────────────────────
            float deckH = h - topH - bottomH;
            float cardW = 460f;
            float cardH = Mathf.Clamp(deckH - 8f, 400f, 495f);
            float centerBaseX = (w - cardW) / 2f;
            float cardY = topH + (deckH - cardH) / 2f;

            float sideW = Mathf.Clamp(centerBaseX - 20f, 200f, 270f);
            float sideH = cardH * 0.90f;
            float sideY = cardY + (cardH - sideH) * 0.5f;

            float dragOffset = isDraggingCampaign ? (dragCurrentX - dragStartX) : 0f;
            float animOffset = campaignCarouselOffset + dragOffset;
            float cardX = centerBaseX + animOffset;

            // ── LEFT BACKGROUND CARD (PREVIOUS STAGE) ─────────────────────
            if (selectedNodeId > 0)
            {
                Rect leftCardRect = new Rect(cardX - sideW - 14f, sideY, sideW, sideH);
                DrawBackgroundStageCard(leftCardRect, selectedNodeId - 1, true);
            }

            // ── RIGHT BACKGROUND CARD (NEXT STAGE) ────────────────────────
            if (selectedNodeId < totalNodes - 1)
            {
                Rect rightCardRect = new Rect(cardX + cardW + 14f, sideY, sideW, sideH);
                DrawBackgroundStageCard(rightCardRect, selectedNodeId + 1, false);
            }

            // ── NAVIGATION ARROWS (Floating Badges on Card Edges) ──────────
            if (selectedNodeId > 0)
            {
                Rect leftArrow = new Rect(cardX - 20, cardY + cardH * 0.46f, 38, 38);
                LobbyFill(leftArrow, new Color(0.02f, 0.035f, 0.025f, 0.95f));
                CommandGUI.DrawPanel(leftArrow, new Color(0.02f, 0.035f, 0.025f, 0.95f), CommandGUI.AccentGold, 1.2f);
                LobbyLabel(leftArrow, "◀", 16, CommandGUI.AccentGold, true, TextAnchor.MiddleCenter);
                if (GUI.Button(leftArrow, GUIContent.none, GUIStyle.none))
                {
                    selectedNodeId--;
                    campaignCarouselOffset = -130f;
                    PlayBriefingAudio(selectedNodeId);
                }
            }

            if (selectedNodeId < totalNodes - 1)
            {
                Rect rightArrow = new Rect(cardX + cardW - 18, cardY + cardH * 0.46f, 38, 38);
                LobbyFill(rightArrow, new Color(0.02f, 0.035f, 0.025f, 0.95f));
                CommandGUI.DrawPanel(rightArrow, new Color(0.02f, 0.035f, 0.025f, 0.95f), CommandGUI.AccentGold, 1.2f);
                LobbyLabel(rightArrow, "▶", 16, CommandGUI.AccentGold, true, TextAnchor.MiddleCenter);
                if (GUI.Button(rightArrow, GUIContent.none, GUIStyle.none))
                {
                    selectedNodeId++;
                    campaignCarouselOffset = 130f;
                    PlayBriefingAudio(selectedNodeId);
                }
            }

            // ── ACTIVE CENTER MISSION CARD (Glowing Gold Border) ───────────
            Rect cardRect = new Rect(cardX, cardY, cardW, cardH);

            // Subtle gold ambient glow around active card
            CommandGUI.DrawGlow(cardRect, new Color(0.95f, 0.72f, 0.12f, 0.22f));

            // Card panel backing
            LobbyFill(cardRect, new Color(0.022f, 0.032f, 0.026f, 0.98f));
            CommandGUI.DrawPanel(cardRect, new Color(0.022f, 0.032f, 0.026f, 0.98f), CommandGUI.AccentGold, 2f);

            // Tactical corner reticles on the card
            float bracketLen = 16f;
            float bracketThick = 2.5f;
            Color bracketCol = CommandGUI.AccentGold;
            // TL
            LobbyFill(new Rect(cardRect.x, cardRect.y, bracketLen, bracketThick), bracketCol);
            LobbyFill(new Rect(cardRect.x, cardRect.y, bracketThick, bracketLen), bracketCol);
            // TR
            LobbyFill(new Rect(cardRect.xMax - bracketLen, cardRect.y, bracketLen, bracketThick), bracketCol);
            LobbyFill(new Rect(cardRect.xMax - bracketThick, cardRect.y, bracketThick, bracketLen), bracketCol);
            // BL
            LobbyFill(new Rect(cardRect.x, cardRect.yMax - bracketThick, bracketLen, bracketThick), bracketCol);
            LobbyFill(new Rect(cardRect.x, cardRect.yMax - bracketLen, bracketThick, bracketLen), bracketCol);
            // BR
            LobbyFill(new Rect(cardRect.xMax - bracketLen, cardRect.yMax - bracketThick, bracketLen, bracketThick), bracketCol);
            LobbyFill(new Rect(cardRect.xMax - bracketThick, cardRect.yMax - bracketLen, bracketThick, bracketLen), bracketCol);

            // Inside Card Header
            float innerX = cardX + 16f;
            float innerW = cardW - 32f;
            float curY = cardY + 12f;

            // Mission Number
            LobbyLabel(new Rect(innerX, curY, innerW, 16), "MISSION " + (sel.id + 1) + ":", 12, CommandGUI.AccentGold, true);
            curY += 18f;

            // Mission Title
            LobbyLabel(new Rect(innerX, curY, innerW, 26), sel.title.ToUpperInvariant(), 18, Color.white, true);
            curY += 28f;

            // Metadata Lines: LOCATION / EST. TIME / OBJECTIVE
            string locName = MissionLocations[sel.id % MissionLocations.Length];
            string estTime = MissionTimes[sel.id % MissionTimes.Length];

            LobbyLabel(new Rect(innerX, curY, innerW, 16), "LOCATION: " + locName, 10, new Color(0.72f, 0.80f, 0.75f), true);
            curY += 16f;

            LobbyLabel(new Rect(innerX, curY, innerW, 16), "EST. TIME: " + estTime + "   |   THREAT: " + sel.enemyCount + " HOSTILES", 10, new Color(0.72f, 0.80f, 0.75f), true);
            curY += 16f;

            string objText = string.IsNullOrEmpty(sel.briefing) ? "NEUTRALIZE ALL MARKED HOSTILE TARGETS" : sel.briefing;
            if (objText.Length > 75) objText = objText.Substring(0, 72) + "...";
            LobbyLabel(new Rect(innerX, curY, innerW, 16), "OBJECTIVE: " + objText.ToUpperInvariant(), 10, CommandGUI.AccentGold, true);
            curY += 20f;

            // ── CENTRAL THUMBNAIL / RETICLE SCENERY ─────────────────────────
            float thumbH = 200f;
            Rect thumbRect = new Rect(innerX, curY, innerW, thumbH);

            Texture2D thumbTex = (cachedMissionThumbs != null && sel.id < cachedMissionThumbs.Length)
                ? cachedMissionThumbs[sel.id % 12] : null;

            var prevGuiCol = GUI.color;
            GUI.color = Color.white;
            if (thumbTex != null)
            {
                GUI.DrawTexture(thumbRect, thumbTex, ScaleMode.ScaleAndCrop);
            }
            else
            {
                LobbyFill(thumbRect, new Color(0.04f, 0.06f, 0.05f));
            }
            GUI.color = prevGuiCol;

            // Dark vignette top & bottom on thumbnail
            LobbyFill(new Rect(thumbRect.x, thumbRect.y, thumbRect.width, 24), new Color(0.01f, 0.01f, 0.01f, 0.35f));
            LobbyFill(new Rect(thumbRect.x, thumbRect.yMax - 38, thumbRect.width, 38), new Color(0.01f, 0.01f, 0.01f, 0.82f));
            CommandGUI.DrawPanel(thumbRect, Color.clear, new Color(0.20f, 0.30f, 0.22f, 0.6f), 1f);

            // Tactical Sniper Scope Reticle Overlay on thumbnail
            Vector2 tCenter = thumbRect.center;
            Color reticleCol = new Color(0.25f, 0.95f, 0.40f, 0.65f);

            // Outer scope ring
            CommandGUI.DrawRing(new Rect(tCenter.x - 36, tCenter.y - 36, 72, 72), new Color(0.25f, 0.95f, 0.40f, 0.65f));
            CommandGUI.DrawRing(new Rect(tCenter.x - 16, tCenter.y - 16, 32, 32), new Color(0.25f, 0.95f, 0.40f, 0.35f));

            // Crosshair hairlines with center gap
            CommandGUI.DrawLine(new Vector2(tCenter.x - 54, tCenter.y), new Vector2(tCenter.x - 6, tCenter.y), reticleCol, 1.2f);
            CommandGUI.DrawLine(new Vector2(tCenter.x + 6, tCenter.y), new Vector2(tCenter.x + 54, tCenter.y), reticleCol, 1.2f);
            CommandGUI.DrawLine(new Vector2(tCenter.x, tCenter.y - 54), new Vector2(tCenter.x, tCenter.y - 6), reticleCol, 1.2f);
            CommandGUI.DrawLine(new Vector2(tCenter.x, tCenter.y + 6), new Vector2(tCenter.x, tCenter.y + 54), reticleCol, 1.2f);

            // Tick marks
            for (int tm = -2; tm <= 2; tm++)
            {
                if (tm == 0) continue;
                float tx = tCenter.x + tm * 16f;
                float ty = tCenter.y + tm * 16f;
                CommandGUI.DrawLine(new Vector2(tx, tCenter.y - 4), new Vector2(tx, tCenter.y + 4), reticleCol, 1f);
                CommandGUI.DrawLine(new Vector2(tCenter.x - 4, ty), new Vector2(tCenter.x + 4, ty), reticleCol, 1f);
            }

            // Tactical telemetry labels on the scope
            LobbyLabel(new Rect(thumbRect.x + 8, thumbRect.y + 6, 120, 16), "RANGE: " + (120 + sel.id * 35) + "M", 9, new Color(0.25f, 0.95f, 0.40f, 0.85f), true);
            LobbyLabel(new Rect(thumbRect.xMax - 110, thumbRect.y + 6, 102, 16), "WIND: 2.4 M/S ◀", 9, new Color(0.25f, 0.95f, 0.40f, 0.85f), true, TextAnchor.MiddleRight);

            // ── AUDIO BRIEFING / RADIO COMMS BAR ──────────────────────────
            Rect commsBar = new Rect(thumbRect.x, thumbRect.yMax - 34, thumbRect.width, 34);
            LobbyFill(commsBar, new Color(0.02f, 0.035f, 0.025f, 0.92f));
            LobbyFill(new Rect(commsBar.x, commsBar.y, commsBar.width, 1), CommandGUI.AccentGold);

            bool isVoicePlaying = briefingAudioSource != null && briefingAudioSource.isPlaying;
            string speakerIcon = isVoicePlaying ? "🔊" : "🔈";
            LobbyLabel(new Rect(commsBar.x + 8, commsBar.y + 6, 22, 22), speakerIcon, 13, CommandGUI.AccentGold, false);

            string commsLabel = isVoicePlaying ? "TACTICAL RADIO COMMS // OFFICER BRIEFING" : "RADIO COMMS // READY";
            LobbyLabel(new Rect(commsBar.x + 32, commsBar.y + 8, 250, 18), commsLabel, 9, Color.white, true);

            // Live Equalizer Animated Waveform
            float eqX = commsBar.xMax - 155f;
            float eqY = commsBar.y + 8f;
            for (int k = 0; k < 7; k++)
            {
                float eqH = isVoicePlaying ? (Mathf.PingPong(Time.realtimeSinceStartup * (6f + k * 1.5f) + k * 0.8f, 1f) * 14f + 3f) : 3f;
                Color eqCol = isVoicePlaying ? CommandGUI.AccentGold : CommandGUI.Muted;
                LobbyFill(new Rect(eqX + k * 6f, eqY + 16f - eqH, 3f, eqH), eqCol);
            }

            // Audio Play/Pause Button
            Rect audioBtn = new Rect(commsBar.xMax - 82, commsBar.y + 5, 76, 24);
            LobbyFill(audioBtn, new Color(0.06f, 0.09f, 0.07f, 0.90f));
            CommandGUI.DrawPanel(audioBtn, new Color(0.06f, 0.09f, 0.07f, 0.90f), CommandGUI.AccentGold, 1f);
            string audioBtnLabel = isVoicePlaying ? "❚❚ PAUSE" : "▶ PLAY";
            LobbyLabel(audioBtn, audioBtnLabel, 9, CommandGUI.AccentGold, true, TextAnchor.MiddleCenter);
            if (GUI.Button(audioBtn, GUIContent.none, GUIStyle.none))
            {
                ToggleBriefingAudio(sel.id);
            }

            curY += thumbH + 12f;

            // ── DIFFICULTY SELECTOR (Easy / Normal / Hard) ─────────────────
            // 3 circular pill selectors matching the reference image!
            var curDiff = DifficultyProfile.Selected;
            float diffW = innerW / 3f;

            (DifficultyLevel level, string label, string icon)[] diffOpts = new[]
            {
                (DifficultyLevel.Casual, "easy", "🙂"),
                (DifficultyLevel.Standard, "Normal", "👤"),
                (DifficultyLevel.Hardcore, "Hard", "🔥")
            };

            for (int d = 0; d < 3; d++)
            {
                var opt = diffOpts[d];
                bool isSel = (curDiff == opt.level);
                float colCenterX = innerX + d * diffW + diffW * 0.5f;

                // Circular icon button
                float circD = 38f;
                Rect circRect = new Rect(colCenterX - circD * 0.5f, curY, circD, circD);

                Color circBg = isSel ? new Color(0.20f, 0.16f, 0.04f, 0.95f) : new Color(0.04f, 0.06f, 0.05f, 0.95f);
                Color circBorder = isSel ? CommandGUI.AccentGold : new Color(0.22f, 0.30f, 0.24f);

                if (isSel)
                {
                    CommandGUI.DrawGlow(circRect, new Color(CommandGUI.AccentGold.r, CommandGUI.AccentGold.g, CommandGUI.AccentGold.b, 0.35f));
                }
                LobbyFill(circRect, circBg);
                CommandGUI.DrawCircle(circRect, circBorder);

                LobbyLabel(circRect, opt.icon, 16, isSel ? CommandGUI.AccentGold : CommandGUI.Muted, false, TextAnchor.MiddleCenter);

                // Label below circle
                Rect lblRect = new Rect(innerX + d * diffW, curY + circD + 3, diffW, 16);
                Color lblCol = isSel ? CommandGUI.AccentGold : CommandGUI.Muted;
                LobbyLabel(lblRect, opt.label, 10, lblCol, isSel, TextAnchor.MiddleCenter);

                // Click area covering circle and label
                Rect clickArea = new Rect(innerX + d * diffW + 10, curY, diffW - 20, circD + 20);
                if (GUI.Button(clickArea, GUIContent.none, GUIStyle.none))
                {
                    DifficultyProfile.Selected = opt.level;
                }
            }

            curY += 60f;

            // ── CHALLENGES & INTEL FOOTER ─────────────────────────────────
            Rect challStrip = new Rect(innerX, curY, innerW, 36f);
            LobbyFill(challStrip, new Color(0.02f, 0.03f, 0.024f, 0.90f));
            LobbyFill(new Rect(challStrip.x, challStrip.y, challStrip.width, 1), CommandGUI.ThemeBorder);

            LobbyLabel(new Rect(challStrip.x + 8, challStrip.y + 4, 85, 14), "CHALLENGES:", 8, CommandGUI.Muted, true);
            string challList = "☑ Silent Assassin    ⯐ Long Shot";
            LobbyLabel(new Rect(challStrip.x + 8, challStrip.y + 18, challStrip.width - 90, 16), challList, 10, Color.white, true);

            // Intel Rating / Stars
            int nodeStars = PlayerPrefs.GetInt("GeoSniper.NodeStars_" + sel.id, 0);
            string starStr = nodeStars >= 3 ? "★★★" : nodeStars == 2 ? "★★☆" : nodeStars == 1 ? "★☆☆" : "☆☆☆";
            LobbyLabel(new Rect(challStrip.xMax - 80, challStrip.y + 4, 72, 14), "INTEL: " + Mathf.Max(1, nodeStars) + "/3", 9, CommandGUI.Muted, true, TextAnchor.MiddleRight);
            LobbyLabel(new Rect(challStrip.xMax - 80, challStrip.y + 18, 72, 16), starStr, 11, CommandGUI.AccentGold, true, TextAnchor.MiddleRight);

            // ── BOTTOM ACTION BAR (CO-OP LOBBY / START MISSION / OPTIONS) ──
            LobbyFill(new Rect(0, h - bottomH, w, bottomH), new Color(0.016f, 0.024f, 0.018f, 0.98f));
            LobbyFill(new Rect(0, h - bottomH, w, 1), CommandGUI.ThemeBorder);

            // CO-OP LOBBY BUTTON (Left)
            Rect coopBtn = new Rect(centerBaseX, h - 60, 130, 48);
            LobbyFill(coopBtn, new Color(0.04f, 0.06f, 0.05f, 0.95f));
            CommandGUI.DrawPanel(coopBtn, new Color(0.04f, 0.06f, 0.05f, 0.95f), CommandGUI.ThemeBorder, 1.2f);
            LobbyLabel(coopBtn, "👥  CO-OP LOBBY", 11, Color.white, true, TextAnchor.MiddleCenter);
            if (GUI.Button(coopBtn, GUIContent.none, GUIStyle.none))
            {
                StopBriefingAudio();
                SwitchTab(LobbyTab.Location);
            }

            // START MISSION (Giant Glowing Green Play Button in Center)
            Rect startBtn = new Rect(centerBaseX + 145, h - 62, cardW - 290, 52);
            bool isUnlocked = CampaignNodeGraph.IsNodeUnlocked(sel.id);
            GUI.enabled = isUnlocked;

            string deployTitle = isUnlocked ? "START MISSION" : "🔒 LOCKED";
            if (CommandGUI.DrawGreenPlayButton(startBtn, deployTitle) && isUnlocked)
            {
                StopBriefingAudio();
                choosingStage = false;
                PlayerPrefs.SetInt("GeoSniper.ActiveNodeId", sel.id);
                rangeToStart = false;
                campaignNodeToStart = sel.id;
                if (sel.isPvPDuel) { isPvPDuel = true; stageIndexToStart = -1; }
                else { isPvPDuel = false; stageIndexToStart = sel.stageIndex; }
                StartPreferredLocationMission();
            }
            GUI.enabled = true;

            // OPTIONS BUTTON (Right)
            Rect optBtn = new Rect(centerBaseX + cardW - 130, h - 60, 130, 48);
            LobbyFill(optBtn, new Color(0.04f, 0.06f, 0.05f, 0.95f));
            CommandGUI.DrawPanel(optBtn, new Color(0.04f, 0.06f, 0.05f, 0.95f), CommandGUI.ThemeBorder, 1.2f);
            LobbyLabel(optBtn, "⚙  OPTIONS", 11, Color.white, true, TextAnchor.MiddleCenter);
            if (GUI.Button(optBtn, GUIContent.none, GUIStyle.none))
            {
                StopBriefingAudio();
                SwitchTab(LobbyTab.Settings);
            }
        }
    }
}
