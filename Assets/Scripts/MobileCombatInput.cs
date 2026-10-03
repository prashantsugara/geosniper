using UnityEngine;

namespace GeoSniper
{
    [DefaultExecutionOrder(-200)]
    public sealed class MobileCombatInput : MonoBehaviour
    {
        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool FireHeld { get; private set; }
        public bool FirePressed { get; private set; }
        public bool JumpPressed, ScopePressed, ReloadPressed, CrouchPressed, Sprint, FLIRPressed, IsCrouching;
        public static float ZoomSliderValue = 0f;
        public bool HoldBreathHeld { get; private set; }
        public bool DrawControlsInOnGUI = true;
        public bool ShowHoldBreath = false;
        public bool KnifeKillAvailable = false;
        public bool KnifeKillPressed = false;
        public bool IsPointerOverUI { get; private set; }

        public int SelectedWeaponIndex = 0;
        public bool WeaponSwitchTriggered = false;
        public string[] WeaponSlotNames = new string[] { "BARRETT .50", "M24 TAC", "MK12 SPR" };
        public string[] WeaponSlotAmmo = new string[] { "5 / 25", "20 / 60", "30 / 90" };

        bool WeaponOwned(int index)
        {
            if (GeoSniperGame.IsDebugMode) return true;
            return PlayerPrefs.GetInt("GeoSniper.WeaponUnlocked_" + index, index == 1 ? 1 : 0) == 1;
        }
        public bool CanSelectWeaponSlot(int index) => !ShowHoldBreath && OwnedWeaponCount() > 1 && WeaponOwned(index);
        int OwnedWeaponCount()
        {
            int count = 0;
            for (int i = 0; i < 3; i++) if (WeaponOwned(i)) count++;
            return count;
        }

        int moveFinger = -1, lookFinger = -1, leftFireFinger = -1, rightFireFinger = -1, breathFinger = -1;

        enum MouseControlKind { None, Move, Look, LeftFire, RightFire, Breath }
        MouseControlKind mouseActive = MouseControlKind.None;
        Vector2 mouseLastScreenPos;

        public static float Scale => Mathf.Max(.4f, Mathf.Min(Screen.height / 720f, Screen.width / 800f));
        public static Rect Safe => new Rect(Screen.safeArea.x / Scale, (Screen.height - Screen.safeArea.yMax) / Scale, Screen.safeArea.width / Scale, Screen.safeArea.height / Scale);
        public static Vector2 LeftCenter => new Vector2(Safe.x + 150, Safe.yMax - 140);

        // PUBG / CoD Mobile Style Ergonomic Layout
        public static Rect LeftFireRect => new Rect(Safe.x + 75, Safe.yMax - 335, 84, 84);
        public static Rect RightFireRect => new Rect(Safe.xMax - 245, Safe.yMax - 205, 92, 92);

        public static Rect ScopeRect => new Rect(Safe.xMax - 145, Safe.yMax - 295, 76, 76);
        public static Rect JumpRect => new Rect(Safe.xMax - 95, Safe.yMax - 180, 64, 64);
        public static Rect CrouchRect => new Rect(Safe.xMax - 145, Safe.yMax - 105, 64, 64);
        public static Rect ReloadRect => new Rect(Safe.xMax - 225, Safe.yMax - 105, 64, 64);

        public static Rect SprintRect => new Rect(Safe.x + 95, Safe.yMax - 235, 60, 60);
        public static Rect BreathRect => new Rect(Safe.xMax - 235, Safe.yMax - 305, 70, 70);
        public static Rect FLIRRect => new Rect(Safe.xMax - 315, Safe.yMax - 305, 70, 70);
        public static bool FLIRActive;
        public static Rect KnifeKillRect => new Rect(Safe.xMax - 315, Safe.yMax - 185, 78, 78);

        // Weapon Switcher Dock (3 slots at bottom center)
        public static Rect WeaponSlot1Rect => new Rect(Safe.center.x - 175, Safe.yMax - 48, 112, 40);
        public static Rect WeaponSlot2Rect => new Rect(Safe.center.x - 56, Safe.yMax - 48, 112, 40);
        public static Rect WeaponSlot3Rect => new Rect(Safe.center.x + 63, Safe.yMax - 48, 112, 40);

        // Procedural High-Definition Anti-Aliased Vector Icons
        static Texture2D discBgTex;
        static Texture2D discBgActiveTex;
        static Texture2D discBgFireActiveTex;
        static Texture2D iconBullet;
        static Texture2D iconScope;
        static Texture2D iconCrouch;
        static Texture2D iconJump;
        static Texture2D iconReload;
        static Texture2D iconBreath;
        static Texture2D iconSprint;
        static Texture2D iconKnife;
        static Texture2D iconFLIR;
        static Texture2D joystickRingTex;
        static Texture2D joystickKnobTex;

