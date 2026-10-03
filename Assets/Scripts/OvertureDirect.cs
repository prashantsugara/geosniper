using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace GeoSniper
{
    public static class OvertureDirect
    {
        const int Zoom=14;
        const string Catalog="https://stac.overturemaps.org/catalog.json";
        const string BaseUrl="https://overturemaps-extras-us-west-2.s3.us-west-2.amazonaws.com/tiles/";
        static string Folder=>Path.Combine(Application.persistentDataPath,"overture-public-tiles-v1");
        static string release;
        static DateTime nextCheck;
        static bool verified;

        public static IEnumerator Load(GameLocation location,Action<List<MapFeature>,string> complete,Action<string> status,bool refresh=false)
        {
            JObject cached=refresh?null:MapServiceClient.ReadOverture(location);
            List<MapFeature> result=null;
            var stack=new Stack<IEnumerator>();
            stack.Push(Fetch(location,cached,features=>result=features,status,refresh));
            float deadline=Time.realtimeSinceStartup+60;
            string error=null;
            // Catch nested parser/download failures so a valid offline copy remains playable.
            while(stack.Count>0)
            {
                if(Time.realtimeSinceStartup>deadline) { error="OVERTURE DOWNLOAD TIMED OUT"; break; }
                var step=stack.Peek(); bool more=false; object current=null;
                try { more=step.MoveNext(); if(more) current=step.Current; }
                catch(Exception exception) { error=exception is FormatException?exception.Message:"DIRECT MAP DOWNLOAD FAILED"; }
                if(error!=null) break;
                if(!more) { (step as IDisposable)?.Dispose(); stack.Pop(); }
                else if(current is IEnumerator nested) stack.Push(nested);
                else yield return current;
            }
            while(stack.Count>0) (stack.Pop() as IDisposable)?.Dispose();
            if(result!=null) complete(result,"OVERTURE TILES "+release+(verified?"":" (SAVED RELEASE)"));
            else if(cached!=null) complete(MapServiceClient.Decode(cached.ToString()),"OVERTURE TILES "+(string)cached["release"]+" (OFFLINE SAVED COPY)");
            else complete(null,error??"OVERTURE UNAVAILABLE - CONNECT TO DOWNLOAD THIS AREA");
        }

        static IEnumerator Fetch(GameLocation location,JObject cached,Action<List<MapFeature>> complete,Action<string> status,bool refresh=false)
        {
            if(release==null)
            {
                try
                {
                    var meta=JObject.Parse(File.ReadAllText(Path.Combine(Folder,"release.json")));
                    string saved=(string)meta["release"];
                    if(ValidRelease(saved)) release=saved;
                    var checkedAt=new DateTime((long)meta["checked"],DateTimeKind.Utc);
                    if(checkedAt<=DateTime.UtcNow && checkedAt.AddHours(1)>DateTime.UtcNow) { nextCheck=checkedAt.AddHours(1); verified=true; }
                }
                catch { }
            }
            if(refresh || DateTime.UtcNow>=nextCheck)
            {
                status?.Invoke("CHECKING OVERTURE RELEASE..."); verified=false;
                using(var request=UnityWebRequest.Get(Catalog))
                {
                    request.SetRequestHeader("User-Agent","GeoSniperGame/2.0 (contact: geetasugara99@gmail.com; +https://github.com/geetasugara99)");
                    request.timeout=5;
                    var operation=request.SendWebRequest();
                    while(!operation.isDone)
                    {
                        if(request.downloadedBytes>1024*1024) { request.Abort(); break; }
                        yield return null;
                    }
                    if(request.result==UnityWebRequest.Result.Success && request.downloadedBytes<=1024*1024)
                    {
                        string candidate=null;
                        try { candidate=(string)JObject.Parse(request.downloadHandler.text)["latest"]; } catch { }
                        if(ValidRelease(candidate))
                        {
                            release=candidate; verified=true;
                            Write(Path.Combine(Folder,"release.json"),Encoding.UTF8.GetBytes(new JObject{["release"]=release,["checked"]=DateTime.UtcNow.Ticks}.ToString()));
                        }
                    }
                }
                nextCheck=DateTime.UtcNow.AddSeconds(verified?3600:60);
            }
            if(refresh && !verified) throw new FormatException("LIVE MAP CHECK FAILED - RETRY WITH INTERNET CONNECTION");
            if(cached!=null && (!verified || ((string)cached["release"]==release && (int?)cached["styleVersion"]==5)))
            {
                release=(string)cached["release"];
                complete(MapServiceClient.Decode(cached.ToString())); yield break;
            }
            if(!ValidRelease(release)) throw new FormatException("OVERTURE RELEASE UNAVAILABLE");
            string pinned=release;
            double dx=455/(111320*Math.Cos(location.Latitude*Math.PI/180)),dy=455/111320;
            var elements=new JArray();
            foreach(string theme in new[]{"transportation","buildings","places","base"})
            {
                string url=BaseUrl+pinned+"/"+theme+".pmtiles";
                byte[] header=null;
                yield return Range(url,0,127,b=>header=b);
                if(Encoding.ASCII.GetString(header,0,7)!="PMTiles" || header[7]!=3 || header[99]!=1 || header[101]<10)
                {
                    if(theme=="transportation" || theme=="buildings") throw new FormatException("UNSUPPORTED OVERTURE ARCHIVE: "+theme);
                    continue;
                }
                int themeZoom = Math.Min(Zoom, (int)header[101]);
                int tMinX = TileX(location.Longitude - dx, themeZoom), tMaxX = TileX(location.Longitude + dx, themeZoom);
                int tMinY = TileY(location.Latitude + dy, themeZoom), tMaxY = TileY(location.Latitude - dy, themeZoom);
                byte[] root=null;
                yield return Range(url,OvertureTileCodec.U64(header,8),OvertureTileCodec.U64(header,16),b=>root=b);
                if(root==null) continue;
                var directory=OvertureTileCodec.Directory(OvertureTileCodec.Inflate(root,header[97]));
                for(int y=tMinY;y<=tMaxY;y++) for(int x=tMinX;x<=tMaxX;x++)
                {
                    status?.Invoke("DOWNLOADING OVERTURE "+theme.ToUpperInvariant()+" TO PHONE...");
                    var entries=directory; ulong id=OvertureTileCodec.TileId(themeZoom,x,y);
                    for(int depth=0;depth<4;depth++)
                    {
                        var entry=OvertureTileCodec.Find(entries,id);
                        if(entry==null) break;
                        byte[] bytes=null;
                        if(entry.Run==0)
                        {
                            yield return Range(url,checked(OvertureTileCodec.U64(header,40)+entry.Offset),entry.Length,b=>bytes=b);
                            entries=OvertureTileCodec.Directory(OvertureTileCodec.Inflate(bytes,header[97]));
                            if(depth==3) throw new FormatException("TILE DIRECTORY DEPTH");
                        }
                        else
                        {
                            yield return Range(url,checked(OvertureTileCodec.U64(header,56)+entry.Offset),entry.Length,b=>bytes=b);
                            AddFeatures(elements,OvertureTileCodec.Decode(OvertureTileCodec.Inflate(bytes,header[98])),x,y,themeZoom,location);
                            break;
                        }
                    }
                    yield return null;
                }
            }
            var features=SectorMap.Parse(new JObject{["elements"]=elements}.ToString(Newtonsoft.Json.Formatting.None),location);
            if(!features.Exists(f=>f.Kind=="road")) throw new FormatException("NO OVERTURE ROADS IN THIS AREA");
            var data=new JObject{["version"]=2,["styleVersion"]=5,["provider"]="overture",["release"]=pinned,["features"]=Rows(features)};
            MapServiceClient.SaveOverture(location,data);
            complete(features);
        }

        static bool ValidRelease(string value)=>value!=null && Regex.IsMatch(value,@"^\d{4}-\d{2}-\d{2}\.\d+$");
        static int TileX(double lon,int zoom=Zoom)=>Math.Max(0,Math.Min((1<<zoom)-1,(int)Math.Floor((lon+180)/360*(1<<zoom))));
        static int TileY(double lat,int zoom=Zoom)
        {
            double r=Math.Max(-85,Math.Min(85,lat))*Math.PI/180;
            return Math.Max(0,Math.Min((1<<zoom)-1,(int)Math.Floor((1-Math.Log(Math.Tan(r)+1/Math.Cos(r))/Math.PI)/2*(1<<zoom))));
        }
        static IEnumerator Range(string url,ulong offset,ulong length,Action<byte[]> complete)
        {
            if(length<1 || length>OvertureTileCodec.Limit) throw new FormatException("TILE RANGE LIMIT");
            string key;
            using(var hash=SHA256.Create()) key=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(url+":"+offset+":"+length))).Replace("-","");
            string path=Path.Combine(Folder,key+".bin"); byte[] saved=null;
            try { if(File.Exists(path) && new FileInfo(path).Length==(long)length) saved=File.ReadAllBytes(path); } catch(IOException) { }
            if(saved!=null) { complete(saved); yield break; }
            using(var request=UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("User-Agent","GeoSniperGame/2.0 (contact: geetasugara99@gmail.com; +https://github.com/geetasugara99)");
                ulong end=checked(offset+length-1);
                request.SetRequestHeader("Range","bytes="+offset+"-"+end); request.timeout=12;
                var operation=request.SendWebRequest();
                while(!operation.isDone)
                {
                    if(request.downloadedBytes>length) { request.Abort(); break; }
                    yield return null;
                }
                if(request.result!=UnityWebRequest.Result.Success || request.responseCode!=206 || request.downloadedBytes!=length
                    || !(request.GetResponseHeader("Content-Range")??"").StartsWith("bytes "+offset+"-"+end+"/",StringComparison.Ordinal))
                    throw new FormatException("OVERTURE RANGE DOWNLOAD FAILED");
                byte[] bytes=request.downloadHandler.data;
                Write(path,bytes); complete(bytes);
            }
        }
        static void Write(string path,byte[] bytes)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllBytes(path+".tmp",bytes);
                if(File.Exists(path)) File.Replace(path+".tmp",path,null); else File.Move(path+".tmp",path);
                var files=new DirectoryInfo(Folder).GetFiles("*.bin");
                Array.Sort(files,(a,b)=>b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                long total=0;
                foreach(var file in files) { total+=file.Length; if(total>128L*1024*1024) file.Delete(); }
            }
            catch(IOException) { }
            catch(UnauthorizedAccessException) { }
        }
        static JArray Rows(List<MapFeature> features)
        {
            var rows=new JArray();
            foreach(var f in features)
            {
                var pts=new JArray(); foreach(var p in f.Points) pts.Add(new JArray(p.x,p.y));
                rows.Add(new JObject{["kind"]=f.Kind,["name"]=f.Name,["landmark"]=f.Landmark,["height"]=f.Height,["points"]=pts,["details"]=f.Details,["waterRings"]=WaterGeometry.Encode(f.WaterRings)});
            }
            return rows;
        }
        static void AddFeatures(JArray elements,List<OvertureTileCodec.Feature> features,int tileX,int tileY,int zoom,GameLocation origin)
        {
            foreach(var f in features)
            {
                string Tag(string key)=>f.Tags.TryGetValue(key,out var value)?value:"";
                string subtype = Tag("subtype");
                if (f.Layer == "segment" && subtype != "road" && subtype != "path" && subtype != "cycleway") continue;
                if (f.Layer == "place" && Tag("operating_status") == "permanently_closed") continue;
                if (f.Layer == "land_use" && subtype != "park" && subtype != "forest" && subtype != "recreation" && subtype != "cemetery" && subtype != "pitch") continue;
                string name = Tag("@name");
                if (string.IsNullOrWhiteSpace(name))
                    foreach (string key in new[] { "name", "name:en", "official_name", "brand" })
                        if (!string.IsNullOrWhiteSpace(Tag(key))) { name = Tag(key); break; }
                var tags = new JObject { ["name"] = name };
                var details = new JObject();
                foreach (string key in new[] { "class", "num_floors", "facade_color", "facade_material", "roof_shape", "roof_color", "roof_material", "roof_height", "min_height", "subtype", "level", "is_bridge" })
                    if (Tag(key).Length > 0) details[key] = Tag(key);
                if (f.Layer == "building" || f.Layer == "building_part")
                {
                    if (Tag("id").Length > 0) details["id"] = Tag("id");
                    if (Tag("building_id").Length > 0) details["building_id"] = Tag("building_id");
                    if (Tag("has_parts").Length > 0) details["has_parts"] = Tag("has_parts");
                    if (f.Layer == "building_part") details["is_part"] = "true";
                }
                details["category"] = Tag("basic_category");
                details["width"] = UnconditionalRule(Tag("width_rules"));
                details["surface"] = UnconditionalRule(Tag("road_surface"));
                tags["_details"] = details;
                if (f.Layer == "building" || f.Layer == "building_part")
                {
                    tags["building"] = "yes";
                    tags["height"] = Tag("height");
                    tags["building:levels"] = Tag("num_floors");
                    if (f.Layer == "building_part") tags["building_part"] = "yes";
                }
                else if (f.Layer == "segment")
                {
                    string roadClass = Tag("class");
                    if (string.IsNullOrEmpty(roadClass)) roadClass = subtype;
                    tags["highway"] = roadClass;
                }
                else if (f.Layer == "water")
                {
                    if(Tag("id").Length>0)details["water_id"]=Tag("id");
                    details["water_class"]=Tag("basic_category");
                    tags["natural"] = "water";
                }
                else if (f.Layer == "land_use")
                {
                    tags["leisure"] = "park";
                }
                else if (f.Layer == "place")
                {
                    string cat = Tag("basic_category");
                    if (cat == "gas_station") tags["amenity"] = "fuel";
                    else if (cat == "police_station") tags["amenity"] = "police";
                    else if (cat == "hospital" || cat == "clinic") tags["amenity"] = "hospital";
                    else if (cat == "bank") tags["amenity"] = "bank";
                    else if (cat == "hotel") tags["tourism"] = "hotel";
                    else if (cat == "restaurant" || cat == "cafe") tags["amenity"] = cat;
                    else tags["shop"] = "yes";
                }
                else tags["shop"] = "yes";
                if(f.Layer=="water" && f.Type==3)
                {
                    var rings=new List<List<Vector2>>();int pointCount=0;
                    foreach(var path in f.Paths)
                    {
                        var local=new List<Vector2>();
                        foreach(var point in ClipPolygon(path,f.Extent))
                        {
                            double lon=(tileX+point[0]/f.Extent)/(1<<zoom)*360-180;
                            double lat=Math.Atan(Math.Sinh(Math.PI*(1-2*(tileY+point[1]/f.Extent)/(1<<zoom))))*180/Math.PI;
                            local.Add(new Vector2((float)((lon-origin.Longitude)*Math.Cos(origin.Latitude*Math.PI/180)*111320),(float)((lat-origin.Latitude)*111320)));
                        }
                        var clipped=WaterGeometry.Clip(local);
                        if(clipped.Count>=3){rings.Add(clipped);pointCount+=clipped.Count;}
                    }
                    if(rings.Count>0 && pointCount<=WaterGeometry.MaxPoints && rings.Count<=128)
                        elements.Add(new JObject{["type"]="water_geometry",["tags"]=tags,["waterRings"]=WaterGeometry.Encode(rings)});
                    continue;
                }
                // Do not fill courtyards with solid buildings. MVT outer rings are positive in tile coordinates.
                if (f.Type == 3 && f.Paths.Exists(p => Area(p) < 0)) continue;
                foreach (var original in f.Type == 2 ? ClipLines(f.Paths, f.Extent) : f.Paths)
                {
                    var path = f.Type == 3 ? ClipPolygon(original, f.Extent) : original;
                    if (path.Count == 0) continue;
                    var geometry = new JArray();
                    foreach (var p in path)
                    {
                        double lon = (tileX + p[0] / f.Extent) / (1 << zoom) * 360 - 180;
                        double lat = Math.Atan(Math.Sinh(Math.PI * (1 - 2 * (tileY + p[1] / f.Extent) / (1 << zoom)))) * 180 / Math.PI;
                        geometry.Add(new JObject { ["lat"] = lat, ["lon"] = lon });
                    }
                    double cx = 0, cz = 0;
                    foreach (JObject p in geometry) { cx += ((double)p["lon"] - origin.Longitude) * Math.Cos(origin.Latitude * Math.PI / 180) * 111320; cz += ((double)p["lat"] - origin.Latitude) * 111320; }
                    if (f.Type != 2 && (Math.Abs(cx / geometry.Count) > 400 || Math.Abs(cz / geometry.Count) > 400)) continue;
                    if (f.Type == 1)
                    {
                        var p = path[0]; if (p[0] < 0 || p[1] < 0 || p[0] >= f.Extent || p[1] >= f.Extent) continue;
                        elements.Add(new JObject { ["type"] = "node", ["tags"] = tags.DeepClone(), ["lat"] = geometry[0]["lat"], ["lon"] = geometry[0]["lon"] });
                    }
                    else
                    {
                        if (f.Type == 3) { if (geometry.Count < 3) continue; geometry.Add(geometry[0].DeepClone()); }
                        elements.Add(new JObject { ["type"] = "way", ["tags"] = tags.DeepClone(), ["geometry"] = geometry });
                    }
                    if (elements.Count > 35000) throw new FormatException("SECTOR FEATURE LIMIT");
                }
            }
        }
        static double Area(List<double[]> p)
        {
            double sum=0; for(int i=0,j=p.Count-1;i<p.Count;j=i++) sum+=p[j][0]*p[i][1]-p[i][0]*p[j][1]; return sum;
        }
        static string UnconditionalRule(string json)
        {
            // Segment-wide rules only: conditional widths must not widen the whole street.
            try
            {
                var rows=JArray.Parse(json);
                foreach(var row in rows)
                {
                    var between=row["between"]; var when=row["when"];
                    bool whole=between==null || between.Type==JTokenType.Null || (between is JArray range && range.Count==2 && (double)range[0]==0 && (double)range[1]==1);
                    if(whole && (when==null || when.Type==JTokenType.Null) && row["value"] is JValue value) return (string)value;
                }
            }
            catch { }
            return "";
        }
        static List<List<double[]>> ClipLines(List<List<double[]>> paths,double extent)
        {
            var result=new List<List<double[]>>();
            foreach(var path in paths)
            {
                List<double[]> run=null;
                for(int i=1;i<path.Count;i++)
                {
                    var a=path[i-1]; var b=path[i]; double start=0,end=1;
                    for(int axis=0;axis<2;axis++)
                    {
                        double d=b[axis]-a[axis];
                        if(Math.Abs(d)<1e-12) { if(a[axis]<0 || a[axis]>extent) end=-1; continue; }
                        double first=-a[axis]/d,last=(extent-a[axis])/d;
                        start=Math.Max(start,Math.Min(first,last)); end=Math.Min(end,Math.Max(first,last));
                    }
                    if(end<=start) { run=null; continue; }
                    var p=new[]{a[0]+start*(b[0]-a[0]),a[1]+start*(b[1]-a[1])};
                    var q=new[]{a[0]+end*(b[0]-a[0]),a[1]+end*(b[1]-a[1])};
                    if(run==null || Math.Abs(run[run.Count-1][0]-p[0])+Math.Abs(run[run.Count-1][1]-p[1])>.001)
                    { run=new List<double[]>{p}; result.Add(run); }
                    run.Add(q);
                }
            }
            return result;
        }
        static List<double[]> ClipPolygon(List<double[]> polygon,double extent)
        {
            var output=polygon;
            for(int edge=0;edge<4;edge++)
            {
                var input=output; output=new List<double[]>(); if(input.Count==0) break;
                int axis=edge/2; double boundary=edge%2==0?0:extent;
                var previous=input[input.Count-1]; bool prevInside=edge%2==0?previous[axis]>=boundary:previous[axis]<=boundary;
                foreach(var point in input)
                {
                    bool inside=edge%2==0?point[axis]>=boundary:point[axis]<=boundary;
                    if(inside!=prevInside)
                    {
                        double t=(boundary-previous[axis])/(point[axis]-previous[axis]);
                        output.Add(new[]{previous[0]+t*(point[0]-previous[0]),previous[1]+t*(point[1]-previous[1])});
                    }
                    if(inside) output.Add(point);
                    previous=point; prevInside=inside;
                }
            }
            return output;
        }
    }
}
