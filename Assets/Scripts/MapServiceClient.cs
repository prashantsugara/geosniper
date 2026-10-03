using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace GeoSniper
{
    public static class MapServiceClient
    {
        public static bool UsesOverture
        {
            get
            {
                try { return (string)JObject.Parse(Resources.Load<TextAsset>("MapServiceConfig").text)["provider"]=="overture"; }
                catch { return false; }
            }
        }

        public static bool UsesDirectOverture
        {
            get
            {
                try { return UsesOverture && (string)JObject.Parse(Resources.Load<TextAsset>("MapServiceConfig").text)["transport"]=="publicTiles"; }
                catch { return false; }
            }
        }

        static string OverturePath(GameLocation location) => Path.Combine(Application.persistentDataPath,UsesDirectOverture?"overture-direct-sectors-v1":"overture-sectors-v1","sector-"+SectorMapCache.Key(location)+".json");

        internal static JObject ReadOverture(GameLocation location)
        {
            try
            {
                var path=OverturePath(location);
                if(!File.Exists(path) || new FileInfo(path).Length>SectorMap.MaxResponseBytes) return null;
                var data=JObject.Parse(File.ReadAllText(path));
                if((string)data["provider"]!="overture" || string.IsNullOrEmpty((string)data["release"])) return null;
                Decode(data.ToString());
                return data;
            }
            catch { return null; }
        }

        internal static void SaveOverture(GameLocation location,JObject data)
        {
            try
            {
                string path=OverturePath(location), directory=Path.GetDirectoryName(path);
                Directory.CreateDirectory(directory);
                File.WriteAllText(path+".tmp",data.ToString(Newtonsoft.Json.Formatting.None));
                if(File.Exists(path)) File.Replace(path+".tmp",path,null);
                else File.Move(path+".tmp",path);
                var files=new DirectoryInfo(directory).GetFiles("sector-*.json");
                Array.Sort(files,(a,b)=>b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                long bytes=0;
                for(int i=0;i<files.Length;i++)
                {
                    bytes+=files[i].Length;
                    if(i>=32 || bytes>64L*1024*1024) files[i].Delete();
                }
            }
            catch(IOException) { }
            catch(UnauthorizedAccessException) { }
        }

        public static IEnumerator LoadOverture(GameLocation location,Action<List<MapFeature>,string> complete,Action<string> status,bool refresh=false)
        {
            if(UsesDirectOverture)
            {
                yield return OvertureDirect.Load(location,complete,status,refresh);
                yield break;
            }
            var cached=refresh?null:ReadOverture(location);
            string url=Endpoint;
            if(Uri.TryCreate(url,UriKind.Absolute,out var uri) && (uri.Scheme=="https" || uri.Scheme=="http"))
            {
                status?.Invoke("CHECKING OVERTURE RELEASE / LOADING SECTOR...");
                using(var request=new UnityWebRequest(url.TrimEnd('/')+"/v1/sector","POST"))
                {
                    var body=new JObject{["latitude"]=Math.Round(location.Latitude,6),["longitude"]=Math.Round(location.Longitude,6),["cachedRelease"]=cached?["release"]};
                    request.uploadHandler=new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body.ToString(Newtonsoft.Json.Formatting.None)));
                    request.downloadHandler=new DownloadHandlerBuffer(); request.timeout=210;
                    request.SetRequestHeader("Content-Type","application/json");
                    var operation=request.SendWebRequest();
                    while(!operation.isDone)
                    {
                        if(request.downloadedBytes>SectorMap.MaxResponseBytes) { request.Abort(); break; }
                        yield return null;
                    }
                    List<MapFeature> features=null; string source="";
                    if(request.result==UnityWebRequest.Result.Success && request.downloadedBytes<=SectorMap.MaxResponseBytes)
                    {
                        try
                        {
                            var data=JObject.Parse(request.downloadHandler.text);
                            if((string)data["provider"]!="overture" || string.IsNullOrEmpty((string)data["release"])) throw new FormatException();
                            if(refresh && (string)data["source"]=="stale_cache") throw new FormatException("LIVE MAP UPDATE UNAVAILABLE");
                            bool unchanged=(bool?)data["notModified"]==true;
                            if(unchanged && (cached==null || (string)cached["release"]!=(string)data["release"])) throw new FormatException();
                            features=Decode((unchanged?cached:data).ToString());
                            source="OVERTURE "+(string)data["release"]+((string)data["source"]=="stale_cache"?" (SAVED; UPDATE UNAVAILABLE)":"");
                            if(!unchanged) SaveOverture(location,data);
                        }
                        catch { status?.Invoke("INVALID OVERTURE SECTOR RESPONSE"); }
                    }
                    if(features!=null) { complete(features,source); yield break; }
                }
            }
            else status?.Invoke("CONFIGURE OVERTURE SERVICE ADDRESS");
            if(cached!=null)
            {
                complete(Decode(cached.ToString()),"OVERTURE "+(string)cached["release"]+" (OFFLINE SAVED COPY)");
                yield break;
            }
            complete(null,"OVERTURE UNAVAILABLE - CHECK MAP SERVICE CONFIGURATION / CONNECTION");
        }

        public static string Endpoint
        {
            get
            {
                var settings=Resources.Load<TextAsset>("MapServiceConfig");
                if(settings==null) return "";
                try
                {
                    var config=JObject.Parse(settings.text);
                    return ((string)config[Application.isEditor?"editorUrl":"androidUrl"] ?? "").Trim();
                }
                catch { return ""; }
            }
        }
        public static IEnumerator LoadOsmSupplement(GameLocation location,string endpoint,Action<List<MapFeature>,string> complete,Action<string> status)
        {
            var servers=new List<string>();
            string[] pool = new string[] { endpoint, "https://lz4.overpass-api.de/api/interpreter", "https://z.overpass-api.de/api/interpreter", "https://overpass-api.de/api/interpreter" };
            foreach(string candidate in pool)
                if(!string.IsNullOrEmpty(candidate) && servers.Count<1 && Uri.TryCreate(candidate,UriKind.Absolute,out var uri) && (uri.Scheme=="https" || uri.Scheme=="http") && !servers.Contains(candidate)) servers.Add(candidate);
            string query=SectorMap.BuildQuery(location);
            foreach(var server in servers)
            {
                using(var request=new UnityWebRequest(server,"POST"))
                {
                    request.uploadHandler=new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes("data="+Uri.EscapeDataString(query)));
                    request.downloadHandler=new DownloadHandlerBuffer(); request.timeout=5;
                    request.SetRequestHeader("Content-Type","application/x-www-form-urlencoded"); request.SetRequestHeader("User-Agent","GeoSniperGame/2.0 (contact: geetasugara99@gmail.com; +https://github.com/geetasugara99)");
                    yield return request.SendWebRequest();
                    if(request.result!=UnityWebRequest.Result.Success || request.downloadedBytes>SectorMap.MaxResponseBytes) continue;
                    try { complete(SectorMap.Parse(request.downloadHandler.text,location),"OSM supplement"); yield break; }
                    catch(FormatException) { }
                }
            }
            complete(null,"OSM supplement unavailable");
        }

        public static IEnumerator Load(GameLocation location,Action<List<MapFeature>,string> complete,Action<string> status,bool refresh=false)
        {
            string url=Endpoint;
            if(!Uri.TryCreate(url,UriKind.Absolute,out var uri) || (uri.Scheme!="https" && !(Application.isEditor && uri.IsLoopback)))
            { status?.Invoke("MAP SERVICE ADDRESS IS NOT CONFIGURED"); complete(null,"UNAVAILABLE"); yield break; }
            status?.Invoke("DOWNLOADING OSM SECTOR...");
            using(var request=new UnityWebRequest(url.TrimEnd('/')+"/v1/sector","POST"))
            {
                var body=new JObject{["latitude"]=Math.Round(location.Latitude,6),["longitude"]=Math.Round(location.Longitude,6)};
                request.uploadHandler=new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(body.ToString(Newtonsoft.Json.Formatting.None)));
                request.downloadHandler=new DownloadHandlerBuffer(); request.timeout=100;
                request.SetRequestHeader("Content-Type","application/json");
                yield return request.SendWebRequest();
                if(request.result==UnityWebRequest.Result.Success && request.downloadedBytes<=SectorMap.MaxResponseBytes)
                {
                    List<MapFeature> features=null; string source="";
                    try
                    {
                        var json=request.downloadHandler.text;
                        var kind=(string)JObject.Parse(json)["source"];
                        if(refresh && kind!="live") throw new FormatException("LIVE MAP UPDATE REQUIRED");
                        features=Decode(json);
                        source=kind=="live"?"LIVE OSM":kind=="stale_cache"?"CACHED OSM (OLDER DATA)":"CACHED OSM";
                        // Cached server responses must not restart the local freshness clock.
                        if(kind=="live") Save(features,location);
                    }
                    catch(Exception) { status?.Invoke("INVALID MAP SERVICE RESPONSE"); }
                    if(features!=null) { complete(features,source); yield break; }
                }
                else status?.Invoke(request.responseCode>0?"MAP SERVICE HTTP "+request.responseCode:"MAP SERVICE UNREACHABLE");
            }
            complete(null,"UNAVAILABLE");
        }
        public static List<MapFeature> Decode(string json)
        {
            var root=JObject.Parse(json);
            if((int?)root["version"]!=2 || !(root["features"] is JArray rows) || rows.Count>SectorMap.MaxFeatures) throw new FormatException("MAP_SCHEMA");
            var result=new List<MapFeature>();
            foreach(var row in rows)
            {
                string kind=(string)row["kind"];
                if(kind!="road" && kind!="building" && kind!="place" && kind!="water" && kind!="park") throw new FormatException("MAP_KIND");
                float height=(float?)row["height"] ?? 12;
                if(float.IsNaN(height) || float.IsInfinity(height)) throw new FormatException("MAP_HEIGHT");
                var feature=new MapFeature{Kind=kind,Name=((string)row["name"] ?? "").Trim(),Landmark=((string)row["landmark"] ?? "").Trim().ToLowerInvariant(),Height=Mathf.Clamp(height,3,MapFeatureStyle.MaxBuildingHeight),Details=MapFeatureStyle.ReadDetails(row["details"])};
                if(!(row["points"] is JArray points) || points.Count<(kind=="road"?2:kind=="place"?4:3) || points.Count>(kind=="water"?WaterGeometry.MaxPoints:400)) throw new FormatException("MAP_POINTS");
                foreach(var point in points)
                {
                    if(!(point is JArray pair) || pair.Count!=2) throw new FormatException("MAP_POINT");
                    float x=(float)pair[0],z=(float)pair[1];
                    if(float.IsNaN(x) || float.IsNaN(z) || float.IsInfinity(x) || float.IsInfinity(z) || Math.Abs(x)>SectorMap.GeometryLimit || Math.Abs(z)>SectorMap.GeometryLimit) throw new FormatException("MAP_BOUNDS");
                    feature.Points.Add(new Vector2(x,z));
                }
                if(kind=="water") feature.WaterRings=WaterGeometry.Decode(row["waterRings"]);
                result.Add(feature);
            }
            if(!result.Exists(f=>f.Kind=="road")) throw new FormatException("MAP_NO_ROADS");
            return result;
        }
        static SectorMapCache Cache=>new SectorMapCache(Path.Combine(Application.persistentDataPath,"osm-sectors-v2"));
        static List<MapFeature> sessionFeatures;
        static GameLocation sessionLocation;

        static List<MapFeature> CloneFeatures(List<MapFeature> source, double dx, double dz)
        {
            if (source == null) return null;
            var list = new List<MapFeature>(source.Count);
            foreach (var f in source)
            {
                var copy = new MapFeature
                {
                    Kind = f.Kind,
                    Name = f.Name,
                    Landmark = f.Landmark,
                    Height = f.Height,
                    Details = f.Details,
                    Points = new List<Vector2>(f.Points.Count)
                };
                for (int i = 0; i < f.Points.Count; i++)
                {
                    copy.Points.Add(new Vector2((float)(f.Points[i].x + dx), (float)(f.Points[i].y + dz)));
                }
                foreach(var ring in f.WaterRings)
                {
                    var shifted=new List<Vector2>();foreach(var p in ring)shifted.Add(new Vector2(p.x+(float)dx,p.y+(float)dz));
                    var clipped=WaterGeometry.Clip(shifted);if(clipped.Count>=3)copy.WaterRings.Add(clipped);
                }
                if(f.Kind=="water" && f.WaterRings.Count>0 && copy.WaterRings.Count==0)continue;
                list.Add(copy);
            }
            return list;
        }

        public static void Save(List<MapFeature> features,GameLocation location)
        {
            sessionFeatures = CloneFeatures(features, 0, 0);
            sessionLocation = location;
            Cache.Save(features,location);
        }

        public static List<MapFeature> ReadPrevious(GameLocation location,TimeSpan? maxAge=null)
        {
            if (sessionFeatures != null && sessionLocation != null && location != null)
            {
                double dz = (sessionLocation.Latitude - location.Latitude) * 111320;
                double dx = (sessionLocation.Longitude - location.Longitude) * Math.Cos(location.Latitude * Math.PI / 180) * 111320;
                double dist = Math.Sqrt(dx * dx + dz * dz);
                if (MapCachePolicy.CanReuse(dist))
                {
                    return CloneFeatures(sessionFeatures, dx, dz);
                }
            }

            var age=maxAge ?? TimeSpan.FromDays(90);
            var cached=Cache.Read(location,age);
            if(cached!=null)
            {
                // Keep the original disk cache origin. Re-anchoring a nearby cache
                // on every read lets repeated small moves drift arbitrarily far.
                sessionFeatures = CloneFeatures(cached, 0, 0);
                sessionLocation = location;
                return cached;
            }

            // Retain old single-sector caches without refreshing their original expiry.
            try
            {
                var info=new FileInfo(Path.Combine(Application.persistentDataPath,"last-osm-sector.json"));
                if(!info.Exists || info.Length>SectorMap.MaxResponseBytes || DateTime.UtcNow-info.LastWriteTimeUtc>age) return null;
                string json=File.ReadAllText(info.FullName);
                var data=JObject.Parse(json);
                if((double?)data["latitude"]!=location.Latitude || (double?)data["longitude"]!=location.Longitude) return null;
                var features=Decode(json);
                if(features!=null)
                {
                    sessionFeatures = CloneFeatures(features, 0, 0);
                    sessionLocation = location;
                }
                return features;
            }
            catch { return null; }
        }
    }
}
