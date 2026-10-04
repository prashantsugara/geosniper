using UnityEngine;

namespace GeoSniper
{
    // Menu-only design system. Combat HUD styling is deliberately independent.
    public static class CommandGUI
    {
        // Tactical Ballistics HUD palette — Stitch-designed military optronics
        public static readonly Color ThemeBg    = new Color(0.067f, 0.078f, 0.090f, 0.96f);   // deep ballistic carbon #111417
        public static readonly Color ThemeCard  = new Color(0.106f, 0.125f, 0.145f, 0.92f);   // dark titanium panel #1b2025
        public static readonly Color ThemeBorder= new Color(0.18f, 0.23f, 0.27f, 0.70f);      // titanium frame border #2e3b45
        public static readonly Color AccentGold = new Color(1.00f, 0.60f, 0.00f);             // ballistic amber #FF9900
        public static readonly Color AccentCyan = new Color(0.00f, 0.94f, 1.00f);             // optic cyan #00F0FF
        public static readonly Color AccentGreen= new Color(0.00f, 0.98f, 0.40f);             // NVG phosphor green #00FA64
        public static readonly Color AccentRed  = new Color(0.95f, 0.20f, 0.20f);             // alert red #F23333
        public static readonly Color Text       = new Color(0.88f, 0.90f, 0.92f);             // near-white #E1E6EB
        public static readonly Color Muted      = new Color(0.52f, 0.58f, 0.62f);             // muted steel #84949E

        // Responsive widescreen canvas scale helper:
        // Guarantees width >= 1200 and height >= 720 in landscape.
        public static float GetCanvasScale(Rect safe, out float w, out float h)
        {
            if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Screen.width, Screen.height);
            bool isPortrait = safe.width < safe.height;
            float scale = isPortrait
                ? Mathf.Max(0.01f, Mathf.Min(safe.width / 720f, safe.height / 1100f))
                : Mathf.Max(0.01f, Mathf.Min(safe.width / 1200f, safe.height / 720f));
            w = safe.width / scale;
            h = safe.height / scale;
            return scale;
        }
        public static void Fill(Rect r, Color c)
        { var saved=GUI.color; GUI.color=c; GUI.DrawTexture(r,Texture2D.whiteTexture); GUI.color=saved; }
        public static bool IsClicked(Rect r) => GUI.Button(r,GUIContent.none,GUIStyle.none);
        static void Label(Rect r,string text,int size,Color color)
        {
            GUI.Label(r,text,GUIStyleCache.Get(size,FontStyle.Bold,TextAnchor.MiddleCenter,color,true));
        }
        public static void DrawPanel(Rect r,Color bg,Color border,float borderWidth=1.5f)
        {
            Fill(r,borderWidth<=0?ThemeBg:ThemeCard);
            if(borderWidth>0 && border.a>.01f)
                Fill(new Rect(r.x,r.yMax-1,r.width,1),ThemeBorder);
        }
        public static bool DrawButton(Rect r,string text,bool isPrimary=false,int fontSize=16)
        {
            bool hover=r.Contains(Event.current.mousePosition) && GUI.enabled;
            Color bg = isPrimary
                ? (hover ? new Color(1.0f, 0.70f, 0.20f) : AccentGold)
                : (hover ? new Color(0.14f, 0.18f, 0.22f) : ThemeCard);
            Fill(r, bg);
            // 1px frame border
            Color bCol = isPrimary ? AccentGold : (hover ? AccentCyan : ThemeBorder);
            Fill(new Rect(r.x, r.y, r.width, 1), bCol);
            Fill(new Rect(r.x, r.yMax - 1, r.width, 1), bCol);
            Fill(new Rect(r.x, r.y, 1, r.height), bCol);
            Fill(new Rect(r.xMax - 1, r.y, 1, r.height), bCol);
            if (isPrimary || hover)
            {
                Fill(new Rect(r.x, r.y, 4, 2), isPrimary ? ThemeBg : AccentCyan);
                Fill(new Rect(r.xMax - 4, r.yMax - 2, 4, 2), isPrimary ? ThemeBg : AccentCyan);
            }
            Color textCol = isPrimary ? ThemeBg : (hover ? Color.white : (GUI.enabled ? Text : Muted));
            Label(new Rect(r.x+6,r.y,r.width-12,r.height),text,fontSize,textCol);
            return IsClicked(r);
        }
        public static bool DrawGreenPlayButton(Rect r, string text = "DEPLOY")
        {
            bool hover = r.Contains(Event.current.mousePosition) && GUI.enabled;
            float pulse = Mathf.PingPong(Time.realtimeSinceStartup * 2.5f, 1f);
            Color glow = Color.Lerp(new Color(0.00f, 0.70f, 0.85f, 0.16f), new Color(0.00f, 0.94f, 1.00f, 0.35f), pulse);
            DrawGlow(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), glow);

            Color bg = hover ? new Color(0.05f, 0.14f, 0.18f, 0.98f) : new Color(0.03f, 0.08f, 0.12f, 0.96f);
            Fill(r, bg);

