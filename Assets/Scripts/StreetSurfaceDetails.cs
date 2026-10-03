using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    // One lightweight mesh per sector, with no extra colliders or per-marking objects.
    public static class StreetSurfaceDetails
    {
        struct JunctionEnd
        {
            public Vector2 Point, Inward;
            public float Width;
            public MapFeature Road;
        }

        public static Mesh Build(List<MapFeature> features, Func<float, float, float> ground, bool mobile)
        {
            var junctionMask=new RoadJunctionMask(features);
            var vertices = new List<Vector3>();
            var yellow = new List<int>();
            var white = new List<int>();
            var ends = new List<JunctionEnd>();
            int dashBudget = mobile ? 360 : 900;
            int edgeBudget = mobile ? 450 : 1100;
            foreach (var road in features)
            {
                if (road.Kind != "road" || road.Points == null || road.Points.Count < 2 || !MapFeatureStyle.PavedRoad(road)) continue;
                float width = MapFeatureStyle.RoadWidth(road);
                if (width < 5.5f) continue;
                for (int i = 1; i < road.Points.Count; i++)
                {
                    Vector2 a = road.Points[i - 1], b = road.Points[i];
                    float length = Vector2.Distance(a, b);
                    if (length < 2) continue;
                    Vector2 along = (b - a) / length, across = new Vector2(-along.y, along.x);
                    if (edgeBudget > 0 && length > 7)
                    {
                        // Sample the elevation in short stretches, matching the road mesh below.
                        int sections = Mathf.CeilToInt(length / 6f);
                        for (int s = 0; s < sections && edgeBudget > 0; s++)
                        {
                            float start = length * s / sections, end = length * (s + 1) / sections;
                            foreach (int side in new[] { -1, 1 })
                            {
                                Vector2 offset = across * side * (width * .5f - .42f);
                                Vector2 center = a + along * ((start + end) * .5f) + offset;
                                // A strip can cross a narrow side street even when its midpoint is clear.
                                // Keep paint out of the complete overlap, not just its centre sample.
                                if(!junctionMask.OccupiedAlong(a+along*start+offset,a+along*end+offset,road,along))
                                    Quad(vertices, white, center, along, end - start, .10f, ground);
                            }
                            edgeBudget--;
                        }
                    }
                    if (dashBudget > 0 && width >= 6f)
                        for (float distance = 7; distance + 3.5f < length && dashBudget > 0; distance += 13f)
                        {
                            if(!junctionMask.Occupied(a+along*distance,road,along))Quad(vertices, yellow, a + along * distance, along, 3.5f, .13f, ground);
                            dashBudget--;
                        }
                    AddEnd(a, b, width, road, ends);
                    AddEnd(b, a, width, road, ends);
                }
            }

            // Crossings appear only where two street ends meet at a meaningful angle.
            // A split in an otherwise straight OSM road is not an intersection.
            ends.Sort((a, b) => a.Point.sqrMagnitude.CompareTo(b.Point.sqrMagnitude));
            var cells = new Dictionary<Vector2Int, List<int>>();
            for (int i = 0; i < ends.Count; i++)
            {
                var cell = Cell(ends[i].Point);
                if (!cells.TryGetValue(cell, out var indices)) cells[cell] = indices = new List<int>();
                indices.Add(i);
            }
            int crossingBudget = mobile ? 24 : 64;
            for (int i = 0; i < ends.Count && crossingBudget > 0; i++)
            {
                var current = ends[i];
                bool junction = false;
                var centerCell = Cell(current.Point);
                for (int x = -1; x <= 1 && !junction; x++)
                    for (int y = -1; y <= 1 && !junction; y++)
                        if (cells.TryGetValue(centerCell + new Vector2Int(x, y), out var nearby))
                            foreach (int j in nearby)
                                if (j != i && ends[j].Road != current.Road &&
                                    (ends[j].Point - current.Point).sqrMagnitude < 9f &&
                                    Mathf.Abs(Vector2.Dot(ends[j].Inward, current.Inward)) < .72f)
                                { junction = true; break; }
                if (!junction) continue;
                Vector2 across = new Vector2(-current.Inward.y, current.Inward.x);
                int bars = Mathf.Clamp(Mathf.FloorToInt((current.Width - 1.2f) / .8f), 4, 18);
                for (int bar = 0; bar < bars; bar++)
                {
                    float offset = (bar - (bars - 1) * .5f) * .8f;
                    Quad(vertices, white, current.Point + current.Inward * 3f + across * offset,
                        current.Inward, 1.8f, .46f, ground);
                }
                crossingBudget--;
            }
            if (vertices.Count == 0) return null;
            var mesh = new Mesh { name = "Batched lane lines and crossings", subMeshCount = 2 };
            mesh.indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices); mesh.SetTriangles(yellow, 0); mesh.SetTriangles(white, 1); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        static void AddEnd(Vector2 point, Vector2 neighbor, float width, MapFeature road, List<JunctionEnd> ends)
        {
            Vector2 delta = neighbor - point;
            if (delta.sqrMagnitude < 16) return;
            ends.Add(new JunctionEnd { Point = point, Inward = delta.normalized, Width = width, Road = road });
        }

        static Vector2Int Cell(Vector2 point) => new Vector2Int(Mathf.FloorToInt(point.x / 3f), Mathf.FloorToInt(point.y / 3f));

        static void Quad(List<Vector3> vertices, List<int> triangles, Vector2 center, Vector2 along,
            float length, float width, Func<float, float, float> ground)
        {
            Vector2 across = new Vector2(-along.y, along.x);
            Vector2 a = center - along * (length * .5f) - across * (width * .5f);
            Vector2 b = center - along * (length * .5f) + across * (width * .5f);
            Vector2 c = center + along * (length * .5f) + across * (width * .5f);
            Vector2 d = center + along * (length * .5f) - across * (width * .5f);
            int start = vertices.Count;
            foreach (var p in new[] { a, b, c, d }) vertices.Add(new Vector3(p.x, ground(p.x, p.y) + .20f, p.y));
            triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
    }
}
