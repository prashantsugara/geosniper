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

        public int SelectedWeaponIndex = WeaponCatalog.StarterPrimary;
        public bool WeaponSwitchTriggered = false;
        public string[] WeaponSlotNames = System.Array.ConvertAll(WeaponCatalog.All,g=>g.Name);
        public string[] WeaponSlotAmmo = System.Array.ConvertAll(WeaponCatalog.All,g=>g.Magazine+" / "+g.Magazine);

        bool WeaponOwned(int index) => WeaponCatalog.Owned(index);
        public bool CanSelectWeaponSlot(int index) => !ShowHoldBreath && OwnedWeaponCount()>1 && WeaponOwned(DockWeapon(index));
        int OwnedWeaponCount() { int count=0;for(int i=0;i<WeaponCatalog.Count;i++)if(WeaponOwned(i))count++;return count; }
        bool ExpandedDock() { for(int i=3;i<WeaponCatalog.Count;i++)if(WeaponOwned(i))return true;return false; }
        int DockWeapon(int slot)
        {
            if(OwnedWeaponCount()<=2)
            {
                if(slot==0)return WeaponCatalog.StarterPrimary;
                if(slot==1)return WeaponCatalog.StarterSecondary;
                return -1;
            }
            if(!ExpandedDock())return slot;
            int current=Mathf.Clamp(SelectedWeaponIndex,0,WeaponCatalog.Count-1);
            if(slot==1)return current;
            int direction=slot==0?-1:1;
            for(int step=1;step<WeaponCatalog.Count;step++)
            { int candidate=(current+direction*step+WeaponCatalog.Count)%WeaponCatalog.Count;if(WeaponOwned(candidate))return candidate; }
            return current;
        }
        int moveFinger = -1, lookFinger = -1, leftFireFinger = -1, rightFireFinger = -1, breathFinger = -1;

        enum MouseControlKind { None, Move, Look, LeftFire, RightFire, Breath }
        MouseControlKind mouseActive = MouseControlKind.None;
        Vector2 mouseLastScreenPos;

        public static float Scale => Mathf.Max(.4f, Mathf.Min(Screen.height / 720f, Screen.width / 800f));
        public static Rect Safe
        {
            get
            {
                Rect safe = Screen.safeArea;
                if (safe.width <= 0 || safe.height <= 0) safe = new Rect(0, 0, Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
                return new Rect(safe.x / Scale, (Screen.height - safe.yMax) / Scale, safe.width / Scale, safe.height / Scale);
            }
        }
        public static Rect MovementRect => MobileControlLayout.GetRect(MobileControl.Move);
        public static Vector2 LeftCenter => MovementRect.center;

        // PUBG / CoD Mobile Style Ergonomic Layout
        public static Rect LeftFireRect => MobileControlLayout.GetRect(MobileControl.LeftFire);
        public static Rect RightFireRect => MobileControlLayout.GetRect(MobileControl.RightFire);

        public static Rect ScopeRect => MobileControlLayout.GetRect(MobileControl.Scope);
        public static Rect JumpRect => MobileControlLayout.GetRect(MobileControl.Jump);
        public static Rect CrouchRect => MobileControlLayout.GetRect(MobileControl.Crouch);
        public static Rect ReloadRect => MobileControlLayout.GetRect(MobileControl.Reload);

        public static Rect SprintRect => MobileControlLayout.GetRect(MobileControl.Sprint);
        public static Rect BreathRect => MobileControlLayout.GetRect(MobileControl.Breath);
        public static Rect FLIRRect => MobileControlLayout.GetRect(MobileControl.FLIR);
        public static bool FLIRActive;
        public static Rect KnifeKillRect => MobileControlLayout.GetRect(MobileControl.Knife);

        // Weapon Switcher Dock (3 slots at bottom center)
        public static Rect WeaponSlot1Rect => MobileControlLayout.WeaponSlot(MobileControlLayout.GetRect(MobileControl.WeaponDock), 0);
        public static Rect WeaponSlot2Rect => MobileControlLayout.WeaponSlot(MobileControlLayout.GetRect(MobileControl.WeaponDock), 1);
        public static Rect WeaponSlot3Rect => MobileControlLayout.WeaponSlot(MobileControlLayout.GetRect(MobileControl.WeaponDock), 2);

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

                    Color normFill = Color.Lerp(new Color(.025f,.045f,.07f),new Color(.18f,.23f,.29f),y/(float)(S-1));
                    normFill.a=.78f*fillA;
                    Color normRing = Color.Lerp(new Color(.24f,.32f,.40f),new Color(.70f,.80f,.88f),y/(float)(S-1));
                    normRing.a=.8f*ringA+bezelA*.6f;
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
            if (MobileControlLayout.Editing) { ResetTouches(); IsPointerOverUI = true; return; }
            Look = Vector2.zero;
            JumpPressed = ScopePressed = ReloadPressed = CrouchPressed = FLIRPressed = false;
            FirePressed = false;
            KnifeKillPressed = false;
            WeaponSwitchTriggered = false;
            IsPointerOverUI = false;

            // 1. Process Multi-touch inputs (Mobile & Touch Screen)
            if (Input.touchCount > 0)
            {
                for (int touchIndex = 0; touchIndex < Input.touchCount; touchIndex++)
                {
                    var touch = Input.GetTouch(touchIndex);
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
                        else if (CanSelectWeaponSlot(0) && WeaponSlot1Rect.Contains(p)) { SelectedWeaponIndex = DockWeapon(0); WeaponSwitchTriggered = true; IsPointerOverUI = true; }
                        else if (CanSelectWeaponSlot(1) && WeaponSlot2Rect.Contains(p)) { SelectedWeaponIndex = DockWeapon(1); WeaponSwitchTriggered = true; IsPointerOverUI = true; }
                        else if (CanSelectWeaponSlot(2) && WeaponSlot3Rect.Contains(p)) { SelectedWeaponIndex = DockWeapon(2); WeaponSwitchTriggered = true; IsPointerOverUI = true; }
                        else if (MovementRect.Contains(p) && moveFinger < 0) { moveFinger = touch.fingerId; IsPointerOverUI = true; }
                        else if (MobileControlLayout.ReservedUIContains(p, Safe)) { IsPointerOverUI = true; }
                        else if ((MobileControlLayout.AimOnLeft ? p.x < Safe.center.x : p.x > Safe.center.x) && lookFinger < 0) { lookFinger = touch.fingerId; }
                    }

                    bool ended = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
                    if (touch.fingerId == moveFinger)
                    {
                        var delta = (p - LeftCenter) / Mathf.Max(1, MovementRect.width * (75f / 180f));
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
                               MovementRect.Contains(mouseP) || MobileControlLayout.ReservedUIContains(mouseP, Safe);

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
                    else if (CanSelectWeaponSlot(0) && WeaponSlot1Rect.Contains(mouseP)) { SelectedWeaponIndex = DockWeapon(0); WeaponSwitchTriggered = true; }
                    else if (CanSelectWeaponSlot(1) && WeaponSlot2Rect.Contains(mouseP)) { SelectedWeaponIndex = DockWeapon(1); WeaponSwitchTriggered = true; }
                    else if (CanSelectWeaponSlot(2) && WeaponSlot3Rect.Contains(mouseP)) { SelectedWeaponIndex = DockWeapon(2); WeaponSwitchTriggered = true; }
                    else if (MovementRect.Contains(mouseP)) mouseActive = MouseControlKind.Move;
                }

                if (Input.GetMouseButton(0))
                {
                    Vector2 mouseDelta = (new Vector2(Input.mousePosition.x, Input.mousePosition.y) - mouseLastScreenPos) / Scale;
                    mouseLastScreenPos = Input.mousePosition;

                    if (mouseActive == MouseControlKind.Move)
                    {
                        var delta = (mouseP - LeftCenter) / Mathf.Max(1, MovementRect.width * (75f / 180f));
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

        static Color WithOpacity(Color color, float opacity) { color.a *= opacity; return color; }
        bool studyHud;
        void ModernHUDButton(MobileControl control, Rect rect, Texture2D icon, Color tint, bool active, string badge = null, bool isFire = false)
        {
            InitProceduralHUD();
            float opacity = MobileControlLayout.Opacity(control);
            var bg = active ? (isFire ? (discBgFireActiveTex != null ? discBgFireActiveTex : discBgActiveTex) : discBgActiveTex) : discBgTex;
            if (bg != null)
            {
                GUI.color = WithOpacity(active ? (isFire ? new Color(1f, 0.95f, 0.85f, 0.98f) : new Color(1f, 1f, 1f, 0.98f)) : new Color(1f, 1f, 1f, studyHud?.42f:.92f), opacity);
                GUI.DrawTexture(rect, bg);
            }

            if (icon != null)
            {
                float iconPadding = rect.width * 0.22f;
                Rect iconRect = new Rect(rect.x + iconPadding, rect.y + iconPadding, rect.width - iconPadding * 2, rect.height - iconPadding * 2);
                GUI.color = WithOpacity(active ? (isFire ? new Color(1f, 0.98f, 0.85f, 1f) : Color.white) : tint, opacity);
                GUI.DrawTexture(iconRect, icon);
            }

            if (!string.IsNullOrEmpty(badge))
            {
                var badgeStyle = GUIStyleCache.Get(11,FontStyle.Bold,TextAnchor.MiddleCenter,
                    active ? (isFire ? TacticalGUI.AccentGold : TacticalGUI.AccentCyan) : new Color(.92f,.95f,.98f));
                Rect labelRect=MobileControlLayout.Clamp(new Rect(rect.x-10,rect.yMax-6,rect.width+20,16),Safe);
                GUI.color=new Color(.015f,.025f,.035f,.68f * opacity);
                GUI.DrawTexture(new Rect(rect.center.x-32,labelRect.y,64,16),Texture2D.whiteTexture);
                GUI.color = WithOpacity(Color.white, opacity);
                GUI.Label(labelRect, badge, badgeStyle);
            }
            GUI.color = Color.white;
        }

        void ModernWeaponSlot(Rect rect, string name, int index, bool active, string ammoText)
        {
            InitProceduralHUD();
            float opacity = MobileControlLayout.Opacity(MobileControl.WeaponDock);
            Color bg = active ? new Color(0.04f, 0.22f, 0.32f, 0.78f) : new Color(0.03f, 0.06f, 0.10f, 0.55f);
            Color border = active ? TacticalGUI.AccentCyan : new Color(0.25f, 0.35f, 0.45f, 0.35f);

            TacticalGUI.DrawPanel(rect, WithOpacity(bg, opacity), WithOpacity(border, opacity), active ? 1.8f : 1.0f);

            if (active)
            {
                GUI.color = WithOpacity(TacticalGUI.AccentCyan, opacity);
                GUI.DrawTexture(new Rect(rect.x + 4, rect.y + 4, 3, rect.height - 8), Texture2D.whiteTexture);
                GUI.color = WithOpacity(Color.white, opacity);
            }

            GUI.color = WithOpacity(Color.white, opacity);

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
            GUI.color = Color.white;
        }

        void OnGUI()
        {
            if (!DrawControlsInOnGUI) return;
            GUI.depth = -300; // Prioritized above scope blackout (-200) so controls remain visible while scoped
            DrawControls();
        }

        public static void DrawLayoutPreviewControl(MobileControl control, Rect rect, float opacity, bool selected)
        {
            InitProceduralHUD();
            GUI.color = new Color(1, 1, 1, opacity);
            Texture2D icon;
            switch (control)
            {
                case MobileControl.Move: icon = joystickRingTex; break;
                case MobileControl.LeftFire: case MobileControl.RightFire: icon = iconBullet; break;
                case MobileControl.Scope: icon = iconScope; break;
                case MobileControl.Jump: icon = iconJump; break;
                case MobileControl.Crouch: icon = iconCrouch; break;
                case MobileControl.Reload: icon = iconReload; break;
                case MobileControl.Sprint: icon = iconSprint; break;
                case MobileControl.Knife: icon = iconKnife; break;
                case MobileControl.Breath: icon = iconBreath; break;
                case MobileControl.FLIR: icon = iconFLIR; break;
                default: icon = null; break;
            }
            if (control != MobileControl.Move && control != MobileControl.WeaponDock) GUI.DrawTexture(rect, discBgTex);
            if (icon != null)
            {
                float pad = control == MobileControl.Move ? 0 : rect.width * .24f;
                GUI.DrawTexture(new Rect(rect.x + pad, rect.y + pad, rect.width - pad * 2, rect.height - pad * 2), icon);
            }
            if (control == MobileControl.WeaponDock)
            {
                for (int i = 0; i < 3; i++)
                {
                    var slot = MobileControlLayout.WeaponSlot(rect, i);
                    CommandGUI.Fill(slot, new Color(.12f, .22f, .28f, opacity));
                    GUI.Label(slot, "RIFLE " + (i + 1), GUIStyleCache.Get(10, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white));
                }
            }
            else GUI.Label(new Rect(rect.x - 8, rect.yMax - 6, rect.width + 16, 20), MobileControlLayout.Name(control), GUIStyleCache.Get(10, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white));
            GUI.color = Color.white;
            if (selected)
            {
                CommandGUI.Fill(new Rect(rect.x, rect.y, rect.width, 2), CommandGUI.AccentCyan);
                CommandGUI.Fill(new Rect(rect.x, rect.yMax - 2, rect.width, 2), CommandGUI.AccentCyan);
                CommandGUI.Fill(new Rect(rect.x, rect.y, 2, rect.height), CommandGUI.AccentCyan);
                CommandGUI.Fill(new Rect(rect.xMax - 2, rect.y, 2, rect.height), CommandGUI.AccentCyan);
            }
        }

        public void DrawControls()
        {
            if (MobileControlLayout.Editing) return;
            studyHud=SectorWorld.ActiveFeatures!=null && SectorWorld.ActiveFeatures.Exists(f=>MapFeatureStyle.Text(f,"street_study")=="true");
            InitProceduralHUD();
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * Scale);

            // Removed IMGUI EventType.MouseDown hack that caused continuous firing

            // Left Joystick (PUBG / COD floating translucent HUD)
            if (joystickRingTex != null)
            {
                GUI.color = new Color(1f, 1f, 1f, (studyHud?.34f:.7f) * MobileControlLayout.Opacity(MobileControl.Move));
                GUI.DrawTexture(MovementRect, joystickRingTex);
            }
            Vector2 knobPos = LeftCenter + new Vector2(Move.x, -Move.y) * (MovementRect.width / 3);
            if (joystickKnobTex != null)
            {
                float knob = MovementRect.width * .4f;
                GUI.color = WithOpacity(Color.white, MobileControlLayout.Opacity(MobileControl.Move));
                GUI.DrawTexture(new Rect(knobPos.x - knob / 2, knobPos.y - knob / 2, knob, knob), joystickKnobTex);
            }

            // PUBG/COD Mobile Semi-Transparent Vector Controls
            // Left & Right Fire with fiery combustion flare on active press
            ModernHUDButton(MobileControl.LeftFire, LeftFireRect, iconBullet, new Color(1.0f, 0.4f, 0.35f, 0.85f), leftFireFinger >= 0 || mouseActive == MouseControlKind.LeftFire, "FIRE", isFire: true);
            ModernHUDButton(MobileControl.RightFire, RightFireRect, iconBullet, new Color(1.0f, 0.3f, 0.25f, 0.95f), rightFireFinger >= 0 || mouseActive == MouseControlKind.RightFire, "AIM/FIRE", isFire: true);

            bool isScoped = ShowHoldBreath;

            // Dedicated Tactical Combat Knife / Stealth Takedown Button (only active when unscoped)
            if (!isScoped)
            {
                if (KnifeKillAvailable)
                {
                    float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 9f);
                    Rect pulseRect = new Rect(KnifeKillRect.x - (KnifeKillRect.width * (pulse - 1f) * 0.5f), KnifeKillRect.y - (KnifeKillRect.height * (pulse - 1f) * 0.5f), KnifeKillRect.width * pulse, KnifeKillRect.height * pulse);
                    ModernHUDButton(MobileControl.Knife, pulseRect, iconKnife, new Color(1.0f, 0.22f, 0.22f, 0.98f), true, "TAKEDOWN", isFire: true);
                }
                else
                {
                    ModernHUDButton(MobileControl.Knife, KnifeKillRect, iconKnife, new Color(0.88f, 0.92f, 0.98f, 0.85f), KnifeKillPressed, "KNIFE");
                }
            }

            // Scope Optic (Toggles ADS / UNSCOPE)
            ModernHUDButton(MobileControl.Scope, ScopeRect, iconScope, isScoped ? TacticalGUI.AccentGold : TacticalGUI.AccentCyan, ScopePressed, isScoped ? "UNSCOPE" : "ADS");

            // Stance Controls (Crouch & Jump)
            ModernHUDButton(MobileControl.Crouch, CrouchRect, iconCrouch, IsCrouching ? TacticalGUI.AccentCyan : new Color(0.92f, 0.94f, 0.98f, 0.85f), IsCrouching, IsCrouching ? "CROUCHED" : "CROUCH");
            if (!isScoped) ModernHUDButton(MobileControl.Jump, JumpRect, iconJump, new Color(0.92f, 0.94f, 0.98f, 0.85f), JumpPressed, "JUMP");

            // Reload & Sprint
            ModernHUDButton(MobileControl.Reload, ReloadRect, iconReload, new Color(0.92f, 0.94f, 0.98f, 0.85f), ReloadPressed, "RELOAD");
            if (!isScoped) ModernHUDButton(MobileControl.Sprint, SprintRect, iconSprint, Sprint ? TacticalGUI.AccentCyan : new Color(0.75f, 0.8f, 0.9f, 0.75f), Sprint, Sprint ? "LOCKED" : "SPRINT");

            // Hold Breath / Focus Button & FLIR Thermal Optic
            if (isScoped)
            {
                ModernHUDButton(MobileControl.Breath, BreathRect, iconBreath, HoldBreathHeld ? TacticalGUI.AccentGold : TacticalGUI.AccentCyan, HoldBreathHeld, "BREATH");
                ModernHUDButton(MobileControl.FLIR, FLIRRect, iconFLIR, FLIRActive ? TacticalGUI.AccentGold : TacticalGUI.AccentCyan, FLIRPressed || FLIRActive, FLIRActive ? "FLIR ON" : "FLIR");
            }

            // Only show weapon switching when the player owns multiple rifles and is unscoped.
            // A single starter rifle gets a clean compact status label instead.
            if (!isScoped)
            {
                if (OwnedWeaponCount() > 1)
                {
                    int dock0=DockWeapon(0);if(WeaponOwned(dock0)) ModernWeaponSlot(WeaponSlot1Rect, WeaponSlotNames[dock0], dock0, SelectedWeaponIndex==dock0, WeaponSlotAmmo[dock0]);
                    int dock1=DockWeapon(1);if(WeaponOwned(dock1)) ModernWeaponSlot(WeaponSlot2Rect, WeaponSlotNames[dock1], dock1, SelectedWeaponIndex==dock1, WeaponSlotAmmo[dock1]);
                    int dock2=DockWeapon(2);if(WeaponOwned(dock2)) ModernWeaponSlot(WeaponSlot3Rect, WeaponSlotNames[dock2], dock2, SelectedWeaponIndex==dock2, WeaponSlotAmmo[dock2]);
                }
                else
                {
                    var singleStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = TacticalGUI.AccentCyan } };
                    GUI.color = WithOpacity(Color.white, MobileControlLayout.Opacity(MobileControl.WeaponDock));
                    GUI.Label(MobileControlLayout.GetRect(MobileControl.WeaponDock), WeaponSlotNames[Mathf.Clamp(SelectedWeaponIndex, 0, WeaponCatalog.Count-1)] + "  //  EQUIPPED", singleStyle);
                    GUI.color = Color.white;
                }
            }

            GUI.matrix = previous;
        }
    }
}