            Color border = Color.Lerp(new Color(0.00f, 0.75f, 0.90f), AccentCyan, pulse);
            Fill(new Rect(r.x, r.y, r.width, 2), border);
            Fill(new Rect(r.x, r.yMax - 2, r.width, 2), border);
            Fill(new Rect(r.x, r.y, 3, r.height), border);
            Fill(new Rect(r.xMax - 3, r.y, 3, r.height), border);

            // Chamfer corner accents (tactical optronic ticks)
            Fill(new Rect(r.x + 6, r.y + 4, 8, 2), AccentGold);
            Fill(new Rect(r.xMax - 14, r.yMax - 6, 8, 2), AccentGold);

            int fSize = Mathf.Clamp((int)(r.height * 0.34f), 12, 20);
            GUI.Label(r, text, GUIStyleCache.Get(fSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, true));
            return IsClicked(r);
        }
        public static bool DrawGoldPlayButton(Rect r, string text = "DEPLOY")
        {
            bool hover = r.Contains(Event.current.mousePosition) && GUI.enabled;
            float pulse = Mathf.PingPong(Time.realtimeSinceStartup * 2.5f, 1f);
            Color glow = Color.Lerp(new Color(0.85f, 0.65f, 0.10f, 0.16f), new Color(1.00f, 0.80f, 0.20f, 0.35f), pulse);
            DrawGlow(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), glow);

            Color bg = hover ? new Color(0.18f, 0.14f, 0.04f, 0.98f) : new Color(0.12f, 0.09f, 0.02f, 0.96f);
            Fill(r, bg);

            Color border = Color.Lerp(new Color(0.85f, 0.65f, 0.10f), AccentGold, pulse);
            Fill(new Rect(r.x, r.y, r.width, 2), border);
            Fill(new Rect(r.x, r.yMax - 2, r.width, 2), border);
            Fill(new Rect(r.x, r.y, 3, r.height), border);
            Fill(new Rect(r.xMax - 3, r.y, 3, r.height), border);

            // Chamfer corner accents
            Fill(new Rect(r.x + 6, r.y + 4, 8, 2), AccentCyan);
            Fill(new Rect(r.xMax - 14, r.yMax - 6, 8, 2), AccentCyan);

            int fSize = Mathf.Clamp((int)(r.height * 0.34f), 12, 20);
            GUI.Label(r, text, GUIStyleCache.Get(fSize, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, true));
            return IsClicked(r);
        }
        public static bool DrawGoldStoreButton(Rect r,string text="STORE") => DrawGreenPlayButton(r,text);
        public static void DrawBadge(Rect r,string text,Color accent,int fontSize=13)
        { Fill(r,new Color(.18f,.17f,.13f)); Label(r,text,fontSize,AccentGold); }
        public static void DrawDarkPill(Rect r,string icon,string value,Color color)
        { Fill(r,ThemeBg); Label(r,icon+" "+value,12,Text); }
        public static void DrawProgressBar(Rect r,float progress,Color fill,Color bg)
        { Fill(r,ThemeBorder); Fill(new Rect(r.x,r.y,r.width*Mathf.Clamp01(progress),r.height),AccentGold); }
        public static void DrawPipRating(Rect r,int current,int max,Color color)
        {
            float width=(r.width-3*(max-1))/Mathf.Max(1,max);
            for(int i=0;i<max;i++) Fill(new Rect(r.x+i*(width+3),r.y,width,r.height),i<current?AccentGold:ThemeBorder);
        }
        static readonly Texture2D[] diagonalLines = new Texture2D[4];
        public static void DrawLine(Vector2 a,Vector2 b,Color color,float width=2)
        {
            // Axis-aligned texture rectangles respect nested IMGUI scroll clipping. Rotating
            // GUI.matrix inside a scroll view can rotate the clip and leak lines over navigation.
            float dx=b.x-a.x,dy=b.y-a.y;
            if(Mathf.Abs(dy)<.01f) { Fill(new Rect(Mathf.Min(a.x,b.x),a.y-width/2,Mathf.Abs(dx),width),color); return; }
            if(Mathf.Abs(dx)<.01f) { Fill(new Rect(a.x-width/2,Mathf.Min(a.y,b.y),width,Mathf.Abs(dy)),color); return; }
            int index=(dx*dy>0?1:0)+(width>2?2:0);
            if(diagonalLines[index]==null)
            {
                const int size=128;
                var texture=new Texture2D(size,size,TextureFormat.RGBA32,false) {wrapMode=TextureWrapMode.Clamp};
                var pixels=new Color[size*size];
                for(int y=0;y<size;y++) for(int x=0;x<size;x++)
                {
                    float lineY=(index%2==1)?size-1-x:x;
                    pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01((index>=2?2.2f:1.2f)-Mathf.Abs(y-lineY)));
                }
                texture.SetPixels(pixels);texture.Apply(false,true);diagonalLines[index]=texture;
            }
            var saved=GUI.color;GUI.color=color;
            GUI.DrawTexture(new Rect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Abs(dx),Mathf.Abs(dy)),diagonalLines[index]);
            GUI.color=saved;
        }
        static GUIStyle sliderThumb;
        public static float HorizontalSlider(Rect r,float value,float min,float max)
        {
            Fill(new Rect(r.x,r.y+6,r.width,3),ThemeBorder);
            Fill(new Rect(r.x,r.y+6,r.width*Mathf.InverseLerp(min,max,value),3),AccentGold);
            if(sliderThumb==null) sliderThumb=new GUIStyle {fixedWidth=12,fixedHeight=16,
                normal={background=Texture2D.whiteTexture},hover={background=Texture2D.whiteTexture},active={background=Texture2D.whiteTexture}};
            var saved=GUI.color; GUI.color=AccentGold;
            try { return GUI.HorizontalSlider(r,value,min,max,GUIStyle.none,sliderThumb); }
            finally { GUI.color=saved; }
        }
        public static void DrawCircle(Rect r,Color color) => TacticalGUI.DrawCircle(r,color);
        public static void DrawRing(Rect r,Color color) => TacticalGUI.DrawRing(r,color);
        public static void DrawGlow(Rect r,Color color)
        {
            // Soft multi-layer glow: 3 expanding rectangles with decreasing alpha
            var saved=GUI.color;
            for(int layer=0;layer<3;layer++)
            {
                float exp=layer*4f;
                GUI.color=new Color(color.r,color.g,color.b,color.a*(0.4f-layer*0.12f));
                GUI.DrawTexture(new Rect(r.x-exp,r.y-exp,r.width+exp*2,r.height+exp*2),Texture2D.whiteTexture);
            }
            GUI.color=saved;
        }
        // Real bottom navigation bar — 5 tabs with icon + label
        // Returns the index of the tab clicked, or -1 if none clicked.
        public static int DrawNavBar(Rect r, string[] icons, string[] labels, int selected)
        {
            Fill(r, ThemeBg);
            // Top separator line
            Fill(new Rect(r.x, r.y, r.width, 1.5f), ThemeBorder);
            int clicked = -1;
            float tabW = r.width / labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                Rect tab = new Rect(r.x + i * tabW, r.y, tabW, r.height);
                bool active = i == selected;
                if (active)
                {
                    Fill(new Rect(tab.x + 8, tab.y, tab.width - 16, 2.5f), AccentCyan);
                    Fill(new Rect(tab.x, tab.y, tab.width, tab.height), new Color(AccentCyan.r, AccentCyan.g, AccentCyan.b, 0.05f));
                }
                float iconY = tab.y + 7f;
                float labelY = tab.y + 29f;
                // Icon
                GUI.Label(new Rect(tab.x, iconY, tab.width, 20),
                    icons[i], GUIStyleCache.Get(16, FontStyle.Normal, TextAnchor.MiddleCenter,
                    active ? AccentCyan : Muted, false));
                // Label
                GUI.Label(new Rect(tab.x, labelY, tab.width, 18),
                    labels[i], GUIStyleCache.Get(10, FontStyle.Bold, TextAnchor.MiddleCenter,
                    active ? AccentCyan : Muted, false));
                if (GUI.Button(tab, GUIContent.none, GUIStyle.none) && !active) clicked = i;
            }
            return clicked;
        }
        // Legacy compat shim — kept so existing DrawTabBar call sites compile unchanged.
        public static int DrawTabBar(Rect r,string[] icons,string[] titles,int selected) => selected;
        public static Texture2D LoadTexture(string path) => TacticalGUI.LoadTexture(path);

        // Draws the standard header bar used by all full-screen pages.
        // Returns true if the back-to-home button was clicked.
        public static bool DrawPageHeader(Rect r, string title, int credits, int gold)
        {
            Fill(r, ThemeBg);
            Fill(new Rect(r.x, r.yMax - 1, r.width, 1.5f), ThemeBorder);
            // Gold accent left stripe
            Fill(new Rect(r.x, r.y, 3f, r.height), AccentGold);
            GUI.Label(new Rect(r.x + 14, r.y, r.width * 0.5f, r.height),
                title, GUIStyleCache.Get(15, FontStyle.Bold, TextAnchor.MiddleLeft, AccentGold, true));
            // Currency badges on right
            string curr = "  " + gold.ToString("N0") + "G   " + credits.ToString("N0") + "Cr";
            GUI.Label(new Rect(r.xMax - 220, r.y, 210, r.height),
                curr, GUIStyleCache.Get(11, FontStyle.Bold, TextAnchor.MiddleRight, Text, true));
            return false;
        }
        // Hexagon outline — flat-top hex drawn as 6-segment axis-aligned line approximation
        public static void DrawHex(Vector2 center, float radius, Color color, float lineWidth = 2f)
        {
            const int sides = 6;
            Vector2 prev = center + new Vector2(0, -radius);
            for (int i = 1; i <= sides; i++)
            {
                float angle = (i * 60f - 90f) * Mathf.Deg2Rad;
                Vector2 next = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                DrawLine(prev, next, color, lineWidth);
                prev = next;
            }
        }
    }
}
