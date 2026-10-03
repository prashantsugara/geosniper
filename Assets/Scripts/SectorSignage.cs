using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    public static class SectorSignage
    {
        public static void BuildPlaces(SectorWorld world, Material face, Material post)
        {
            var captions = new Dictionary<MapFeature, List<string>>();
            var faces = new Dictionary<MapFeature, Material>();
            var categoryFaces=new Dictionary<string,Material>();
            Material PlaceFace(MapFeature place)
            {
                string category=MapFeatureStyle.Category(place);
                if(!categoryFaces.TryGetValue(category,out var material))
                {
                    material=world.SignMaterial(MapFeatureStyle.PlaceColor(place)); categoryFaces[category]=material;
                }
                return material;
            }
            foreach (var building in world.Buildings.Keys)
            {
                var lines = new List<string>();
                AddCaption(lines, Caption(building));
                captions.Add(building, lines);
            }
            var standalone = new List<MapFeature>();
            foreach (var place in world.Features)
            {
                if (place.Kind != "place" || string.IsNullOrWhiteSpace(Caption(place))) continue;
                Vector2 point = Center(place.Points);
                MapFeature host = FindHost(place, world.Buildings.Keys, world.Features);
                if (host != null) { AddCaption(captions[host], Caption(place)); if(!faces.ContainsKey(host)) faces[host]=PlaceFace(place); }
                else if(!IsShop(place)) standalone.Add(place);
            }
            foreach (var pair in captions)
            {
                if (pair.Value.Count == 0) continue;
                BuildFacade(world, pair.Key, string.Join("\n", pair.Value), faces.TryGetValue(pair.Key,out var categoryFace)?categoryFace:face);
            }
            var placed = new List<Vector2>();
            foreach (var place in standalone)
            {
                Vector2 point = Center(place.Points);
                if (placed.Exists(p => (p - point).sqrMagnitude < 4)) continue;
                var direction = NearestRoad(world.Features, point) - point;
                if (direction.sqrMagnitude < .01f) direction = Vector2.up;
                var position = To3(point, world.Ground(point.x, point.y) + 2.4f);
                if (Occupied(world, position, new Vector2(3.5f, 1.25f), direction)) continue;
                var sign = WorldSign.Create(world.transform, Caption(place), position,
                    Quaternion.LookRotation(To3(direction, 0)), new Vector2(3.5f, 1.25f), PlaceFace(place));
                AddPosts(world, sign, post);
                placed.Add(point);
            }
        }

        public static bool IsShop(MapFeature place)
        {
            string category=MapFeatureStyle.Text(place,"category");
            // Overture's generic fallback landmark also labels non-retail places as shops.
            // Prefer the actual category whenever it is available.
            return category.EndsWith("_store") || category.EndsWith("_shop") ||
                category=="shopping" || category=="retail" || category=="supermarket" ||
                category=="grocery" || category=="pharmacy" || category=="bakery" ||
                category=="convenience" || (category.Length==0 && place.Landmark=="shop");
        }

        public static MapFeature FindHost(MapFeature place,IEnumerable<MapFeature> buildings,List<MapFeature> features)
        {
            if(place.Points==null || place.Points.Count==0)return null;
            Vector2 point=Center(place.Points);
            MapFeature host=null;float nearest=36f;
            foreach(var building in buildings)
            {
                if(building.Kind!="building" || building.Points.Count<3 ||
                    WorldDetailPlanner.IsBuildingPart(building) || MapFeatureStyle.Number(building,"min_height",0)>.3f)continue;
                if(Contains(point,building.Points))return building;
                for(int i=0;i<building.Points.Count;i++)
                {
                    var wall=Closest(point,building.Points[i],building.Points[(i+1)%building.Points.Count]);
                    float distance=(point-wall).sqrMagnitude;
                    if(distance<nearest && !CrossesRoad(features,point,wall)){nearest=distance;host=building;}
                }
            }
            return host;
        }

        static void AddCaption(List<string> captions, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (!captions.Exists(c => string.Equals(c, value, System.StringComparison.OrdinalIgnoreCase))) captions.Add(value);
        }

        static string Caption(MapFeature feature)
        {
            if (!string.IsNullOrWhiteSpace(feature.Name))
            {
                string category=MapFeatureStyle.Category(feature);
                return feature.Name.Trim()+(category.Length>0 && feature.Kind=="place"?"\n"+category:"");
            }
            return feature.Landmark == "fuel" ? "Petrol pump" : feature.Landmark == "police" ? "Police station" : feature.Landmark == "shop" ? "Shop" : "";
        }

        static void BuildFacade(SectorWorld world, MapFeature building, string caption, Material face)
        {
            float best = float.MaxValue;
            Vector3 selected = default;
            Vector2 normal = default, size = default;
            var points = building.Points;
            float area = 0;
            for (int i = 0; i < points.Count; i++) area += Cross(points[i], points[(i + 1) % points.Count]);
            int lines = caption.Split('\n').Length;
            float panelHeight = Mathf.Min(Mathf.Max(1.15f, lines * .5f + .25f), Mathf.Max(1.15f, building.Height - .8f));
            float height = world.Buildings[building].transform.localPosition.y + Mathf.Min(3f, Mathf.Max(3, building.Height) - panelHeight / 2 - .25f);
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 a = points[i], b = points[(i + 1) % points.Count], delta = b - a;
                float length = delta.magnitude;
                if (length < 2) continue;
                Vector2 outward = new Vector2(delta.y, -delta.x).normalized * (area >= 0 ? 1 : -1);
                Vector2 midpoint = (a + b) / 2, point = midpoint + outward * .4f;
                if (Contains(point, points)) continue;
                Vector2 panelSize = new Vector2(Mathf.Min(7.5f, length - .6f), panelHeight);
                Vector3 position = To3(point, height);
                if (Occupied(world, position, panelSize, outward)) continue;
                Vector2 road = NearestRoad(world.Features, midpoint), toRoad = road - midpoint;
                float score = toRoad.magnitude + (Vector2.Dot(toRoad, outward) < 0 ? 1000 : 0) - panelSize.x;
                if (score >= best) continue;
                best = score; selected = position; normal = outward; size = panelSize;
            }
            if (best == float.MaxValue) return;
            var sign = WorldSign.Create(world.transform, caption, selected, Quaternion.LookRotation(To3(normal, 0)), size, face, false);
            // Keep ownership explicit for validation and future building streaming.
            sign.transform.SetParent(world.Buildings[building].transform, true);
        }

        public static void BuildRoads(SectorWorld world, List<MapFeature> features, Material face, Material post)
        {
            var placed = new List<Vector3>();
            var names = new List<string>();
            foreach (var road in features)
            {
                if (road.Kind != "road" || string.IsNullOrWhiteSpace(road.Name)) continue;
                float total = 0;
                for (int i = 1; i < road.Points.Count; i++) total += Vector2.Distance(road.Points[i - 1], road.Points[i]);
                if (total < 2) continue;
                float next = Mathf.Min(total / 2, 18), travelled = 0;
                for (int i = 1; i < road.Points.Count; i++)
                {
                    Vector2 a = road.Points[i - 1], b = road.Points[i], delta = b - a;
                    float length = delta.magnitude;
                    if (length < .001f) continue;
                    while (next <= travelled + length)
                    {
                        Vector2 point = Vector2.Lerp(a, b, (next - travelled) / length);
                        next += 55;
                        Vector2 along = delta / length, side = new Vector2(along.y, -along.x);
                        bool found = false;
                        float verge=MapFeatureStyle.RoadWidth(road)*.5f+2.5f;
                        foreach (float offset in new[] { verge, -verge, verge+2, -verge-2 })
                        {
                            Vector2 roadside = point + side * offset;
                            var position = To3(roadside, world.Ground(roadside.x, roadside.y) + 2.5f);
                            var size = new Vector2(3.4f, 1f);
                            bool crowded = false;
                            for (int p = 0; p < placed.Count; p++)
                                if ((position - placed[p]).sqrMagnitude < (names[p] == road.Name ? 45 * 45 : 12 * 12)) { crowded = true; break; }
                            if (crowded || Occupied(world, position, size, along)) continue;
                            // The caption belongs to this exact OSM way; nearby road names are never substituted.
                            var sign = WorldSign.Create(world.transform, road.Name, position, Quaternion.LookRotation(To3(along, 0)), size, face);
                            AddPosts(world, sign, post);
                            placed.Add(position); names.Add(road.Name); found = true;
                            break;
                        }
                        if (found && placed.Count >= 160) return;
                    }
                    travelled += length;
                }
            }
        }

        static bool Occupied(SectorWorld world, Vector3 position, Vector2 size, Vector2 forward)
        {
            Quaternion rotation = world.transform.rotation * Quaternion.LookRotation(To3(forward, 0));
            return Physics.CheckBox(world.transform.TransformPoint(position), new Vector3(size.x / 2, size.y / 2, WorldSign.Thickness / 2 + .02f), rotation, ~0, QueryTriggerInteraction.Ignore);
        }

        static void AddPosts(SectorWorld world, WorldSign sign, Material material)
        {
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 basePoint = sign.transform.TransformPoint(new Vector3(side * sign.PanelSize.x * .32f, 0, 0));
                Vector3 local = world.transform.InverseTransformPoint(basePoint);
                float ground = world.transform.TransformPoint(new Vector3(local.x, world.Ground(local.x, local.z), local.z)).y;
                float top = sign.transform.position.y - sign.PanelSize.y / 2;
                float height = Mathf.Max(.1f, top - ground);
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = "Sign support";
                post.transform.SetParent(sign.transform, false);
                post.transform.localPosition = new Vector3(side * sign.PanelSize.x * .32f, -sign.PanelSize.y / 2 - height / 2, 0);
                post.transform.localScale = new Vector3(.1f, height, .1f);
                post.GetComponent<Renderer>().sharedMaterial = material;
                post.GetComponent<Collider>().enabled = false;
            }
        }

        static bool CrossesRoad(List<MapFeature> features, Vector2 a, Vector2 b)
        {
            foreach (var road in features)
                if (road.Kind == "road") for (int i = 1; i < road.Points.Count; i++)
                {
                    Vector2 c = road.Points[i - 1], d = road.Points[i];
                    if (Cross(b - a, c - a) * Cross(b - a, d - a) < 0 && Cross(d - c, a - c) * Cross(d - c, b - c) < 0) return true;
                }
            return false;
        }

        static Vector2 NearestRoad(List<MapFeature> features, Vector2 point)
        {
            float best = float.MaxValue;
            Vector2 result = point + Vector2.up;
            foreach (var road in features)
                if (road.Kind == "road") for (int i = 1; i < road.Points.Count; i++)
                {
                    Vector2 candidate = Closest(point, road.Points[i - 1], road.Points[i]);
                    float distance = (candidate - point).sqrMagnitude;
                    if (distance < best) { best = distance; result = candidate; }
                }
            return result;
        }

        public static Vector2 Center(List<Vector2> points)
        {
            Vector2 center = Vector2.zero;
            foreach (var point in points) center += point;
            return center / Mathf.Max(1, points.Count);
        }
        static Vector2 Closest(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 delta = b - a;
            return a + delta * (delta.sqrMagnitude < .0001f ? 0 : Mathf.Clamp01(Vector2.Dot(p - a, delta) / delta.sqrMagnitude));
        }
        public static bool Contains(Vector2 point, List<Vector2> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }
        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        static Vector3 To3(Vector2 point, float y) => new Vector3(point.x, y, point.y);
    }
}
