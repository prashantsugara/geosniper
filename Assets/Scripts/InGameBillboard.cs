using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    /// <summary>
    /// Physical 3D In-Game Billboard component placed in the active gameplay world
    /// (rooftop gantries, street-level monopoles, and sidewalk kiosks).
    /// Renders authentic, high-definition real-world commercial brand displays (automotive,
    /// aerospace/defense, luxury watches, athletic energy) directly on 3D meshes with
    /// dynamic night emission and weather integration. Zero 2D UI popups during combat.
    /// </summary>
    public sealed class InGameBillboard : MonoBehaviour
    {
        public static readonly List<InGameBillboard> ActiveBillboards = new List<InGameBillboard>();

        [SerializeField] private MeshRenderer faceRenderer;
        private Material displayMaterial;
        private int brandIndex = 0;
        private bool lastNightState = false;

        private static Texture2D[] brandTextures;
        private static readonly Color[] brandEmissionTints = new Color[]
        {
            new Color(0.12f, 0.45f, 0.65f), // Titan Defense (Cyan/Steel)
            new Color(0.18f, 0.38f, 0.85f), // Aero Dynamics (Cobalt/Cyan)
            new Color(0.68f, 0.50f, 0.16f), // Chronos Genève (Warm Gold)
            new Color(0.38f, 0.72f, 0.12f)  // Volt Kinetic (Neon Lime)
        };

        public void Initialize(MeshRenderer renderer, int seed = 0)
        {
            faceRenderer = renderer;
            brandIndex = Mathf.Abs(seed) % 4;
            EnsureDisplayMaterial();
            ApplyBrandVisuals(SectorStreetLighting.IsNight);
        }

        private void OnEnable()
        {
            if (!ActiveBillboards.Contains(this))
                ActiveBillboards.Add(this);
            ApplyBrandVisuals(SectorStreetLighting.IsNight);
        }

        private void OnDisable()
        {
            ActiveBillboards.Remove(this);
        }

        private void OnDestroy()
        {
            ActiveBillboards.Remove(this);
        }

        private void Update()
        {
            bool isNight = SectorStreetLighting.IsNight;
            if (isNight != lastNightState)
            {
                lastNightState = isNight;
                ApplyBrandVisuals(isNight);
            }
        }

        private void EnsureDisplayMaterial()
        {
            if (faceRenderer == null) return;
            if (displayMaterial == null)
            {
                var shader = Shader.Find("Mobile/Diffuse") ?? Shader.Find("Diffuse") ?? Shader.Find("Standard");
                displayMaterial = new Material(shader) { name = "Billboard_Commercial_" + brandIndex };
                faceRenderer.material = displayMaterial;
            }
        }

        private void ApplyBrandVisuals(bool isNight)
        {
            EnsureDisplayMaterial();
            if (displayMaterial == null) return;
            EnsureBrandTextures();

            displayMaterial.mainTexture = brandTextures[brandIndex];
            displayMaterial.color = Color.white;

            if (displayMaterial.HasProperty("_EmissionColor"))
            {
                displayMaterial.EnableKeyword("_EMISSION");
                Color tint = brandEmissionTints[brandIndex];
                Color emission = isNight ? (tint * 1.45f) : (tint * 0.38f);
                displayMaterial.SetColor("_EmissionColor", emission);
            }
        }

        private static void EnsureBrandTextures()
        {
            if (brandTextures != null && brandTextures.Length == 4) return;
            brandTextures = new Texture2D[4];
            brandTextures[0] = GenerateTitanDefenseTexture();
            brandTextures[1] = GenerateAeroDynamicsTexture();
            brandTextures[2] = GenerateChronosTexture();
            brandTextures[3] = GenerateVoltKineticTexture();
        }

        // ── BRAND 0: TITAN DEFENSE & AEROSPACE ────────────────────────────────
        private static Texture2D GenerateTitanDefenseTexture()
        {
            const int W = 512, H = 256;
            var tex = CreateBaseTexture(W, H);
            var cols = new Color[W * H];

            Color darkSlate = new Color(0.06f, 0.08f, 0.11f);
            Color steelBlue = new Color(0.12f, 0.18f, 0.25f);
            Color cyanAccent = new Color(0.00f, 0.90f, 1.00f);
            Color amberAccent = new Color(1.00f, 0.72f, 0.12f);

            for (int y = 0; y < H; y++)
            {
                float v = (float)y / H;
                for (int x = 0; x < W; x++)
                {
                    float u = (float)x / W;
                    // Tactical carbon/grid gradient background
                    float grid = (x % 32 == 0 || y % 32 == 0) ? 0.08f : 0f;
                    Color c = Color.Lerp(darkSlate, steelBlue, v * 0.75f + grid);

                    // Central radar sweep rings & crosshair
                    float dx = (x - 256);
                    float dy = (y - 128);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    bool ring1 = Mathf.Abs(dist - 50f) < 1.8f;
                    bool ring2 = Mathf.Abs(dist - 90f) < 2.0f;
                    bool crossH = Mathf.Abs(dy) < 1.2f && dist < 105f;
                    bool crossV = Mathf.Abs(dx) < 1.2f && dist < 105f;
                    if (ring1 || ring2 || crossH || crossV)
                        c = Color.Lerp(c, cyanAccent, 0.85f);

                    // Dual chevron wings flanking the radar
                    bool leftChev = Mathf.Abs(dx + 160f + Mathf.Abs(dy) * 0.7f) < 5f && Mathf.Abs(dy) < 55f;
                    bool rightChev = Mathf.Abs(dx - 160f - Mathf.Abs(dy) * 0.7f) < 5f && Mathf.Abs(dy) < 55f;
                    if (leftChev || rightChev)
                        c = Color.Lerp(c, cyanAccent, 0.95f);

                    // Corporate branding top bar & bottom telemetry strip
                    if (y >= 210 && y <= 214 && x >= 40 && x <= 472) c = cyanAccent;
                    if (y >= 40 && y <= 44 && x >= 40 && x <= 472) c = amberAccent;

                    // Header & subheader block lettering bars
                    if (y >= 175 && y <= 195 && x >= 120 && x <= 392)
                    {
                        bool letterTick = (x % 16 < 11);
                        if (letterTick) c = Color.white;
                    }
                    if (y >= 55 && y <= 67 && x >= 90 && x <= 422)
                    {
                        bool tagTick = (x % 12 < 8);
                        if (tagTick) c = new Color(0.75f, 0.88f, 0.98f);
                    }

                    // Hazard corner stripes
                    if (x < 36 && y < 36 && ((x + y) / 6 % 2 == 0)) c = amberAccent;
                    if (x > 476 && y < 36 && ((x - y) / 6 % 2 == 0)) c = amberAccent;

                    cols[y * W + x] = ApplyFrameAndScanlines(x, y, W, H, c);
                }
            }

            tex.SetPixels(cols);
            tex.Apply(false);
            return tex;
        }

        // ── BRAND 1: AERO DYNAMICS (ELECTRIC HYPERCAR) ─────────────────────────
        private static Texture2D GenerateAeroDynamicsTexture()
        {
            const int W = 512, H = 256;
            var tex = CreateBaseTexture(W, H);
            var cols = new Color[W * H];

            Color deepSpace = new Color(0.03f, 0.05f, 0.12f);
            Color cobalt = new Color(0.08f, 0.22f, 0.48f);
            Color brightCyan = new Color(0.15f, 0.92f, 1.00f);
            Color horizonGlow = new Color(0.70f, 0.92f, 1.00f);

            for (int y = 0; y < H; y++)
            {
                float v = (float)y / H;
                for (int x = 0; x < W; x++)
                {
                    float u = (float)x / W;
                    // Sweeping aerodynamic horizon sky gradient
                    float horizon = Mathf.Exp(-Mathf.Pow((v - 0.45f) * 4f, 2));
                    Color c = Color.Lerp(deepSpace, cobalt, v * 0.9f) + horizonGlow * (horizon * 0.55f);

                    // Flowing wind-tunnel streamline curves (hypercar aerodynamics)
                    float streamline1 = Mathf.Sin(u * 7f + 0.3f) * 18f + 115f;
                    float streamline2 = Mathf.Sin(u * 6f - 0.4f) * 14f + 98f;
                    float streamline3 = Mathf.Sin(u * 8f + 1.2f) * 22f + 132f;
                    if (Mathf.Abs(y - streamline1) < 2.5f || Mathf.Abs(y - streamline2) < 2.0f || Mathf.Abs(y - streamline3) < 2.2f)
                        c = Color.Lerp(c, brightCyan, 0.90f);

                    // Sleek aerodynamic roofline curve
                    float carRoof = -Mathf.Pow((u - 0.52f) * 3.2f, 2) * 55f + 148f;
                    if (u >= 0.24f && u <= 0.80f && Mathf.Abs(y - carRoof) < 3.2f)
                        c = Color.white;

                    // High-tech typography bars
                    if (y >= 180 && y <= 202 && x >= 60 && x <= 452)
                    {
                        bool fontTick = (x % 18 < 13);
                        if (fontTick) c = Color.white;
                    }
                    if (y >= 50 && y <= 62 && x >= 140 && x <= 372)
                    {
                        bool tagTick = (x % 10 < 7);
                        if (tagTick) c = brightCyan;
                    }

                    // Dual side speed accents
                    if (y >= 80 && y <= 160 && (x == 50 || x == 54 || x == 458 || x == 462))
                        c = brightCyan;

                    cols[y * W + x] = ApplyFrameAndScanlines(x, y, W, H, c);
                }
            }

            tex.SetPixels(cols);
            tex.Apply(false);
            return tex;
        }

        // ── BRAND 2: CHRONOS GENÈVE (SWISS LUXURY CHRONOGRAPH) ─────────────────
        private static Texture2D GenerateChronosTexture()
        {
            const int W = 512, H = 256;
            var tex = CreateBaseTexture(W, H);
            var cols = new Color[W * H];

            Color obsidian = new Color(0.04f, 0.05f, 0.07f);
            Color charcoal = new Color(0.12f, 0.13f, 0.16f);
            Color pureGold = new Color(0.96f, 0.78f, 0.28f);
            Color warmBronze = new Color(0.72f, 0.52f, 0.18f);

            for (int y = 0; y < H; y++)
            {
                float v = (float)y / H;
                for (int x = 0; x < W; x++)
                {
                    // Dark radial guilloché pattern
                    float dx = (x - 256);
                    float dy = (y - 128);
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);
                    float sunburst = Mathf.Abs(Mathf.Sin(angle * 24f)) * 0.06f;

                    Color c = Color.Lerp(obsidian, charcoal, (dist / 220f) + sunburst);

                    // Concentric watch tachymeter ring & minute track
                    bool mainBezel = Mathf.Abs(dist - 98f) < 3.0f;
                    bool innerTrack = Mathf.Abs(dist - 84f) < 1.6f;
                    bool hourTick = Mathf.Abs(dist - 91f) < 6f && ((int)(angle * Mathf.Rad2Deg + 360f) % 30 < 3);

                    if (mainBezel || hourTick) c = Color.Lerp(c, pureGold, 0.95f);
                    else if (innerTrack) c = Color.Lerp(c, warmBronze, 0.75f);

                    // Auxiliary chronograph sub-dial rings (left & right)
                    float subLeft = Mathf.Sqrt(Mathf.Pow(dx + 42f, 2) + dy * dy);
                    float subRight = Mathf.Sqrt(Mathf.Pow(dx - 42f, 2) + dy * dy);
                    if (Mathf.Abs(subLeft - 24f) < 1.8f || Mathf.Abs(subRight - 24f) < 1.8f)
                        c = Color.Lerp(c, pureGold, 0.85f);

                    // Center chrono hands
                    bool handH = Mathf.Abs(dy) < 1.4f && dx >= -15f && dx <= 60f;
                    bool handV = Mathf.Abs(dx) < 1.4f && dy >= -10f && dy <= 72f;
                    if (handH || handV) c = Color.white;

                    // Brand Header: "CHRONOS GENÈVE" (Gold Typography Blocks)
                    if (y >= 195 && y <= 218 && x >= 100 && x <= 412)
                    {
                        bool fontTick = (x % 20 < 14);
                        if (fontTick) c = pureGold;
                    }
                    // Tagline: "SWISS CHRONOMETER • 1884"
                    if (y >= 38 && y <= 50 && x >= 130 && x <= 382)
                    {
                        bool tagTick = (x % 11 < 7);
                        if (tagTick) c = new Color(0.85f, 0.75f, 0.55f);
                    }

                    // Gold pinstripe framing
                    if ((y == 28 || y == 228) && x >= 32 && x <= 480) c = warmBronze;

                    cols[y * W + x] = ApplyFrameAndScanlines(x, y, W, H, c);
                }
            }

            tex.SetPixels(cols);
            tex.Apply(false);
            return tex;
        }

        // ── BRAND 3: VOLT KINETIC (EXTREME ENERGY & ATHLETICS) ────────────────
        private static Texture2D GenerateVoltKineticTexture()
        {
            const int W = 512, H = 256;
            var tex = CreateBaseTexture(W, H);
            var cols = new Color[W * H];

            Color deepBlack = new Color(0.04f, 0.04f, 0.05f);
            Color darkGreen = new Color(0.06f, 0.16f, 0.08f);
            Color neonLime = new Color(0.46f, 1.00f, 0.08f);
            Color electricCyan = new Color(0.00f, 0.95f, 1.00f);

            for (int y = 0; y < H; y++)
            {
                float v = (float)y / H;
                for (int x = 0; x < W; x++)
                {
                    float u = (float)x / W;
                    // Dynamic diagonal 45° angle split
                    float diag = (x * 0.75f - y);
                    Color c = (diag > 50) ? darkGreen : deepBlack;

                    // High-voltage lightning slash vector
                    float slash = Mathf.Abs(diag - 50);
                    if (slash < 7f) c = neonLime;
                    else if (slash < 14f) c = electricCyan;

                    // Athletic energy chevrons (triple power arrows)
                    for (int i = 0; i < 3; i++)
                    {
                        float cx = 110f + i * 45f;
                        float cy = 128f;
                        bool chev = Mathf.Abs((x - cx) - Mathf.Abs(y - cy) * 0.65f) < 4f && Mathf.Abs(y - cy) < 45f;
                        if (chev) c = (i == 2) ? electricCyan : neonLime;
                    }

                    // Brand Title block: "VOLT KINETIC"
                    if (y >= 140 && y <= 175 && x >= 240 && x <= 472)
                    {
                        bool fontTick = (x % 22 < 16);
                        if (fontTick) c = Color.white;
                    }

                    // Action Tagline: "UNLEASH THE LIMIT"
                    if (y >= 95 && y <= 112 && x >= 240 && x <= 452)
                    {
                        bool tagTick = (x % 14 < 10);
                        if (tagTick) c = neonLime;
                    }

                    // Technical voltage readouts / wattage pulse
                    if (y >= 52 && y <= 66 && x >= 60 && x <= 452)
                    {
                        bool barTick = (x % 18 < 9);
                        if (barTick) c = (x > 320) ? electricCyan : neonLime;
                    }

                    cols[y * W + x] = ApplyFrameAndScanlines(x, y, W, H, c);
                }
            }

            tex.SetPixels(cols);
            tex.Apply(false);
            return tex;
        }

        // ── COMMON BILLBOARD SHADER / SCANLINE / FRAME COMPOSITOR ──────────────
        private static Color ApplyFrameAndScanlines(int x, int y, int w, int h, Color content)
        {
            // 1. Outer metallic structural frame bezel
            bool outerBezel = (x < 6 || x >= w - 6 || y < 6 || y >= h - 6);
            if (outerBezel) return new Color(0.12f, 0.14f, 0.18f);

            bool innerBezel = (x < 10 || x >= w - 10 || y < 10 || y >= h - 10);
            if (innerBezel) return new Color(0.04f, 0.05f, 0.07f);

            // 2. Corner mounting bolts / rivets
            bool boltTL = Vector2.Distance(new Vector2(x, y), new Vector2(16, h - 16)) < 3.5f;
            bool boltTR = Vector2.Distance(new Vector2(x, y), new Vector2(w - 16, h - 16)) < 3.5f;
            bool boltBL = Vector2.Distance(new Vector2(x, y), new Vector2(16, 16)) < 3.5f;
            bool boltBR = Vector2.Distance(new Vector2(x, y), new Vector2(w - 16, 16)) < 3.5f;
            if (boltTL || boltTR || boltBL || boltBR) return new Color(0.75f, 0.82f, 0.90f);

            // 3. Authentic Outdoor Stadium LED Panel Scanline / Dot Grid
            bool scanline = (y % 4 == 0);
            if (scanline) content *= 0.88f;

            bool dotColumn = (x % 4 == 0);
            if (dotColumn) content *= 0.93f;

            return content;
        }

        private static Texture2D CreateBaseTexture(int w, int h)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }
    }
}
