using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace GeoSniper
{
    public static class SectorMap
    {
        public const int MaxFeatures=2400;
        public const float GeometryLimit=960;
        public const int RequestTimeoutSeconds=30;
        public const int LoadTimeoutSeconds=240;
        public const int MaxResponseBytes=8*1024*1024;
        public static string LastSource { get; private set; }
        public static string LastFailure { get; private set; }
        static float retryDirectAfter;
        public static IEnumerator Load(GameLocation location, string endpoint, Action<List<MapFeature>, bool> complete, Action<string> status=null, bool refresh=false)
        {
            LastFailure="";
            
            if(MapServiceClient.UsesOverture)
            {
                List<MapFeature> overture=null;
                yield return MapServiceClient.LoadOverture(location,(features,source)=>{ overture=features; LastSource=source; },status,refresh);
                if(overture!=null)
                {
                    // Dense Overture sectors already contain the buildings and places we
                    // need. An extra Overpass request delays deployment and can fail on
                    // phones; supplement only genuinely sparse Overture coverage.
                    if(overture.FindAll(f=>f.Kind=="building").Count<30)
                    {
                        List<MapFeature> osm=null;
                        yield return MapServiceClient.LoadOsmSupplement(location,endpoint,(features,source)=>osm=features,status);
                        MergeSupplement(overture,osm);
                    }
                    
                    // Save the final map to SectorMapCache to allow jitter-reuse later
                    MapServiceClient.Save(overture, location);
                    LastSource=CoverageLabel(overture,LastSource); LastFailure="";
                    status?.Invoke(LastSource);
                    complete(overture,false);
                    yield break;
                }
                LastFailure=LastSource;
                status?.Invoke("OVERTURE UNAVAILABLE - TRYING DIRECT OSM");
            }

            // The general cache contains both OSM and Overture-derived sectors. It must
            // never bypass an available Overture tile or be labelled as Overture by guess.
            // Reuse only geographically matching data (small GPS jitter).
            var saved=refresh?null:MapServiceClient.ReadPrevious(location,TimeSpan.FromDays(90));
            if(saved!=null && saved.FindAll(f=>f.Kind=="building").Count>=5)
            {
                LastSource=CoverageLabel(saved,"CACHED LOCAL SECTOR");
                status?.Invoke(LastSource); complete(saved,false); yield break;
            }
            if(saved!=null) status?.Invoke("SPARSE SAVED SECTOR - CHECKING LIVE MAP DATA");

            if(!string.IsNullOrEmpty(MapServiceClient.Endpoint))
            {
                List<MapFeature> serviceMap=null;
                yield return MapServiceClient.Load(location,(features,source)=>{serviceMap=features; LastSource=source;},status,refresh);
                if(serviceMap!=null) { complete(serviceMap,false); yield break; }
                // A stopped local service should not force offline play when direct OSM is available.
                status?.Invoke("MAP SERVICE UNAVAILABLE - TRYING DIRECT OSM");
            }
            string query=BuildQuery(location);
            var servers=new List<string>();
            string[] pool = new string[] {
                endpoint,
                "https://lz4.overpass-api.de/api/interpreter",
                "https://z.overpass-api.de/api/interpreter",
                "https://overpass-api.de/api/interpreter"
            };
            foreach(string candidate in pool)
                if(!string.IsNullOrEmpty(candidate) && Uri.TryCreate(candidate,UriKind.Absolute,out var uri) && (uri.Scheme=="https" || uri.Scheme=="http") && !servers.Contains(candidate)) servers.Add(candidate);
            for(int attempt=0;attempt<servers.Count;attempt++)
            {
                while(Time.realtimeSinceStartup<retryDirectAfter)
                {
                    status?.Invoke("MAP SERVER BUSY - RETRY IN " + Mathf.CeilToInt(retryDirectAfter-Time.realtimeSinceStartup) + "s");
                    yield return new WaitForSecondsRealtime(1);
                }
                status?.Invoke("DOWNLOADING MAP " + (attempt+1) + " / " + servers.Count + " (UP TO " + RequestTimeoutSeconds + "s)");
                using(var request=new UnityWebRequest(servers[attempt],"POST"))
                {
                    request.uploadHandler=new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes("data="+Uri.EscapeDataString(query)));
                    request.downloadHandler=new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type","application/x-www-form-urlencoded");
                    request.SetRequestHeader("Accept","application/json");
                    request.SetRequestHeader("User-Agent","GeoSniperGame/2.0 (contact: geetasugara99@gmail.com; +https://github.com/geetasugara99)");
                    request.timeout=RequestTimeoutSeconds;
                    var operation=request.SendWebRequest();
                    while(!operation.isDone)
                    {
                        if(request.downloadedBytes>MaxResponseBytes) { request.Abort(); break; }
                        yield return null;
                    }
                    List<MapFeature> features=null;
                    string reason=null;
                    if(request.downloadedBytes>MaxResponseBytes) reason="MAP RESPONSE TOO LARGE";
                    else if(request.result==UnityWebRequest.Result.Success)
                    {
                        try { features=Parse(request.downloadHandler.text,location); }
                        catch(FormatException error) { reason=error.Message; }
                        catch(Exception) { reason="INVALID MAP RESPONSE"; }
                    }
                    if(features!=null && features.Count>0)
                    {
                        if(!features.Exists(f=>f.Kind=="road"))
                        {
                            features.Add(new MapFeature { Kind = "road", Name = location.Label ?? "Main Road", Height = 0, Points = new List<Vector2> { new Vector2(-250, 0), new Vector2(250, 0) } });
                            features.Add(new MapFeature { Kind = "road", Name = "Cross Street", Height = 0, Points = new List<Vector2> { new Vector2(0, -250), new Vector2(0, 250) } });
                        }
                        MapServiceClient.Save(features,location);
                        LastSource=CoverageLabel(features,"LIVE OSM"); LastFailure="";
                        complete(features,false); yield break;
                    }
                    reason=reason ?? (request.result==UnityWebRequest.Result.Success?"NO MAPPED ROADS AT THIS LOCATION":request.responseCode>0?"MAP SERVER HTTP "+request.responseCode:"MAP CONNECTION FAILED OR TIMED OUT");
                    LastFailure=reason;
                    status?.Invoke(reason);
                    Debug.LogWarning("OSM download failed: "+reason);
                    if(request.responseCode==429) retryDirectAfter=Time.realtimeSinceStartup+5;
                }
                if(attempt+1<servers.Count) yield return new WaitForSecondsRealtime(2);
            }
            saved=refresh?null:MapServiceClient.ReadPrevious(location,TimeSpan.FromDays(180));
            if(saved!=null)
            {
                LastSource=CoverageLabel(saved,"CACHED OSM (OLDER DATA)");
                status?.Invoke(LastSource); complete(saved,false); yield break;
            }
            // Only an explicit Offline Practice action may create fictional geography.
            LastSource="MAP UNAVAILABLE";
            status?.Invoke(LastFailure); complete(null,false);
        }

        public static string BuildQuery(GameLocation location)
        {
            string lat = location.Latitude.ToString("F6", CultureInfo.InvariantCulture);
            string lon = location.Longitude.ToString("F6", CultureInfo.InvariantCulture);
            // Cover the full 640 m square, including its corners.
            string area = "(around:455," + lat + "," + lon + ")";
            return "[out:json][timeout:30];(node[shop]" + area + ";node[amenity~\"^(fuel|police)$\"]" + area + ";way[building]" + area + ";way[shop]" + area + ";way[highway]" + area + ";way[amenity~\"^(fuel|police)$\"]" + area + ";way[leisure=park]" + area + ";way[landuse~\"^(forest|meadow|grass|orchard)$\"]" + area + ";way[natural~\"^(wood|scrub)$\"]" + area + ";way[natural=water]" + area + ";way[waterway~\"^(river|stream|canal)$\"]" + area + ";);out geom;";
        }

        static void MergeSupplement(List<MapFeature> primary,List<MapFeature> supplement)
        {
            if(supplement==null) return;
            foreach(var extra in supplement)
            {
                if(extra==null || extra.Points==null || extra.Points.Count==0) continue;
                MapFeature match=null;
                foreach(var existing in primary)
                    if(existing.Kind==extra.Kind && existing.Points.Count>0 && (SectorSignage.Center(existing.Points)-SectorSignage.Center(extra.Points)).sqrMagnitude<36) { match=existing; break; }
                if(match==null) { if(primary.Count<MaxFeatures) primary.Add(extra); continue; }
                if(string.IsNullOrWhiteSpace(match.Name)) match.Name=extra.Name;
                if(string.IsNullOrWhiteSpace(match.Landmark)) match.Landmark=extra.Landmark;
                if(match.Details==null || !match.Details.HasValues) match.Details=extra.Details;
            }
        }

        public static List<MapFeature> Parse(string json, GameLocation center)
        {
            var result = new List<MapFeature>();
            var root=JObject.Parse(json);
            if(!(root["elements"] is JArray elements)) throw new FormatException("INVALID MAP RESPONSE");
            if(!string.IsNullOrWhiteSpace((string)root["remark"])) throw new FormatException("MAP SERVER RETURNED INCOMPLETE DATA");
            foreach (var element in elements)
            {
                try
                {
                var tags = element["tags"];
                if((string)element["type"]=="water_geometry")
                {
                    var rings=WaterGeometry.Decode(element["waterRings"]);
                    if(rings.Count>0) result.Add(new MapFeature{Kind="water",Name=ReadName(tags),Details=MapFeatureStyle.ReadDetails(tags?["_details"]),Points=new List<Vector2>(rings[0]),WaterRings=rings});
                    continue;
                }
                var geometry = element["geometry"] as JArray;
                bool node=(string)element["type"]=="node";
                if (tags == null || (!node && (geometry == null || geometry.Count < 2 || geometry.Count > (tags?["waterway"]!=null?WaterGeometry.MaxPoints:400)))) continue;
                // Keep public feature names only; omit private/restricted map features.
                if (tags["military"] != null) continue;
                string amenity=((string)tags["amenity"] ?? "").Trim().ToLowerInvariant();
                bool shop=tags["shop"] != null;
                bool landmark=amenity=="fuel" || amenity=="police" || amenity=="hospital" || amenity=="bank";
                bool isBuilding=landmark || shop || tags["building"] != null || tags["building_part"] != null;
                bool isWater=(string)tags["natural"] == "water" || tags["waterway"] != null;
                string kind = node ? "place" : isBuilding ? "building" : tags["highway"] != null ? "road" : isWater ? "water" : "park";
                if((string)tags["building"]=="no" && !landmark && !shop) continue;
                var feature = new MapFeature { Kind = kind, Name = ReadName(tags), Landmark = landmark?amenity:shop?"shop":"", Height = landmark?(amenity=="fuel"?4:8):12 };
                feature.Details=MapFeatureStyle.ReadDetails(tags["_details"]);
                // Preserve public OSM material and surface tags for the world renderer.
                foreach(var mapped in new[]{
                    new[]{"building:levels","num_floors"},new[]{"building:material","facade_material"},new[]{"building:colour","facade_color"},
                    new[]{"roof:material","roof_material"},new[]{"roof:colour","roof_color"},
                    new[]{"roof:shape","roof_shape"},new[]{"roof:height","roof_height"},
                    new[]{"surface","surface"},new[]{"width","width"},new[]{"bridge","is_bridge"},
                    new[]{"highway","class"},new[]{"oneway","oneway"},
                    new[]{"junction","junction"},new[]{"layer","level"},new[]{"driving_side","driving_side"},
                    new[]{"landuse","landuse"},new[]{"natural","natural"},new[]{"leisure","leisure"}})
                {
                    string value=(string)tags?[mapped[0]];
                    if(!string.IsNullOrWhiteSpace(value) && value.Length<=40)feature.Details[mapped[1]]=value.Trim();
                }
                if(node && string.IsNullOrWhiteSpace(feature.Name) && !landmark) continue;
                if (float.TryParse((string)tags["height"], NumberStyles.Float, CultureInfo.InvariantCulture, out float height)) feature.Height = Mathf.Clamp(height, 3, MapFeatureStyle.MaxBuildingHeight);
                else if (float.TryParse((string)tags["building:levels"], NumberStyles.Float, CultureInfo.InvariantCulture, out float levels)) feature.Height = Mathf.Clamp(levels * 3, 3, MapFeatureStyle.MaxBuildingHeight);
                if(float.IsNaN(feature.Height) || float.IsInfinity(feature.Height)) continue;
                bool outside = false;
                if(node)
                {
                    double latitude = (double)element["lat"], longitude = (double)element["lon"];
                    if (!LocationResolver.Valid(latitude, longitude)) continue;
                    var p = new Vector2((float)((longitude - center.Longitude) * Math.Cos(center.Latitude * Math.PI / 180) * 111320), (float)((latitude - center.Latitude) * 111320));
                    if (Math.Abs(p.x) > 320 || Math.Abs(p.y) > 320) outside=true;
                    else feature.Points.AddRange(new[]{p+new Vector2(-2,-2),p+new Vector2(2,-2),p+new Vector2(2,2),p+new Vector2(-2,2)});
                }
                else foreach (var point in geometry)
                {
                    double latitude = (double)point["lat"], longitude = (double)point["lon"];
                    if (!LocationResolver.Valid(latitude, longitude)) { outside = true; break; }
                    var p = new Vector2((float)((longitude - center.Longitude) * Math.Cos(center.Latitude * Math.PI / 180) * 111320), (float)((latitude - center.Latitude) * 111320));
                    if (kind!="road" && kind!="water" && kind!="park" && (Math.Abs(p.x) > GeometryLimit || Math.Abs(p.y) > GeometryLimit)) { outside = true; break; }
                    feature.Points.Add(p);
                }
                if (outside) continue;
                if(kind=="place") { result.Add(feature); continue; }
                if(kind=="road")
                {
                    MapFeature run=null;
                    for(int i=1;i<feature.Points.Count;i++)
                    {
                        var a=feature.Points[i-1]; var b=feature.Points[i];
                        if(!ClipRoad(ref a,ref b)) { run=null; continue; }
                        if(run==null || (run.Points[run.Points.Count-1]-a).sqrMagnitude>.001f)
                        {
                            run=new MapFeature{Kind="road",Name=feature.Name,Details=feature.Details,Points=new List<Vector2>{a}};
                            result.Add(run);
                        }
                        run.Points.Add(b);
                    }
                    continue;
                }
                if(kind=="water" && tags["waterway"]!=null && (string)tags["natural"]!="water")
                {
                    string channel=((string)tags["waterway"]??"").ToLowerInvariant();
                    float width=channel=="river"?10f:channel=="canal"?6f:3f;
                    if(float.TryParse((string)tags["width"],NumberStyles.Float,CultureInfo.InvariantCulture,out var mappedWidth))width=Mathf.Clamp(mappedWidth,2f,60f);
                    feature.Points=WaterGeometry.Ribbon(feature.Points,width);
                    if(feature.Points.Count>=3 && feature.Points.Count<=WaterGeometry.MaxPoints)
                    {
                        feature.WaterRings.Add(new List<Vector2>(feature.Points));
                        if(element["id"]!=null)feature.Details["water_id"]="osm:"+element["id"].ToString();
                        result.Add(feature);
                    }
                    continue;
                }
                if (kind != "road")
                {
                    if (feature.Points.Count < 4 || Vector2.Distance(feature.Points[0], feature.Points[feature.Points.Count - 1]) > .1f) continue;
                    feature.Points.RemoveAt(feature.Points.Count - 1);
                    if(kind=="building")
                    {
                        // A building crossing a sector boundary belongs to the sector containing its center.
                        // Dropping any polygon with one outside vertex left permanent gaps between sectors.
                        Vector2 owner=SectorSignage.Center(feature.Points);
                        if(owner.x < -320 || owner.x >= 320 || owner.y < -320 || owner.y >= 320) continue;
                    }
                }
                if(kind=="water")
                {
                    if(element["id"]!=null)feature.Details["water_id"]="osm:"+element["id"].ToString();
                    feature.Points=WaterGeometry.Clip(feature.Points);
                    if(feature.Points.Count<3)continue;
                    feature.WaterRings.Add(new List<Vector2>(feature.Points));
                }
                else if(kind=="park")
                {
                    feature.Points=WaterGeometry.Clip(feature.Points);
                    if(feature.Points.Count<3)continue;
                }
                if(SectorWorld.Triangulate(feature.Points).Count==0) continue;
                result.Add(feature);
                }
                catch(Exception error) when(error is FormatException || error is InvalidCastException || error is ArgumentException || error is OverflowException)
                {
                    // One malformed OSM feature must not discard the entire neighbourhood.
                }
            }
            // Overpass response order is not geographic order. Select nearest features first so
            // the generated world contains the same buildings beside the player as the OSM map.
            result.Sort((a,b)=>FeatureDistance(a).CompareTo(FeatureDistance(b)));
            var selected=new List<MapFeature>(); int buildings=0,roads=0,areas=0,places=0;
            foreach(var feature in result)
            {
                if(selected.Count>=MaxFeatures) break;
                if(feature.Kind=="building") { if(buildings>=1500) continue; buildings++; }
                else if(feature.Kind=="road") { if(roads>=750) continue; roads++; }
                else if(feature.Kind=="place") { if(places>=240) continue; places++; }
                else { if(areas>=140) continue; areas++; }
                selected.Add(feature);
            }
            return selected;
        }
        public static string ReadName(JToken tags)
        {
            foreach(string key in new[]{"name","name:en","official_name","short_name"})
            {
                string value=(string)tags[key];
                if(!string.IsNullOrWhiteSpace(value)) return value.Trim().Replace('\n',' ').Replace('\r',' ');
            }
            return "";
        }
        static float FeatureDistance(MapFeature feature)
        {
            if(feature.Points==null || feature.Points.Count==0) return float.MaxValue;
            Vector2 center=Vector2.zero;
            foreach(var point in feature.Points) center+=point;
            return (center/feature.Points.Count).sqrMagnitude;
        }
        public static string CoverageLabel(List<MapFeature> features,string source)
        {
            int buildings=features.FindAll(f=>f.Kind=="building").Count;
            return source+" | "+buildings+" mapped buildings"+(buildings<5?" | Sparse building coverage":"");
        }
        public static bool ClipRoad(ref Vector2 a,ref Vector2 b)
        {
            Vector2 delta=b-a; float enter=0,leave=1;
            for(int axis=0;axis<2;axis++)
            {
                float start=a[axis],direction=delta[axis];
                if(Mathf.Abs(direction)<.00001f) { if(Mathf.Abs(start)>320) return false; continue; }
                float lo=(-320-start)/direction,hi=(320-start)/direction;
                if(lo>hi) { float swap=lo; lo=hi; hi=swap; }
                enter=Mathf.Max(enter,lo); leave=Mathf.Min(leave,hi);
                if(enter>leave) return false;
            }
            b=a+delta*leave; a+=delta*enter; return (b-a).sqrMagnitude>.01f;
        }

        public static List<MapFeature> Offline()
        {
            var features = new List<MapFeature>();
            var random = new System.Random(72);
            for (int i = -2; i <= 2; i++)
            {
                features.Add(new MapFeature { Kind = "road", Points = new List<Vector2> { new Vector2(-250, i * 80), new Vector2(250, i * 80) } });
                features.Add(new MapFeature { Kind = "road", Points = new List<Vector2> { new Vector2(i * 80, -250), new Vector2(i * 80, 250) } });
            }
            for (int x = -2; x < 2; x++) for (int z = -2; z < 2; z++)
            {
                float a = x * 80 + 18, b = z * 80 + 18;
                features.Add(new MapFeature { Kind = x == 0 && z == 0 ? "park" : "building", Height = random.Next(8, 32), Points = new List<Vector2> { new Vector2(a,b), new Vector2(a+44,b), new Vector2(a+44,b+44), new Vector2(a,b+44) } });
            }
            return features;
        }
    }
}
