using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    // Picks visible detail around the actual entry point, independent of provider order.
    public static class WorldDetailPlanner
    {
        public static Vector2 Center(MapFeature feature)
        {
            if (feature?.Points == null || feature.Points.Count == 0) return Vector2.zero;
            Vector2 center = Vector2.zero;
            foreach (var point in feature.Points) center += point;
            return center / feature.Points.Count;
        }

        public static float RoadDistanceSquared(MapFeature road, Vector2 focus)
        {
            if (road?.Points == null || road.Points.Count < 2) return float.MaxValue;
            float nearest = float.MaxValue;
            for (int i = 1; i < road.Points.Count; i++)
            {
                Vector2 a = road.Points[i - 1], delta = road.Points[i] - a;
                float t = delta.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector2.Dot(focus - a, delta) / delta.sqrMagnitude) : 0;
                nearest = Mathf.Min(nearest, (focus - a - delta * t).sqrMagnitude);
            }
            return nearest;
        }

        public static bool IsBuildingPart(MapFeature feature) => MapFeatureStyle.Text(feature, "is_part") == "true";

        public static HashSet<MapFeature> SelectBuildings(List<MapFeature> features, Vector2 focus, int budget, float radius)
        {
            var candidates = new List<MapFeature>();
            foreach (var feature in features)
                if (feature.Kind == "building" && feature.Points != null && feature.Points.Count >= 3 &&
                    feature.Height >= 3 && !IsBuildingPart(feature) && (Center(feature) - focus).sqrMagnitude <= radius * radius)
                    candidates.Add(feature);
            candidates.Sort((a, b) => Score(a, focus).CompareTo(Score(b, focus)));
            var selected = new HashSet<MapFeature>();
            for (int i = 0; i < Mathf.Min(budget, candidates.Count); i++) selected.Add(candidates[i]);
            return selected;
        }

        static float Score(MapFeature feature, Vector2 focus)
        {
            float distance = Vector2.Distance(Center(feature), focus);
            // A mapped public landmark earns some detail without displacing a very close facade.
            if (!string.IsNullOrWhiteSpace(feature.Name) || !string.IsNullOrWhiteSpace(feature.Landmark)) distance -= 18f;
            return distance;
        }

        public static List<MapFeature> RoadsNearFocus(List<MapFeature> features, Vector2 focus)
        {
            var roads = features.FindAll(f => f.Kind == "road" && f.Points != null && f.Points.Count >= 2);
            roads.Sort((a, b) => RoadDistanceSquared(a, focus).CompareTo(RoadDistanceSquared(b, focus)));
            return roads;
        }
    }
}
