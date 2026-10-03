using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    public static class GameplayMapRaster
    {
        const int Size = 1024;

        public static Texture2D Render(Vector3 origin, float span)
        {
            var pixels = new Color32[Size * Size];
            float scale = Size / span;

            // 1. Light street-map canvas
            Color32 bgCanvas = new Color32(242, 244, 239, 255);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bgCanvas;

            // 2. Sector ground (slightly lighter dark)
            foreach (var world in SectorWorld.LoadedWorlds)
            {
                if (world == null) continue;
                var corners = new List<Vector2>();
                foreach (var p in new[] { new Vector2(-320, -320), new Vector2(320, -320), new Vector2(320, 320), new Vector2(-320, 320) })
                    corners.Add(Project(world, p, origin, scale));
                Fill(pixels, corners, new Color32(242, 244, 239, 255));
            }

            // 3. Layered Overture Vector Map Elements (Land Use -> Water -> Park -> Building -> Road)

            // Layer A: Water Bodies (Soft Vector Blue #AADAFF)
            foreach (var world in SectorWorld.LoadedWorlds)
            {
                if (world == null) continue;
                foreach (var f in world.Features)
                {
                    if (f.Kind != "water" || f.Points.Count < 2) continue;
                    if(f.WaterRings.Count>0)
                    {
                        try {
                            var triangles=WaterGeometry.Triangles(f.WaterRings);
                            for(int i=0;i<triangles.Count;i+=3)
                                Fill(pixels,ProjectPoints(world,triangles.GetRange(i,3),origin,scale),new Color(.67f,.83f,.95f));
                        } catch(System.FormatException) { }
                    }
                    else {
                        var points = ProjectPoints(world, f.Points, origin, scale);
                        Fill(pixels, points, new Color(.67f, .83f,.95f));
                    }
                }
            }

            // Layer B: Park & Special Land Use Polygons
            foreach (var world in SectorWorld.LoadedWorlds)
            {
                if (world == null) continue;
                foreach (var f in world.Features)
                {
                    if (f.Points.Count < 2) continue;
                    var points = ProjectPoints(world, f.Points, origin, scale);
                    string cls = MapFeatureStyle.Text(f, "class");
                    
                    if (f.Kind == "park" || cls == "park" || cls == "forest" || cls == "grass")
                    {
                        Fill(pixels, points, new Color(.78f, .88f, .73f));
                    }
                    else if (cls == "military" || cls == "hospital" || cls == "government")
                    {
                        Fill(pixels, points, new Color(.94f, .90f, .85f));
                    }
                }
            }

            // Layer C: Building Footprints
            foreach (var world in SectorWorld.LoadedWorlds)
            {
                if (world == null) continue;
                foreach (var f in world.Features)
                {
                    if (f.Kind != "building" || f.Points.Count < 2) continue;
                    var points = ProjectPoints(world, f.Points, origin, scale);

                    var shadowPoints = new List<Vector2>();
                    float shadowOffset = Mathf.Clamp(f.Height * 0.1f * scale, 1.5f, 5f);
                    foreach (var p in points) shadowPoints.Add(p + new Vector2(shadowOffset, -shadowOffset));
                    Fill(pixels, shadowPoints, new Color(.84f, .85f, .82f));
                    Fill(pixels, points, new Color(.89f, .89f, .86f));

                    for (int i = 0; i < points.Count; i++)
                    {
                        Vector2 p1 = points[i];
                        Vector2 p2 = points[(i + 1) % points.Count];
                        Line(pixels, p1, p2, 1.0f, new Color(.79f, .80f, .77f));
                    }
                }
            }

            // Layer D: Overture Road Network (Highways, Avenues, Streets)
            foreach (var world in SectorWorld.LoadedWorlds)
            {
                if (world == null) continue;
                foreach (var f in world.Features)
                {
                    if (f.Kind != "road" || f.Points.Count < 2) continue;
                    var points = ProjectPoints(world, f.Points, origin, scale);
                    bool isPath = MapFeatureStyle.Path(f);
                    float rWidth = Mathf.Max(2.0f, MapFeatureStyle.RoadWidth(f) * scale);
                    string roadClass = MapFeatureStyle.Text(f, "class");
                    
                    bool isHighway = roadClass == "motorway" || roadClass == "trunk" || roadClass == "primary";
                    bool isAvenue = roadClass == "secondary" || roadClass == "tertiary";

                    if (isPath)
                    {
                        Color pathColor = new Color(.78f, .76f, .71f);
                        for (int i = 1; i < points.Count; i++)
                            Line(pixels, points[i - 1], points[i], rWidth * 0.5f, pathColor);
                    }
                    else if (isHighway)
                    {
                        Color casing = new Color(.84f, .73f, .49f);
                        for (int i = 1; i < points.Count; i++)
                            Line(pixels, points[i - 1], points[i], rWidth * 0.8f + 1.5f, casing);

                        Color fill = new Color(1f, .89f, .61f);
                        for (int i = 1; i < points.Count; i++)
                            Line(pixels, points[i - 1], points[i], rWidth * 0.8f, fill);
                    }
                    else if (isAvenue)
                    {
                        Color casing = new Color(.82f, .82f, .77f);
                        for (int i = 1; i < points.Count; i++)
                            Line(pixels, points[i - 1], points[i], rWidth * 0.6f + 1f, casing);

                        Color fill = new Color(1f, .98f, .88f);
                        for (int i = 1; i < points.Count; i++)
                            Line(pixels, points[i - 1], points[i], rWidth * 0.6f, fill);
                    }
                    else
                    {
                        Color fill = Color.white;
                        for (int i = 1; i < points.Count; i++)
                            Line(pixels, points[i - 1], points[i], rWidth * 0.5f, fill);
                    }
                }
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "Loaded Overture Vector Map",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static List<Vector2> ProjectPoints(SectorWorld world, List<Vector2> points, Vector3 origin, float scale)
        {
            var res = new List<Vector2>(points.Count);
            foreach (var p in points) res.Add(Project(world, p, origin, scale));
            return res;
        }

        static List<Vector2> ExpandPolygon(List<Vector2> points, float amount)
        {
            if (points.Count < 3) return points;
            Vector2 centroid = Vector2.zero;
            foreach (var p in points) centroid += p;
            centroid /= points.Count;

            var expanded = new List<Vector2>(points.Count);
            foreach (var p in points)
            {
                Vector2 dir = (p - centroid).normalized;
                expanded.Add(p + dir * amount);
            }
            return expanded;
        }

        static Vector2 Project(SectorWorld world, Vector2 p, Vector3 origin, float scale)
        {
            var v = world.transform.TransformPoint(new Vector3(p.x, 0, p.y)) - origin;
            return new Vector2(Size * .5f + v.x * scale, Size * .5f + v.z * scale);
        }

        static void Fill(Color32[] pixels, List<Vector2> points, Color color)
        {
            if (points.Count < 3) return;
            float low = Size, high = 0;
            foreach (var p in points) { low = Mathf.Min(low, p.y); high = Mathf.Max(high, p.y); }
            var intersections = new List<float>();
            for (int y = Mathf.Max(0, Mathf.FloorToInt(low)); y <= Mathf.Min(Size - 1, Mathf.CeilToInt(high)); y++)
            {
                intersections.Clear(); float scan = y + .5f;
                for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
                {
                    var a = points[j]; var b = points[i];
                    if ((a.y > scan) != (b.y > scan)) intersections.Add(a.x + (scan - a.y) * (b.x - a.x) / (b.y - a.y));
                }
                intersections.Sort();
                for (int i = 1; i < intersections.Count; i += 2)
                {
                    int xStart = Mathf.Max(0, Mathf.CeilToInt(intersections[i - 1]));
                    int xEnd = Mathf.Min(Size - 1, Mathf.FloorToInt(intersections[i]));
                    int row = y * Size;
                    for (int x = xStart; x <= xEnd; x++) pixels[row + x] = color;
                }
            }
        }

        static void Line(Color32[] pixels, Vector2 a, Vector2 b, float width, Color color)
        {
            if (Mathf.Max(a.x, b.x) < 0 || Mathf.Min(a.x, b.x) >= Size || Mathf.Max(a.y, b.y) < 0 || Mathf.Min(a.y, b.y) >= Size) return;
            int steps = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(a, b)), 1, 2048);
            int radius = Mathf.Clamp(Mathf.CeilToInt(width * .5f), 1, 30);
            for (int step = 0; step <= steps; step++)
            {
                var point = Vector2.Lerp(a, b, (float)step / steps);
                int cx = Mathf.RoundToInt(point.x), cy = Mathf.RoundToInt(point.y);
                for (int y = Mathf.Max(0, cy - radius); y <= Mathf.Min(Size - 1, cy + radius); y++)
                {
                    int row = y * Size;
                    for (int x = Mathf.Max(0, cx - radius); x <= Mathf.Min(Size - 1, cx + radius); x++)
                    {
                        if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius)
                            pixels[row + x] = color;
                    }
                }
            }
        }
    }
}
