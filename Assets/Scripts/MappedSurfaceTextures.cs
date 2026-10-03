using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    // Small shared textures keep material variants mapped to source tags without shipping an atlas per building.
    public static class MappedSurfaceTextures
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Texture2D Facade(string material, bool windows)
        {
            string style = NormalizeFacade(material);
            string key = "facade:" + style + (windows ? ":windows" : ":plain");
            if (cache.TryGetValue(key, out var found)) return found;

            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = key };
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                float v = (float)y / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float grain = Mathf.PerlinNoise(x * 0.12f + 7f, y * 0.13f + 13f) * 0.07f - 0.035f;
                    float macroNoise = Mathf.PerlinNoise(x * 0.015f + 42f, y * 0.015f + 88f) * 0.09f;
                    float rainStreak = Mathf.PerlinNoise(x * 0.06f + 10f, y * 0.005f + 25f) * 0.05f;

                    Color wallColor;
                    switch (style)
                    {
                        case "brick":
                        {
                            // Authentic running bond bricks with recessed mortar
                            int row = y / 16;
                            int col = (x + (row % 2) * 16) / 32;
                            bool isMortar = (y % 16 < 3) || ((x + (row % 2) * 16) % 32 < 3);

                            if (isMortar)
                            {
                                float grit = ((x * 17 + y * 29) % 11) / 85f;
                                wallColor = new Color(0.60f + grit, 0.58f + grit, 0.55f + grit, 0.0f);
                            }
                            else
                            {
                                float brickHash = Mathf.PerlinNoise(col * 1.7f + 11f, row * 2.3f + 19f);
                                float tone = Mathf.Lerp(0.68f, 0.88f, brickHash) + grain * 0.4f;
                                wallColor = new Color(tone, tone * 0.72f, tone * 0.54f, 0.0f);
                            }
                            break;
                        }
                        case "stone":
                        {
                            // Ashlar cut stone blocks with beveled relief joints
                            int row = y / 32;
                            int col = (x + (row % 2) * 32) / 64;
                            bool isSeam = (y % 32 < 2) || ((x + (row % 2) * 32) % 64 < 2);
                            float stoneNoise = Mathf.PerlinNoise(x * 0.04f, y * 0.04f) * 0.12f;

                            if (isSeam)
                            {
                                wallColor = new Color(0.38f, 0.37f, 0.35f, 0.0f);
                            }
                            else
                            {
                                float tone = 0.77f + stoneNoise + grain;
                                wallColor = new Color(tone, tone * 0.96f, tone * 0.90f, 0.0f);
                            }
                            break;
                        }
                        case "glass":
                        {
                            // Curtain-wall skyscraper glass & aluminum mullions
                            bool isMullion = (x % 64 < 3) || (y % 64 < 3);
                            if (isMullion)
                            {
                                wallColor = new Color(0.12f, 0.13f, 0.15f, 0.45f);
                            }
                            else
                            {
                                float grad = Mathf.Lerp(0.09f, 0.22f, (float)(y % 64) / 64f);
                                wallColor = new Color(grad * 0.6f, grad * 0.9f, grad * 1.15f, 1.0f);
                            }
                            break;
                        }
                        case "wood":
                        {
                            bool isPlankSeam = (x % 28 < 2);
                            float woodGrain = Mathf.PerlinNoise(x * 0.035f, y * 0.22f) * 0.20f;
                            float tone = isPlankSeam ? 0.42f : 0.68f + woodGrain;
                            wallColor = new Color(tone, tone * 0.78f, tone * 0.55f, 0.0f);
                            break;
                        }
                        case "metal":
                        {
                            bool isPanel = (x % 64 < 2) || (y % 128 < 2);
                            float tone = isPanel ? 0.42f : 0.78f + grain;
                            wallColor = new Color(tone * 0.92f, tone * 0.96f, tone, 0.45f);
                            break;
                        }
                        default: // Concrete & Plaster / Stucco
                        {
                            // Architectural formwork joints and subtle weathering
                            bool isPanelJoint = (y % 128 < 3) || (y > 508);
                            int px = x % 128, py = y % 128;
                            bool isTieRod = ((px == 12 || px == 116) && (py == 12 || py == 116));
                            bool isBasePlinth = (y < 24);
                            bool isFloorTrim = (y % 256 < 6 || y % 256 > 250);

                            float tone = 0.78f + grain + macroNoise - rainStreak;
                            if (isBasePlinth) tone *= 0.65f;
                            else if (isFloorTrim) tone = (y % 256 < 3) ? tone * 0.70f : tone * 1.15f;
                            else if (isPanelJoint) tone *= 0.72f;
                            if (isTieRod) tone *= 0.45f;

                            wallColor = new Color(tone, tone * 0.98f, tone * 0.94f, 0.0f);
                            break;
                        }
                    }

                    if (windows && style != "glass")
                    {
                        // 2 window bays per 256px horizontal (4 bays across 512px)
                        // 2 window storeys per 256px vertical (2 rows in 512px)
                        int wx = x % 256;
                        int wy = y % 256;

                        bool isWindowOuter = (wx >= 40 && wx <= 216 && wy >= 34 && wy <= 222);
                        bool isWindowSill = (wx >= 36 && wx <= 220 && wy >= 26 && wy < 34);

                        if (isWindowSill)
                        {
                            // Architectural stone/concrete window sill ledge (Alpha 0.45 = Frame/Trim zone)
                            wallColor = new Color(0.38f, 0.38f, 0.40f, 0.45f);
                        }
                        else if (isWindowOuter)
                        {
                            bool isFrame = (wx < 48 || wx > 208 || wy < 42 || wy > 214 || (wx >= 125 && wx <= 131) || (wy >= 125 && wy <= 129));
                            if (isFrame)
                            {
                                // Dark powder-coated aluminum window frame & center mullion (Alpha 0.45 = Metal zone)
                                wallColor = new Color(0.10f, 0.11f, 0.13f, 0.45f);
                            }
                            else
                            {
                                // Optical Glass with interior depth (shades / curtains / warm lit rooms)
                                int glassX = (wx < 125) ? (wx - 48) : (wx - 131);
                                int glassY = wy - 42;
                                float glassU = (float)glassX / 77f;
                                float glassV = (float)glassY / 172f;

                                // Interior blinds on upper pane
                                bool hasBlinds = (wy > 130 && ((y / 256 + x / 256) % 3 == 0));
                                float blindSlat = hasBlinds ? ((glassY % 6 < 2) ? 0.32f : 0.46f) : 0.0f;

                                // Subtle curtain silhouette at sides
                                float curtain = (glassU < 0.18f || glassU > 0.82f) ? 0.16f : 0.0f;

                                float interiorShade = Mathf.Clamp01(0.12f + blindSlat + curtain);

                                // Semi-random warm room illumination at night
                                int roomId = (x / 256) * 19 + (y / 256) * 37;
                                bool warmInterior = (roomId % 3 == 0);

                                if (warmInterior)
                                {
                                    float warmTone = 0.55f + interiorShade * 0.45f;
                                    wallColor = new Color(warmTone, warmTone * 0.78f, warmTone * 0.42f, 1.0f);
                                }
                                else
                                {
                                    wallColor = new Color(interiorShade * 0.22f, interiorShade * 0.30f, interiorShade * 0.42f, 1.0f);
                                }
                            }
                        }
                    }

                    pixels[y * size + x] = wallColor;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(true, true);
            cache[key] = tex;
            return tex;
        }

        public static Texture2D WindowInteriors()
        {
            const string key = "window-interiors";
            if (cache.TryGetValue(key, out var found)) return found;

            var texture = new Texture2D(256, 128, TextureFormat.RGBA32, true) { name = key, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[256 * 128];
            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    int style = x / 64;
                    float u = (x % 64 + 0.5f) / 64f, v = (y + 0.5f) / 128f;
                    float edge = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Min(u, 1 - u) / 0.12f)) * Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Min(v, 1 - v) / 0.09f));
                    float light = 0.42f + 0.20f * v;

                    if (style == 0 || style == 1)
                    {
                        float curtain = style == 0 ? 0.30f : 0.46f;
                        if (u < curtain || u > 1 - curtain) light = 0.30f + 0.20f * (0.5f + 0.5f * Mathf.Cos(u * 90f));
                        else light *= 0.75f;
                    }
                    else if (style == 2)
                    {
                        light *= 0.65f + 0.35f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.1f, 0.45f, Mathf.Repeat(v * 16f, 1)));
                    }
                    else
                    {
                        light *= 0.55f + 0.45f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.18f, 0.65f, v));
                    }
                    light *= Mathf.Lerp(0.22f, 1, edge);
                    pixels[y * 256 + x] = new Color(light, light * 0.95f, light * 0.86f, 1);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            cache[key] = texture;
            return texture;
        }

        public static Texture2D Roof(string material)
        {
            string style = (material ?? "").ToLowerInvariant();
            if (style != "metal" && style != "tile" && style != "roof_tiles" && style != "slate") style = "tar";
            string key = "roof:" + style;
            if (cache.TryGetValue(key, out var found)) return found;

            const int size = 512;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, name = key };
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float noise = Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.12f;
                    float fineGrit = ((x * 37 + y * 53) % 19) / 250f;
                    float tone;

                    if (style == "metal")
                    {
                        // Standing-seam metal roof with sharp raised ribs every 64px
                        bool isSeam = (x % 64 < 3);
                        tone = isSeam ? 0.38f : 0.80f + noise;
                    }
                    else if (style == "slate")
                    {
                        // Overlapping slate shingles
                        int row = y / 32;
                        bool isSlateSeam = (y % 32 < 2) || ((x + (row % 2) * 24) % 64 < 2);
                        tone = isSlateSeam ? 0.45f : 0.74f + noise + fineGrit;
                    }
                    else if (style == "tile" || style == "roof_tiles")
                    {
                        // Terracotta clay barrel tiles
                        float curvedTile = Mathf.Sin((x % 32) / 32f * Mathf.PI);
                        tone = Mathf.Lerp(0.52f, 0.85f, curvedTile) + noise;
                    }
                    else
                    {
                        // Industrial bitumen tar gravel roof with bitumen seam bands
                        bool isBitumenSeam = (y % 128 < 4);
                        tone = isBitumenSeam ? 0.44f : 0.72f + noise + fineGrit;
                    }

                    pixels[y * size + x] = new Color(tone, tone, tone, 1.0f);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(true, true);
            cache[key] = tex;
            return tex;
        }

        static string NormalizeFacade(string value)
        {
            value = (value ?? "").ToLowerInvariant();
            if (value.Contains("brick")) return "brick";
            if (value.Contains("stone")) return "stone";
            if (value.Contains("wood") || value.Contains("timber")) return "wood";
            if (value.Contains("metal") || value.Contains("steel")) return "metal";
            if (value.Contains("glass")) return "glass";
            return "concrete";
        }
    }
}
