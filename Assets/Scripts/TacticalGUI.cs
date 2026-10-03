using UnityEngine;

namespace GeoSniper
{
    public static class TacticalGUI
    {
        static Texture2D roundedBoxTex;
        static Texture2D lineTex;
        static Texture2D circleTex;
        static Texture2D ringTex;
        static Texture2D glowTex;

        public static readonly Color ThemeBg = new Color(0.067f, 0.078f, 0.090f, 0.95f);
        public static readonly Color ThemeCard = new Color(0.106f, 0.125f, 0.145f, 0.90f);
        public static readonly Color ThemeBorder = new Color(0.18f, 0.23f, 0.27f, 0.70f);
        public static readonly Color AccentCyan = new Color(0.00f, 0.94f, 1.00f, 1.00f);
        public static readonly Color AccentGold = new Color(1.00f, 0.60f, 0.00f, 1.00f);
        public static readonly Color AccentRed = new Color(0.95f, 0.20f, 0.20f, 1.00f);
        public static readonly Color AccentGreen = new Color(0.00f, 0.98f, 0.40f, 1.00f);

        static void InitTextures()
        {
            if (roundedBoxTex != null) return;

            int size = 32;
            roundedBoxTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];
            float r = 8f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Abs(x - size / 2f + 0.5f) - (size / 2f - r));
                    float dy = Mathf.Max(0, Mathf.Abs(y - size / 2f + 0.5f) - (size / 2f - r));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(r - dist + 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            roundedBoxTex.SetPixels(pixels);
            roundedBoxTex.Apply();

            lineTex = new Texture2D(1, 1);
            lineTex.SetPixel(0, 0, Color.white);
            lineTex.Apply();

            circleTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            Color[] cPixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32, 32));
                    float a = Mathf.Clamp01(32f - dist);
                    cPixels[y * 64 + x] = new Color(1, 1, 1, a);
                }
            }
            circleTex.SetPixels(cPixels);
            circleTex.Apply();

            ringTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            Color[] rPixels = new Color[64 * 64];
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32, 32));
                    float diff = Mathf.Abs(dist - 29f);
                    float a = Mathf.Clamp01(1.6f - diff);
                    rPixels[y * 64 + x] = new Color(1, 1, 1, a);
                }
            }
            ringTex.SetPixels(rPixels);
            ringTex.Apply();

            glowTex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            Color[] gPixels = new Color[32 * 32];
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16, 16)) / 16f;
                    float a = Mathf.Clamp01(1f - dist);
                    gPixels[y * 32 + x] = new Color(1, 1, 1, a * a);
                }
            }
            glowTex.SetPixels(gPixels);
            glowTex.Apply();
        }

        public static bool IsClicked(Rect rect)
        {
            // 1. Standard Unity GUI.Button captures native mouse clicks and touches reliably
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) return true;

            // 2. Direct event fallback for MouseUp release
            var e = Event.current;
            if (e != null && e.type == EventType.MouseUp && e.button == 0 && rect.Contains(e.mousePosition))
            {
                e.Use();
                return true;
            }

            return false;
        }

        public static void DrawPanel(Rect rect, Color bg, Color border, float borderWidth = 1.5f)
        {
            InitTextures();

            // Background panel
            GUI.color = bg;
            GUI.DrawTexture(rect, roundedBoxTex, ScaleMode.StretchToFill, true, 0, bg, 0, 8);

            // Border
            if (border.a > 0.01f && borderWidth > 0)
            {
                GUI.color = border;
                // Outer outline using lineTex
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, borderWidth), lineTex);
                GUI.DrawTexture(new Rect(rect.x, rect.yMax - borderWidth, rect.width, borderWidth), lineTex);
                GUI.DrawTexture(new Rect(rect.x, rect.y, borderWidth, rect.height), lineTex);
                GUI.DrawTexture(new Rect(rect.xMax - borderWidth, rect.y, borderWidth, rect.height), lineTex);
            }
            GUI.color = Color.white;
        }

        public static bool DrawButton(Rect rect, string text, bool isPrimary = false, int fontSize = 16)
        {
            InitTextures();
            bool hover = rect.Contains(Event.current.mousePosition);
            bool press = hover && Event.current.type == EventType.MouseDown;

            Color bg = isPrimary
                ? (hover ? new Color(0.0f, 0.70f, 0.85f, 0.95f) : new Color(0.0f, 0.55f, 0.72f, 0.90f))
                : (hover ? new Color(0.14f, 0.20f, 0.28f, 0.92f) : new Color(0.08f, 0.12f, 0.18f, 0.85f));

            Color border = isPrimary
                ? AccentCyan
                : (hover ? AccentCyan * 0.8f : ThemeBorder);

            DrawPanel(rect, bg, border, isPrimary ? 2f : 1.5f);

            var textColor = isPrimary ? Color.white : (hover ? AccentCyan : new Color(0.90f, 0.93f, 0.96f));
            GUI.Label(rect, text, GUIStyleCache.Get(fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, textColor, true));

            return IsClicked(rect);
        }

        public static void DrawBadge(Rect rect, string text, Color accentColor, int fontSize = 13)
        {
            Color bg = new Color(accentColor.r * 0.2f, accentColor.g * 0.2f, accentColor.b * 0.2f, 0.85f);
            DrawPanel(rect, bg, accentColor, 1f);

            GUI.Label(rect, text, GUIStyleCache.Get(fontSize, FontStyle.Bold, TextAnchor.MiddleCenter, accentColor, false));
        }

        public static void DrawLine(Vector2 pointA, Vector2 pointB, Color color, float width = 2f)
        {
            InitTextures();
            Color savedColor = GUI.color;
            Matrix4x4 savedMatrix = GUI.matrix;

            float angle = Mathf.Rad2Deg * Mathf.Atan2(pointB.y - pointA.y, pointB.x - pointA.x);
            float length = Vector2.Distance(pointA, pointB);

            GUI.color = color;
            GUIUtility.RotateAroundPivot(angle, pointA);
            GUI.DrawTexture(new Rect(pointA.x, pointA.y - width / 2f, length, width), lineTex);

            GUI.matrix = savedMatrix;
            GUI.color = savedColor;
        }

        public static void DrawProgressBar(Rect rect, float progress, Color fill, Color bg)
        {
            InitTextures();
            progress = Mathf.Clamp01(progress);

            // Background track
            DrawPanel(rect, bg, new Color(1, 1, 1, 0.1f), 1f);

            // Filled portion
            if (progress > 0.01f)
            {
                Rect fillRect = new Rect(rect.x + 2, rect.y + 2, (rect.width - 4) * progress, rect.height - 4);
                GUI.color = fill;
                GUI.DrawTexture(fillRect, lineTex);
                GUI.color = Color.white;
            }
        }

        public static void DrawCircle(Rect rect, Color color)
        {
            InitTextures();
            GUI.color = color;
            GUI.DrawTexture(rect, circleTex);
            GUI.color = Color.white;
        }

        public static void DrawRing(Rect rect, Color color)
        {
            InitTextures();
            GUI.color = color;
            GUI.DrawTexture(rect, ringTex);
            GUI.color = Color.white;
        }

        public static void DrawGlow(Rect rect, Color color)
        {
            InitTextures();
            GUI.color = color;
            GUI.DrawTexture(rect, glowTex);
            GUI.color = Color.white;
        }

        static Texture2D greenBtnTex;
        static Texture2D goldBtnTex;
        static Texture2D blueBtnTex;
        static Texture2D darkPillTex;

        static Texture2D MakeBoxTex(int w, int h, float r, Color top, Color bot, Color border)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float t = (float)y / (h - 1);
                Color c = Color.Lerp(bot, top, t);
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Abs(x - w / 2f + 0.5f) - (w / 2f - r));
                    float dy = Mathf.Max(0, Mathf.Abs(y - h / 2f + 0.5f) - (h / 2f - r));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    bool isBorder = d > (r - 1.6f) || x <= 1 || x >= w - 2 || y <= 1 || y >= h - 2;
                    Color finalC = isBorder ? Color.Lerp(c, border, 0.7f) : c;
                    finalC.a *= a;
                    px[y * w + x] = finalC;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        public static bool DrawGreenPlayButton(Rect rect, string text = "PLAY")
        {
            InitTextures();
            if (greenBtnTex == null)
            {
                greenBtnTex = MakeBoxTex(48, 48, 10f,
                    new Color(0.50f, 0.82f, 0.18f),
                    new Color(0.28f, 0.54f, 0.08f),
                    new Color(0.18f, 0.36f, 0.05f));
            }

            bool hover = rect.Contains(Event.current.mousePosition);
            GUI.color = hover ? new Color(1.15f, 1.15f, 1.15f, 1f) : Color.white;
            GUI.DrawTexture(rect, greenBtnTex);
            GUI.color = Color.white;

            var style = GUIStyleCache.Get(Mathf.RoundToInt(rect.height * 0.44f), FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);

            // Subtle drop shadow
            GUI.color = new Color(0, 0, 0, 0.4f);
            GUI.Label(new Rect(rect.x + 1, rect.y + 2, rect.width, rect.height), text, style);
            GUI.color = Color.white;
            GUI.Label(rect, text, style);

            return IsClicked(rect);
        }

        public static bool DrawGoldStoreButton(Rect rect, string text = "STORE")
        {
            InitTextures();
            if (goldBtnTex == null)
            {
                goldBtnTex = MakeBoxTex(48, 48, 8f,
                    new Color(0.96f, 0.76f, 0.28f),
                    new Color(0.78f, 0.52f, 0.12f),
                    new Color(0.48f, 0.30f, 0.06f));
            }

            bool hover = rect.Contains(Event.current.mousePosition);
            GUI.color = hover ? new Color(1.15f, 1.15f, 1.15f, 1f) : Color.white;
            GUI.DrawTexture(rect, goldBtnTex);
            GUI.color = Color.white;

            var style = GUIStyleCache.Get(Mathf.RoundToInt(rect.height * 0.40f), FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.22f, 0.14f, 0.02f));

            GUI.Label(rect, text, style);
            return IsClicked(rect);
        }

        public static bool DrawBlueSettingsButton(Rect rect)
        {
            InitTextures();
            if (blueBtnTex == null)
            {
                blueBtnTex = MakeBoxTex(36, 36, 6f,
                    new Color(0.08f, 0.65f, 0.95f),
                    new Color(0.02f, 0.44f, 0.72f),
                    new Color(0.01f, 0.25f, 0.45f));
            }

            bool hover = rect.Contains(Event.current.mousePosition);
            GUI.color = hover ? new Color(1.15f, 1.15f, 1.15f, 1f) : Color.white;
            GUI.DrawTexture(rect, blueBtnTex);
            GUI.color = Color.white;

            var style = GUIStyleCache.Get(Mathf.RoundToInt(rect.height * 0.52f), FontStyle.Normal, TextAnchor.MiddleCenter, Color.white);
            GUI.Label(rect, "⚙", style);
            return IsClicked(rect);
        }

        public static void DrawNotificationBadge(Vector2 center, string text = "1")
        {
            InitTextures();
            float radius = 9f;
            Rect r = new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f);
            GUI.color = new Color(0.95f, 0.15f, 0.15f, 1f);
            GUI.DrawTexture(r, circleTex);
            GUI.color = Color.white;

            var style = GUIStyleCache.Get(10, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            GUI.Label(r, text, style);
        }

        public static void DrawDarkPill(Rect rect, string icon, string value, Color iconColor)
        {
            InitTextures();
            if (darkPillTex == null)
            {
                darkPillTex = MakeBoxTex(48, 32, 14f,
                    new Color(0.12f, 0.18f, 0.12f, 0.92f),
                    new Color(0.06f, 0.10f, 0.06f, 0.92f),
                    new Color(0.30f, 0.55f, 0.15f, 0.85f));
            }

            GUI.DrawTexture(rect, darkPillTex);

            var iconStyle = GUIStyleCache.Get(Mathf.RoundToInt(rect.height * 0.50f), FontStyle.Bold, TextAnchor.MiddleCenter, iconColor);
            GUI.Label(new Rect(rect.x + 4, rect.y, 22, rect.height), icon, iconStyle);

            var valStyle = GUIStyleCache.Get(Mathf.RoundToInt(rect.height * 0.44f), FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            GUI.Label(new Rect(rect.x + 24, rect.y, rect.width - 28, rect.height), value, valStyle);
        }

        public static bool DrawDockButton(Rect rect, string icon, string text, bool isActive = false)
        {
            InitTextures();
            bool hover = rect.Contains(Event.current.mousePosition);
            Color bg = isActive 
                ? new Color(0.10f, 0.20f, 0.30f, 0.95f) 
                : hover 
                    ? new Color(0.09f, 0.14f, 0.20f, 0.92f) 
                    : new Color(0.05f, 0.08f, 0.12f, 0.88f);
            Color border = isActive ? AccentCyan : hover ? new Color(0.35f, 0.55f, 0.75f, 0.8f) : ThemeBorder;
            DrawPanel(rect, bg, border, isActive ? 2f : 1f);

            var iconStyle = GUIStyleCache.Get(Mathf.RoundToInt(rect.height * 0.38f), FontStyle.Normal, TextAnchor.MiddleCenter, isActive ? AccentCyan : Color.white);
            GUI.Label(new Rect(rect.x, rect.y + 4, rect.width, rect.height * 0.48f), icon, iconStyle);

            var textStyle = GUIStyleCache.Get(Mathf.RoundToInt(rect.height * 0.22f), FontStyle.Bold, TextAnchor.MiddleCenter, isActive ? AccentCyan : new Color(0.85f, 0.90f, 0.95f));
            GUI.Label(new Rect(rect.x, rect.y + rect.height * 0.48f, rect.width, rect.height * 0.44f), text, textStyle);

            return IsClicked(rect);
        }

        public static int DrawTabBar(Rect rect, string[] icons, string[] titles, int selectedIndex)
        {
            InitTextures();
            DrawPanel(rect, new Color(0.03f, 0.05f, 0.08f, 0.95f), new Color(0.14f, 0.20f, 0.28f, 0.85f), 1f);

            int count = Mathf.Min(icons.Length, titles.Length);
            float tabWidth = rect.width / count;
            int newSelected = selectedIndex;

            for (int i = 0; i < count; i++)
            {
                Rect tabRect = new Rect(rect.x + i * tabWidth, rect.y, tabWidth, rect.height);
                bool isSel = (i == selectedIndex);
                bool hover = tabRect.Contains(Event.current.mousePosition);

                if (isSel)
                {
                    // Active top indicator glow line
                    GUI.color = AccentCyan;
                    GUI.DrawTexture(new Rect(tabRect.x + 10, tabRect.y, tabRect.width - 20, 3f), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    DrawPanel(new Rect(tabRect.x + 2, tabRect.y + 3, tabRect.width - 4, tabRect.height - 4), new Color(0.08f, 0.16f, 0.24f, 0.75f), Color.clear, 0);
                }

                var iconStyle = GUIStyleCache.Get(Mathf.RoundToInt(rect.height * 0.32f), FontStyle.Normal, TextAnchor.MiddleCenter, isSel ? AccentCyan : hover ? Color.white : new Color(0.70f, 0.78f, 0.88f));
                GUI.Label(new Rect(tabRect.x, tabRect.y + 3f, tabRect.width, rect.height * 0.48f), icons[i], iconStyle);

                var titleStyle = GUIStyleCache.Get(Mathf.RoundToInt(rect.height * 0.20f), FontStyle.Bold, TextAnchor.MiddleCenter, isSel ? AccentCyan : hover ? Color.white : new Color(0.65f, 0.72f, 0.82f));
                GUI.Label(new Rect(tabRect.x, tabRect.y + rect.height * 0.48f, tabRect.width, rect.height * 0.44f), titles[i], titleStyle);

                if (IsClicked(tabRect))
                {
                    newSelected = i;
                }
            }

            return newSelected;
        }

        public static bool DrawModeCard(Rect rect, string icon, string title, string sub, string tag, Color tagColor, bool isSelected)
        {
            InitTextures();
            bool hover = rect.Contains(Event.current.mousePosition);

            Color bg = isSelected
                ? new Color(0.08f, 0.16f, 0.25f, 0.95f)
                : hover
                    ? new Color(0.07f, 0.11f, 0.17f, 0.90f)
                    : new Color(0.04f, 0.07f, 0.11f, 0.82f);

            Color border = isSelected ? AccentCyan : hover ? AccentCyan * 0.75f : ThemeBorder;
            DrawPanel(rect, bg, border, isSelected ? 2f : 1f);

            if (isSelected)
            {
                DrawGlow(new Rect(rect.x - 4, rect.y - 4, rect.width + 8, rect.height + 8), AccentCyan * 0.35f);
            }

            // Tag badge at top left
            DrawBadge(new Rect(rect.x + 8, rect.y + 8, 76, 16), tag, tagColor, 8);

            // Large Icon
            var iconStyle = GUIStyleCache.Get(24, FontStyle.Normal, TextAnchor.MiddleCenter, isSelected ? AccentCyan : Color.white);
            GUI.Label(new Rect(rect.x, rect.y + 26, rect.width, 32), icon, iconStyle);

            // Title
            var titleStyle = GUIStyleCache.Get(11, FontStyle.Bold, TextAnchor.MiddleCenter, isSelected ? Color.white : new Color(0.85f, 0.90f, 0.96f), true);
            GUI.Label(new Rect(rect.x + 4, rect.y + 60, rect.width - 8, 28), title, titleStyle);

            // Subtitle
            var subStyle = GUIStyleCache.Get(8, FontStyle.Normal, TextAnchor.MiddleCenter, isSelected ? AccentGold : new Color(0.60f, 0.72f, 0.82f), true);
            GUI.Label(new Rect(rect.x + 4, rect.y + 88, rect.width - 8, 22), sub, subStyle);

            return IsClicked(rect);
        }

        public static void DrawPipRating(Rect rect, int currentLevel, int maxLevel, Color activeColor)
        {
            InitTextures();
            float spacing = 3f;
            float pipWidth = (rect.width - spacing * (maxLevel - 1)) / maxLevel;
            for (int i = 0; i < maxLevel; i++)
            {
                Rect pipRect = new Rect(rect.x + i * (pipWidth + spacing), rect.y, pipWidth, rect.height);
                Color c = i < currentLevel ? activeColor : new Color(0.18f, 0.24f, 0.30f, 0.85f);
                DrawPanel(pipRect, c, i < currentLevel ? Color.Lerp(c, Color.white, 0.3f) : new Color(0.12f, 0.16f, 0.20f), 1f);
            }
        }

        public static Texture2D LoadTexture(string relativePath)
        {
            try
            {
                // 1. Resources.Load first (essential for Android APK and player builds)
                string resourceName = relativePath.Replace(".jpg", "").Replace(".png", "");
                if (resourceName.StartsWith("Assets/Resources/")) resourceName = resourceName.Substring("Assets/Resources/".Length);
                else if (resourceName.StartsWith("Resources/")) resourceName = resourceName.Substring("Resources/".Length);
                else if (resourceName.StartsWith("Assets/")) resourceName = resourceName.Substring("Assets/".Length);

                var resTex = Resources.Load<Texture2D>(resourceName);
                if (resTex != null)
                {
                    resTex.wrapMode = TextureWrapMode.Clamp;
                    return resTex;
                }

                string fileNameOnly = System.IO.Path.GetFileNameWithoutExtension(relativePath);
                var texInTextures = Resources.Load<Texture2D>("Textures/" + fileNameOnly);
                if (texInTextures != null)
                {
                    texInTextures.wrapMode = TextureWrapMode.Clamp;
                    return texInTextures;
                }

                // 2. Editor & PC filesystem fallback
                string path = System.IO.Path.Combine(Application.dataPath, relativePath);
                if (System.IO.File.Exists(path))
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(path);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes))
                    {
                        tex.wrapMode = TextureWrapMode.Clamp;
                        return tex;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("TacticalGUI.LoadTexture error: " + e.Message);
            }
            return null;
        }
    }
}