        void Awake()
        {
            InitProceduralHUD();
        }

        static void InitProceduralHUD()
        {
            if (discBgTex != null) return;
            const int S = 128;
            const float C = 64f;

            // 1. Semi-transparent Frosted Glass Button Backgrounds (Normal & Active)
            discBgTex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            discBgActiveTex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            discBgFireActiveTex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var pxNormal = new Color[S * S];
            var pxActive = new Color[S * S];
            var pxFire = new Color[S * S];

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(C, C));
                    // Glass body (r <= 58)
                    float fillA = Mathf.Clamp01(58.5f - d);
                    // Glowing outer ring (54 <= r <= 60)
                    float ringA = Mathf.Clamp01(3.0f - Mathf.Abs(d - 57f));
                    // Subtle inner bezel (r around 46)
                    float bezelA = Mathf.Clamp01(1.5f - Mathf.Abs(d - 46f)) * 0.25f;

                    Color normFill = new Color(0.025f, 0.035f, 0.045f, 0.48f * fillA);
                    Color normRing = new Color(0.85f, 0.92f, 1.00f, 0.35f * ringA + bezelA * 0.5f);
                    var normal=Color.Lerp(normFill,normRing,Mathf.Clamp01(ringA+bezelA));
                    normal.a=Mathf.Clamp01(normFill.a+normRing.a);pxNormal[y*S+x]=normal;

                    Color actFill = new Color(0.04f, 0.18f, 0.22f, 0.72f * fillA);
                    Color actRing = new Color(0.3f, 0.8f, 0.85f, 0.9f * ringA + bezelA * .5f);
                    var pressed=Color.Lerp(actFill,actRing,Mathf.Clamp01(ringA+bezelA));
                    pressed.a=Mathf.Clamp01(actFill.a+actRing.a);pxActive[y*S+x]=pressed;

                    // Solid pressed state keeps the fire icon legible.
                    Color fireFill = new Color(0.92f, 0.25f, 0.06f, 0.78f * fillA);
                    Color fireRing = new Color(1.00f, 0.72f, 0.18f, 0.98f * ringA + bezelA * 2f);
                    var firing=Color.Lerp(fireFill,fireRing,Mathf.Clamp01(ringA+bezelA));
                    firing.a=Mathf.Clamp01(fireFill.a+fireRing.a);pxFire[y*S+x]=firing;
                }
            }
            discBgTex.SetPixels(pxNormal); discBgTex.Apply();
            discBgActiveTex.SetPixels(pxActive); discBgActiveTex.Apply();
            discBgFireActiveTex.SetPixels(pxFire); discBgFireActiveTex.Apply();

            // 2. High-Caliber Bullet Icon
            iconBullet = CreateIcon(S, (x, y) =>
            {
                float nx = x - C;
                float ny = y - C;
                // Ogive pointed bullet tip (y from 0 to 44)
                if (ny >= 0 && ny <= 44)
                {
                    float rAtY = 16f * (1f - Mathf.Pow(ny / 44f, 1.6f));
                    if (Mathf.Abs(nx) <= rAtY) return Mathf.Clamp01(rAtY - Mathf.Abs(nx) + 0.5f);
                }
                // Bullet cylindrical body (y from -32 to 0)
                if (ny >= -32 && ny < 0)
                {
                    if (Mathf.Abs(nx) <= 16f) return Mathf.Clamp01(16f - Mathf.Abs(nx) + 0.5f);
                }
                // Extraction groove (y from -36 to -32)
                if (ny >= -36 && ny < -32)
                {
                    if (Mathf.Abs(nx) <= 13.5f) return Mathf.Clamp01(13.5f - Mathf.Abs(nx) + 0.5f);
                }
                // Rim base (y from -42 to -36)
                if (ny >= -42 && ny < -36)
                {
                    if (Mathf.Abs(nx) <= 16f) return Mathf.Clamp01(16f - Mathf.Abs(nx) + 0.5f);
                }
                // Dual muzzle flash side chevrons
                if (Mathf.Abs(ny - 34f) <= 2.5f && Mathf.Abs(nx) >= 24f && Mathf.Abs(nx) <= 38f) return 0.85f;
                if (Mathf.Abs(ny - 20f) <= 2.5f && Mathf.Abs(nx) >= 28f && Mathf.Abs(nx) <= 44f) return 0.85f;
                return 0f;
            });

            // 3. Sniper Precision Scope Reticle Icon
            iconScope = CreateIcon(S, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(C, C));
                float ring = Mathf.Clamp01(2.5f - Mathf.Abs(d - 42f));
                float dot = Mathf.Clamp01(4.0f - d);
                float cross = 0f;
                float nx = Mathf.Abs(x - C);
                float ny = Mathf.Abs(y - C);
                if (d >= 12f && d <= 52f)
                {
                    if (nx <= 1.5f && ny >= 10f) cross = Mathf.Max(cross, Mathf.Clamp01(1.8f - nx));
                    if (ny <= 1.5f && nx >= 10f) cross = Mathf.Max(cross, Mathf.Clamp01(1.8f - ny));
                }
                if (Mathf.Abs(d - 22f) <= 1.2f && (nx <= 6f || ny <= 6f)) cross = 1f;
                if (Mathf.Abs(d - 32f) <= 1.2f && (nx <= 6f || ny <= 6f)) cross = 1f;
                return Mathf.Max(ring, Mathf.Max(dot, cross));
            });

            // 4. Tactical Crouch Silhouette Icon (Real kneeling operative)
            iconCrouch = CreateIcon(S, (x, y) =>
            {
                float dHead = Vector2.Distance(new Vector2(x, y), new Vector2(52, 90));
                if (dHead <= 11f) return Mathf.Clamp01(11.5f - dHead); // Helmet/Head
                // Torso leaning forward at tactical combat angle
                float torsoDist = DistToSegment(x, y, 52, 80, 44, 48);
                if (torsoDist <= 9.5f) return Mathf.Clamp01(10f - torsoDist);
                // Forward bent thigh & shin
                float thighDist = DistToSegment(x, y, 44, 48, 72, 40);
                if (thighDist <= 7.5f) return Mathf.Clamp01(8f - thighDist);
                float shinDist = DistToSegment(x, y, 72, 40, 72, 20);
                if (shinDist <= 6.5f) return Mathf.Clamp01(7f - shinDist);
                // Rear ground knee & foot
                float rearLeg = DistToSegment(x, y, 44, 48, 28, 22);
                if (rearLeg <= 7f) return Mathf.Clamp01(7.5f - rearLeg);
                float rearFoot = DistToSegment(x, y, 28, 22, 18, 22);
                if (rearFoot <= 5f) return Mathf.Clamp01(5.5f - rearFoot);
                // Tactical rifle held at ready
                float rifle = DistToSegment(x, y, 48, 68, 94, 68);
                if (rifle <= 3.5f) return Mathf.Clamp01(4f - rifle);
                // Support arm
                float arm = DistToSegment(x, y, 50, 76, 68, 68);
                if (arm <= 5f) return Mathf.Clamp01(5.5f - arm);
                return 0f;
            });

            // 5. Tactical Jump / Vault Icon (Upward motion chevrons)
            iconJump = CreateIcon(S, (x, y) =>
            {
                float a1 = ChevronAlpha(x, y, 64, 88, 32, 60, 6.5f);
                float a2 = ChevronAlpha(x, y, 64, 66, 32, 38, 6.5f);
                float bar = (y >= 20 && y <= 26 && x >= 36 && x <= 92) ? 0.9f : 0f;
                return Mathf.Max(a1, Mathf.Max(a2, bar));
            });

            // 6. Circular Magazine Reload Icon
            iconReload = CreateIcon(S, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(C, C));
                float angle = Mathf.Atan2(y - C, x - C) * Mathf.Rad2Deg;
                if (angle < 0) angle += 360f;

                float arc = 0f;
                if (angle >= 30 && angle <= 150 && Mathf.Abs(d - 38f) <= 4.5f) arc = Mathf.Clamp01(5f - Mathf.Abs(d - 38f));
                if (angle >= 210 && angle <= 330 && Mathf.Abs(d - 38f) <= 4.5f) arc = Mathf.Clamp01(5f - Mathf.Abs(d - 38f));

                float head1 = TriangleAlpha(x, y, 28, 76, 28, 54, 14, 65);
                float head2 = TriangleAlpha(x, y, 100, 52, 100, 74, 114, 63);
                return Mathf.Max(arc, Mathf.Max(head1, head2));
            });

            // 7. Sniper Lung Breath / Focus Icon
            iconBreath = CreateIcon(S, (x, y) =>
            {
                float dl = Vector2.Distance(new Vector2(x, y * 0.9f), new Vector2(46, 52));
                float leftLobe = Mathf.Clamp01(16f - dl);
                float dr = Vector2.Distance(new Vector2(x, y * 0.9f), new Vector2(82, 52));
                float rightLobe = Mathf.Clamp01(16f - dr);
                float trachea = (Mathf.Abs(x - C) <= 3.5f && y >= 64 && y <= 92) ? 1f : 0f;
                float focusRing = Mathf.Clamp01(2f - Mathf.Abs(Vector2.Distance(new Vector2(x, y), new Vector2(C, C)) - 24f));
                return Mathf.Max(Mathf.Max(leftLobe, rightLobe) * 0.75f, Mathf.Max(trachea, focusRing));
            });

            // 8. Tactical Sprint Icon
            iconSprint = CreateIcon(S, (x, y) =>
            {
                float c1 = ChevronAlpha(y, x, 64, 88, 32, 60, 6f);
                float c2 = ChevronAlpha(y, x, 64, 64, 32, 36, 6f);
                return Mathf.Max(c1, c2);
            });

            // 9. Tactical Combat Knife Icon
            iconKnife = CreateIcon(S, (x, y) =>
            {
                float blade = DistToSegment(x, y, 42, 42, 95, 95);
                float bladeA = blade <= 6f ? Mathf.Clamp01(6.5f - blade) : 0f;
                float tip = TriangleAlpha(x, y, 86, 96, 96, 86, 106, 106);
                float guard = DistToSegment(x, y, 32, 54, 54, 32);
                float guardA = guard <= 5f ? Mathf.Clamp01(5.5f - guard) : 0f;
                float hilt = DistToSegment(x, y, 40, 40, 18, 18);
                float hiltA = hilt <= 4.5f ? Mathf.Clamp01(5f - hilt) : 0f;
                return Mathf.Max(bladeA, Mathf.Max(tip, Mathf.Max(guardA, hiltA)));
            });

            // 9b. FLIR Thermal Vision Icon
            iconFLIR = CreateIcon(S, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(C, C));
                float ring = Mathf.Clamp01(3.5f - Mathf.Abs(d - 46f));
                float core = Mathf.Clamp01(18f - d);
                float midRing = Mathf.Clamp01(2.5f - Mathf.Abs(d - 32f)) * 0.7f;
                bool tickH = Mathf.Abs(y - C) <= 2.5f && (d >= 24f && d <= 56f);
                bool tickV = Mathf.Abs(x - C) <= 2.5f && (d >= 24f && d <= 56f);
                float ticks = (tickH || tickV) ? 0.95f : 0f;
                return Mathf.Max(ring, Mathf.Max(core * 0.85f, Mathf.Max(midRing, ticks)));
            });

            // 10. Modern Joystick Ring & Knob
            joystickRingTex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            joystickKnobTex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var pxRing = new Color[S * S];
            var pxKnob = new Color[S * S];

            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(C, C));
                    float ringA = Mathf.Clamp01(2.5f - Mathf.Abs(d - 56f)) * 0.45f;
                    float baseA = Mathf.Clamp01(58f - d) * 0.18f;
                    bool isTick = (Mathf.Abs(x - C) <= 2f || Mathf.Abs(y - C) <= 2f) && d >= 48f && d <= 58f;
                    pxRing[y * S + x] = new Color(0.1f, 0.8f, 1.0f, isTick ? 0.75f : (ringA + baseA));

                    float knobA = Mathf.Clamp01(52f - d);
                    float knobCore = Mathf.Clamp01(18f - d);
                    float knobRing = Mathf.Clamp01(2.5f - Mathf.Abs(d - 48f));
                    Color coreColor = Color.Lerp(new Color(0.04f, 0.12f, 0.20f, 0.65f), new Color(0.00f, 0.88f, 1.00f, 0.85f), knobCore);
                    pxKnob[y * S + x] = coreColor * knobA + new Color(1f, 1f, 1f, knobRing * 0.6f);
                }
            }
            joystickRingTex.SetPixels(pxRing); joystickRingTex.Apply();
            joystickKnobTex.SetPixels(pxKnob); joystickKnobTex.Apply();
        }

        static Texture2D CreateIcon(int size, System.Func<int, int, float> alphaFunc)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = Mathf.Clamp01(alphaFunc(x, y));
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        static float DistToSegment(float px, float py, float x1, float y1, float x2, float y2)
        {
            float dx = x2 - x1, dy = y2 - y1;
            float l2 = dx * dx + dy * dy;
            if (l2 < 0.0001f) return Vector2.Distance(new Vector2(px, py), new Vector2(x1, y1));
            float t = Mathf.Clamp01(((px - x1) * dx + (py - y1) * dy) / l2);
            return Vector2.Distance(new Vector2(px, py), new Vector2(x1 + t * dx, y1 + t * dy));
        }

        static float ChevronAlpha(float x, float y, float tipX, float tipY, float wingDx, float wingDy, float width)
        {
            float d1 = DistToSegment(x, y, tipX, tipY, tipX - wingDx, wingDy);
            float d2 = DistToSegment(x, y, tipX, tipY, tipX + wingDx, wingDy);
            float d = Mathf.Min(d1, d2);
            return Mathf.Clamp01(width - d);
        }

        static float TriangleAlpha(float px, float py, float x1, float y1, float x2, float y2, float x3, float y3)
        {
            float d1 = (px - x2) * (y1 - y2) - (x1 - x2) * (py - y2);
            float d2 = (px - x3) * (y2 - y3) - (x2 - x3) * (py - y3);
            float d3 = (px - x1) * (y3 - y1) - (x3 - x1) * (py - y1);
            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
            return !(hasNeg && hasPos) ? 1f : 0f;
        }

        void Update()
        {
            Look = Vector2.zero;
            JumpPressed = ScopePressed = ReloadPressed = CrouchPressed = FLIRPressed = false;
            FirePressed = false;
            KnifeKillPressed = false;
            WeaponSwitchTriggered = false;
            IsPointerOverUI = false;

            // 1. Process Multi-touch inputs (Mobile & Touch Screen)
            if (Input.touchCount > 0)
            {
                foreach (var touch in Input.touches)
                {
                    Vector2 p = new Vector2(touch.position.x, Screen.height - touch.position.y) / Scale;
                    if (touch.phase == TouchPhase.Began)
                    {
                        if (LeftFireRect.Contains(p)) { leftFireFinger = touch.fingerId; FirePressed = true; IsPointerOverUI = true; }
                        else if (RightFireRect.Contains(p)) { rightFireFinger = touch.fingerId; FirePressed = true; IsPointerOverUI = true; }
                        else if ((!ShowHoldBreath && KnifeKillRect.Contains(p))) { KnifeKillPressed = true; IsPointerOverUI = true; }
                        else if (ScopeRect.Contains(p)) { ScopePressed = true; IsPointerOverUI = true; }
                        else if ((!ShowHoldBreath && JumpRect.Contains(p))) { JumpPressed = true; IsPointerOverUI = true; }
                        else if (ReloadRect.Contains(p)) { ReloadPressed = true; IsPointerOverUI = true; }
                        else if ((!ShowHoldBreath && SprintRect.Contains(p))) { Sprint = !Sprint; if (Sprint) IsCrouching = false; IsPointerOverUI = true; }
                        else if (CrouchRect.Contains(p))
                        {
                            CrouchPressed = true;

                            IsPointerOverUI = true;
                        }
                        else if (ShowHoldBreath && BreathRect.Contains(p)) { breathFinger = touch.fingerId; IsPointerOverUI = true; }
                        else if (ShowHoldBreath && FLIRRect.Contains(p)) { FLIRPressed = true; IsPointerOverUI = true; }
                        else if (CanSelectWeaponSlot(0) && WeaponSlot1Rect.Contains(p)) { SelectedWeaponIndex = 0; WeaponSwitchTriggered = true; IsPointerOverUI = true; }
                        else if (CanSelectWeaponSlot(1) && WeaponSlot2Rect.Contains(p)) { SelectedWeaponIndex = 1; WeaponSwitchTriggered = true; IsPointerOverUI = true; }
                        else if (CanSelectWeaponSlot(2) && WeaponSlot3Rect.Contains(p)) { SelectedWeaponIndex = 2; WeaponSwitchTriggered = true; IsPointerOverUI = true; }
                        else if (Vector2.Distance(p, LeftCenter) < 120 && moveFinger < 0) { moveFinger = touch.fingerId; IsPointerOverUI = true; }
                        else if (p.y < 44 || (p.x < 100 && p.y < 230) || (p.x > Safe.xMax - 220 && p.y < 360)) { IsPointerOverUI = true; }
                        else if (p.x > Safe.center.x && lookFinger < 0) { lookFinger = touch.fingerId; }
                    }

                    bool ended = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
                    if (touch.fingerId == moveFinger)
                    {
                        var delta = (p - LeftCenter) / 75;
                        Move = ended ? Vector2.zero : Vector2.ClampMagnitude(new Vector2(delta.x, -delta.y), 1);
                        if (ended) moveFinger = -1;
                        else IsPointerOverUI = true;
                    }
                    if (touch.fingerId == lookFinger)
                    {
                        if (!ended) Look += touch.deltaPosition / Scale;
                        else lookFinger = -1;
                    }
                    if (touch.fingerId == rightFireFinger)
                    {
                        // Right fire swipe allows look-through aim while firing
                        if (!ended) Look += touch.deltaPosition / Scale;
                        else rightFireFinger = -1;
                        IsPointerOverUI = true;
                    }
                    if (touch.fingerId == leftFireFinger)
                    {
                        // Left fire swipe allows fine recoil / tracking aim while firing
                        if (!ended) Look += touch.deltaPosition / Scale;
                        else leftFireFinger = -1;
                        IsPointerOverUI = true;
                    }
                    if (touch.fingerId == breathFinger)
                    {
                        if (ended) breathFinger = -1;
                        else IsPointerOverUI = true;
                    }
                }
            }
            else
            {
                // Android can lose an Ended event during interruptions; never leave fire or movement held.
                if (Application.isMobilePlatform) { ResetTouches(false); return; }
                // 2. Process Mouse / Pointer Fallback (Unity Editor & PC testing)
                Vector2 mouseP = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y) / Scale;

                bool inButton = LeftFireRect.Contains(mouseP) || RightFireRect.Contains(mouseP) ||
                               (!ShowHoldBreath && KnifeKillRect.Contains(mouseP)) ||
                               ScopeRect.Contains(mouseP) || (!ShowHoldBreath && JumpRect.Contains(mouseP)) ||
                               CrouchRect.Contains(mouseP) || ReloadRect.Contains(mouseP) ||
                               (!ShowHoldBreath && SprintRect.Contains(mouseP)) || (ShowHoldBreath && BreathRect.Contains(mouseP)) ||
                               (ShowHoldBreath && FLIRRect.Contains(mouseP)) ||
                               (CanSelectWeaponSlot(0) && WeaponSlot1Rect.Contains(mouseP)) || (CanSelectWeaponSlot(1) && WeaponSlot2Rect.Contains(mouseP)) || (CanSelectWeaponSlot(2) && WeaponSlot3Rect.Contains(mouseP)) ||
                               Vector2.Distance(mouseP, LeftCenter) < 120 || mouseP.y < 44 || (mouseP.x < 100 && mouseP.y < 230) || (mouseP.x > Safe.xMax - 220 && mouseP.y < 360);

                if (inButton || mouseActive != MouseControlKind.None)
                {
                    IsPointerOverUI = true;
                }

                if (Input.GetMouseButtonDown(0))
                {
                    mouseLastScreenPos = Input.mousePosition;
                    if (LeftFireRect.Contains(mouseP)) { mouseActive = MouseControlKind.LeftFire; FirePressed = true; }
                    else if (RightFireRect.Contains(mouseP)) { mouseActive = MouseControlKind.RightFire; FirePressed = true; }
                    else if ((!ShowHoldBreath && KnifeKillRect.Contains(mouseP))) KnifeKillPressed = true;
                    else if (ScopeRect.Contains(mouseP)) ScopePressed = true;
                    else if ((!ShowHoldBreath && JumpRect.Contains(mouseP))) JumpPressed = true;
                    else if (ReloadRect.Contains(mouseP)) ReloadPressed = true;
                    else if ((!ShowHoldBreath && SprintRect.Contains(mouseP))) { Sprint = !Sprint; if (Sprint) IsCrouching = false; }
                    else if (CrouchRect.Contains(mouseP))
                    {
                        CrouchPressed = true;

                    }
                    else if (ShowHoldBreath && BreathRect.Contains(mouseP)) mouseActive = MouseControlKind.Breath;
                    else if (ShowHoldBreath && FLIRRect.Contains(mouseP)) FLIRPressed = true;
                    else if (CanSelectWeaponSlot(0) && WeaponSlot1Rect.Contains(mouseP)) { SelectedWeaponIndex = 0; WeaponSwitchTriggered = true; }
                    else if (CanSelectWeaponSlot(1) && WeaponSlot2Rect.Contains(mouseP)) { SelectedWeaponIndex = 1; WeaponSwitchTriggered = true; }
                    else if (CanSelectWeaponSlot(2) && WeaponSlot3Rect.Contains(mouseP)) { SelectedWeaponIndex = 2; WeaponSwitchTriggered = true; }
                    else if (Vector2.Distance(mouseP, LeftCenter) < 120) mouseActive = MouseControlKind.Move;
                }

                if (Input.GetMouseButton(0))
                {
                    Vector2 mouseDelta = (new Vector2(Input.mousePosition.x, Input.mousePosition.y) - mouseLastScreenPos) / Scale;
                    mouseLastScreenPos = Input.mousePosition;

                    if (mouseActive == MouseControlKind.Move)
                    {
                        var delta = (mouseP - LeftCenter) / 75;
                        Move = Vector2.ClampMagnitude(new Vector2(delta.x, -delta.y), 1);
                    }
                    else if (mouseActive == MouseControlKind.RightFire || mouseActive == MouseControlKind.LeftFire)
                    {
                        Look += mouseDelta;
                    }
                }

                if (Input.GetMouseButtonUp(0))
                {
                    if (mouseActive == MouseControlKind.Move) Move = Vector2.zero;
                    mouseActive = MouseControlKind.None;
                }
            }

            FireHeld = leftFireFinger >= 0 || rightFireFinger >= 0 || mouseActive == MouseControlKind.LeftFire || mouseActive == MouseControlKind.RightFire;
            HoldBreathHeld = ShowHoldBreath && (breathFinger >= 0 || mouseActive == MouseControlKind.Breath);
        }

        void ResetTouches(bool resetSprint = true)
        {
            moveFinger = lookFinger = leftFireFinger = rightFireFinger = breathFinger = -1;
            mouseActive = MouseControlKind.None;
            Move = Look = Vector2.zero;
            FireHeld = FirePressed = HoldBreathHeld = false;
            JumpPressed = ScopePressed = ReloadPressed = CrouchPressed = FLIRPressed = false;
            KnifeKillPressed = WeaponSwitchTriggered = false;
            if (resetSprint) Sprint = false;
        }

        void OnApplicationPause(bool paused) { if (paused) ResetTouches(); }
        void OnApplicationFocus(bool focus) { if (!focus) ResetTouches(); }
        void OnDisable() { ResetTouches(); }

        void ModernHUDButton(Rect rect, Texture2D icon, Color tint, bool active, string badge = null, bool isFire = false)
        {
            InitProceduralHUD();
            var bg = active ? (isFire ? (discBgFireActiveTex != null ? discBgFireActiveTex : discBgActiveTex) : discBgActiveTex) : discBgTex;
            if (bg != null)
            {
                GUI.color = active ? (isFire ? new Color(1f, 0.95f, 0.85f, 0.98f) : new Color(1f, 1f, 1f, 0.98f)) : new Color(1f, 1f, 1f, 0.72f);
                GUI.DrawTexture(rect, bg);
            }

            if (icon != null)
            {
                float iconPadding = rect.width * 0.22f;
                Rect iconRect = new Rect(rect.x + iconPadding, rect.y + iconPadding, rect.width - iconPadding * 2, rect.height - iconPadding * 2);
                GUI.color = active ? (isFire ? new Color(1f, 0.98f, 0.85f, 1f) : Color.white) : tint;
                GUI.DrawTexture(iconRect, icon);
            }

            if (!string.IsNullOrEmpty(badge))
            {
                var badgeStyle = GUIStyleCache.Get(11,FontStyle.Bold,TextAnchor.MiddleCenter,
                    active ? (isFire ? TacticalGUI.AccentGold : TacticalGUI.AccentCyan) : new Color(.92f,.95f,.98f));
                Rect labelRect=new Rect(rect.x-10,rect.yMax-6,rect.width+20,16);
                GUI.color=new Color(.015f,.025f,.035f,.68f);
                GUI.DrawTexture(new Rect(rect.center.x-32,labelRect.y,64,16),Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(labelRect, badge, badgeStyle);
            }
            GUI.color = Color.white;
        }

        void ModernWeaponSlot(Rect rect, string name, int index, bool active, string ammoText)
        {
            InitProceduralHUD();
            Color bg = active ? new Color(0.04f, 0.22f, 0.32f, 0.78f) : new Color(0.03f, 0.06f, 0.10f, 0.55f);
            Color border = active ? TacticalGUI.AccentCyan : new Color(0.25f, 0.35f, 0.45f, 0.35f);

            TacticalGUI.DrawPanel(rect, bg, border, active ? 1.8f : 1.0f);

            if (active)
            {
                GUI.color = TacticalGUI.AccentCyan;
                GUI.DrawTexture(new Rect(rect.x + 4, rect.y + 4, 3, rect.height - 8), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            var nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = active ? Color.white : new Color(0.68f, 0.74f, 0.82f) }
            };
            GUI.Label(new Rect(rect.x + (active ? 12 : 8), rect.y + 4, rect.width - 16, 16), name, nameStyle);

            var ammoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.LowerRight,
                normal = { textColor = active ? TacticalGUI.AccentGold : new Color(0.75f, 0.80f, 0.86f, 0.8f) }
            };
            GUI.Label(new Rect(rect.x + 8, rect.y, rect.width - 16, rect.height - 4), ammoText, ammoStyle);
        }

        void OnGUI()
        {
            if (!DrawControlsInOnGUI) return;
            GUI.depth = -300; // Prioritized above scope blackout (-200) so controls remain visible while scoped
            DrawControls();
        }

        public void DrawControls()
        {
            InitProceduralHUD();
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * Scale);

            // Removed IMGUI EventType.MouseDown hack that caused continuous firing

            // Left Joystick (PUBG / COD floating translucent HUD)
            if (joystickRingTex != null)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.7f);
                GUI.DrawTexture(new Rect(LeftCenter.x - 90, LeftCenter.y - 90, 180, 180), joystickRingTex);
            }
            Vector2 knobPos = LeftCenter + new Vector2(Move.x, -Move.y) * 60;
            if (joystickKnobTex != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(knobPos.x - 36, knobPos.y - 36, 72, 72), joystickKnobTex);
            }

            // PUBG/COD Mobile Semi-Transparent Vector Controls
            // Left & Right Fire with fiery combustion flare on active press
            ModernHUDButton(LeftFireRect, iconBullet, new Color(1.0f, 0.4f, 0.35f, 0.85f), leftFireFinger >= 0 || mouseActive == MouseControlKind.LeftFire, "FIRE", isFire: true);
            ModernHUDButton(RightFireRect, iconBullet, new Color(1.0f, 0.3f, 0.25f, 0.95f), rightFireFinger >= 0 || mouseActive == MouseControlKind.RightFire, "AIM/FIRE", isFire: true);

            bool isScoped = ShowHoldBreath;

            // Dedicated Tactical Combat Knife / Stealth Takedown Button (only active when unscoped)
            if (!isScoped)
            {
                if (KnifeKillAvailable)
                {
                    float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 9f);
                    Rect pulseRect = new Rect(KnifeKillRect.x - (KnifeKillRect.width * (pulse - 1f) * 0.5f), KnifeKillRect.y - (KnifeKillRect.height * (pulse - 1f) * 0.5f), KnifeKillRect.width * pulse, KnifeKillRect.height * pulse);
                    ModernHUDButton(pulseRect, iconKnife, new Color(1.0f, 0.22f, 0.22f, 0.98f), true, "TAKEDOWN", isFire: true);
                }
                else
                {
                    ModernHUDButton(KnifeKillRect, iconKnife, new Color(0.88f, 0.92f, 0.98f, 0.85f), KnifeKillPressed, "KNIFE");
                }
            }

            // Scope Optic (Toggles ADS / UNSCOPE)
            ModernHUDButton(ScopeRect, iconScope, isScoped ? TacticalGUI.AccentGold : TacticalGUI.AccentCyan, ScopePressed, isScoped ? "UNSCOPE" : "ADS");

            // Stance Controls (Crouch & Jump)
            ModernHUDButton(CrouchRect, iconCrouch, IsCrouching ? TacticalGUI.AccentCyan : new Color(0.92f, 0.94f, 0.98f, 0.85f), IsCrouching, IsCrouching ? "CROUCHED" : "CROUCH");
            if (!isScoped) ModernHUDButton(JumpRect, iconJump, new Color(0.92f, 0.94f, 0.98f, 0.85f), JumpPressed, "JUMP");

            // Reload & Sprint
            ModernHUDButton(ReloadRect, iconReload, new Color(0.92f, 0.94f, 0.98f, 0.85f), ReloadPressed, "RELOAD");
            if (!isScoped) ModernHUDButton(SprintRect, iconSprint, Sprint ? TacticalGUI.AccentCyan : new Color(0.75f, 0.8f, 0.9f, 0.75f), Sprint, Sprint ? "LOCKED" : "SPRINT");

            // Hold Breath / Focus Button & FLIR Thermal Optic
            if (isScoped)
            {
                ModernHUDButton(BreathRect, iconBreath, HoldBreathHeld ? TacticalGUI.AccentGold : TacticalGUI.AccentCyan, HoldBreathHeld, "BREATH");
                ModernHUDButton(FLIRRect, iconFLIR, FLIRActive ? TacticalGUI.AccentGold : TacticalGUI.AccentCyan, FLIRPressed || FLIRActive, FLIRActive ? "FLIR ON" : "FLIR");
            }

            // Only show weapon switching when the player owns multiple rifles and is unscoped.
            // A single starter rifle gets a clean compact status label instead.
            if (!isScoped)
            {
                if (OwnedWeaponCount() > 1)
                {
                    if (WeaponOwned(0)) ModernWeaponSlot(WeaponSlot1Rect, WeaponSlotNames[0], 0, SelectedWeaponIndex == 0, WeaponSlotAmmo[0]);
                    if (WeaponOwned(1)) ModernWeaponSlot(WeaponSlot2Rect, WeaponSlotNames[1], 1, SelectedWeaponIndex == 1, WeaponSlotAmmo[1]);
                    if (WeaponOwned(2)) ModernWeaponSlot(WeaponSlot3Rect, WeaponSlotNames[2], 2, SelectedWeaponIndex == 2, WeaponSlotAmmo[2]);
                }
                else
                {
                    var singleStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = TacticalGUI.AccentCyan } };
                    GUI.Label(new Rect(Safe.center.x - 82, Safe.yMax - 34, 164, 24), WeaponSlotNames[Mathf.Clamp(SelectedWeaponIndex, 0, 2)] + "  â€¢  EQUIPPED", singleStyle);
                }
            }

            GUI.matrix = previous;
        }
    }
}
