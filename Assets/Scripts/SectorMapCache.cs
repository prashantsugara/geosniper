using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GeoSniper
{
    public sealed class SectorMapCache
    {
        const int Capacity=64;
        const double MaxReuseDistance=MapCachePolicy.MaxReuseDistance;
        readonly string directory;
        public SectorMapCache(string directory) { this.directory=directory; }

        public static string Key(GameLocation location)
        {
            string coordinate=location.Latitude.ToString("F6",CultureInfo.InvariantCulture)+","+location.Longitude.ToString("F6",CultureInfo.InvariantCulture);
            using(var hash=SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(coordinate))).Replace("-","").ToLowerInvariant();
        }

        public void Save(List<MapFeature> features,GameLocation location)
        {
            if(features==null || !features.Exists(f=>f.Kind=="road")) return;
            try
            {
                var rows=new JArray();
                foreach(var feature in features)
                {
                    var points=new JArray();
                    foreach(var p in feature.Points) points.Add(new JArray(p.x,p.y));
                    rows.Add(new JObject{["kind"]=feature.Kind,["name"]=feature.Name ?? "",["landmark"]=feature.Landmark ?? "",["height"]=feature.Height,["points"]=points,["details"]=feature.Details,["waterRings"]=WaterGeometry.Encode(feature.WaterRings)});
                }
                string json=new JObject{
                    ["version"]=2,
                    ["features"]=rows,
                    ["latitude"]=location.Latitude,
                    ["longitude"]=location.Longitude,
                    ["label"]=location.Label ?? ""
                }.ToString(Newtonsoft.Json.Formatting.None);
                if(Encoding.UTF8.GetByteCount(json)>SectorMap.MaxResponseBytes) return;
                Directory.CreateDirectory(directory);
                string path=Path.Combine(directory,"sector-"+Key(location)+".json");
                string temporary=path+".tmp";
                File.WriteAllText(temporary,json);
                if(File.Exists(path)) File.Delete(path);
                File.Move(temporary,path);
                var files=new DirectoryInfo(directory).GetFiles("sector-*.json");
                Array.Sort(files,(a,b)=>b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                long bytes=0;
                for(int i=0;i<files.Length;i++)
                {
                    bytes+=files[i].Length;
                    if(i>=Capacity || bytes>64L*1024*1024) files[i].Delete();
                }
            }
            catch(IOException) { }
            catch(UnauthorizedAccessException) { }
        }

        public List<MapFeature> Read(GameLocation location,TimeSpan maxAge)
        {
            try
            {
                if(!Directory.Exists(directory)) return null;
                var files=new DirectoryInfo(directory).GetFiles("sector-*.json");
                if(files==null || files.Length==0) return null;

                // Prefer the exact sector, then recent sectors
                string exact="sector-"+Key(location)+".json";
                Array.Sort(files,(a,b)=>a.Name==b.Name?0:a.Name==exact?-1:b.Name==exact?1:b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
                
                FileInfo bestFile=null;
                double bestDistance=MaxReuseDistance+.001;
                double bestDx=0, bestDz=0, bestScale=1.0;

                foreach(var file in files)
                {
                    try
                    {
                        if(file.Length>SectorMap.MaxResponseBytes || DateTime.UtcNow-file.LastWriteTimeUtc>maxAge) continue;
                        string json=File.ReadAllText(file.FullName);
                        var root=JObject.Parse(json);
                        double lat=(double)root["latitude"],lon=(double)root["longitude"];
                        if(!LocationResolver.Valid(lat,lon)) continue;

                        double dz=(lat-location.Latitude)*111320;
                        double dx=(lon-location.Longitude)*Math.Cos(location.Latitude*Math.PI/180)*111320;
                        double distance=Math.Sqrt(dx*dx+dz*dz);

                        if(MapCachePolicy.CanReuse(distance) && distance<=bestDistance)
                        {
                            bestDistance=distance;
                            bestFile=file;
                            bestDx=dx;
                            bestDz=dz;
                            bestScale=Math.Cos(location.Latitude*Math.PI/180)/Math.Cos(lat*Math.PI/180);
                            if(distance<1.0) break;
                        }
                    }
                    catch(Exception error) when(error is IOException || error is UnauthorizedAccessException || error is FormatException || error is Newtonsoft.Json.JsonException || error is ArgumentException || error is InvalidCastException)
                    { }
                }

                if(bestFile!=null)
                {
                    string json=File.ReadAllText(bestFile.FullName);
                    var features=MapServiceClient.Decode(json);
                    if(Math.Abs(bestDx)>0.01 || Math.Abs(bestDz)>0.01)
                    {
                        foreach(var feature in features)
                        {
                            for(int i=0;i<feature.Points.Count;i++)
                            {
                                var p=feature.Points[i];
                                feature.Points[i]=new Vector2((float)(p.x*bestScale+bestDx),(float)(p.y+bestDz));
                            }
                            var shiftedRings=new List<List<Vector2>>();
                            foreach(var ring in feature.WaterRings)
                            {
                                var shifted=new List<Vector2>();
                                foreach(var p in ring)shifted.Add(new Vector2((float)(p.x*bestScale+bestDx),(float)(p.y+bestDz)));
                                var clipped=WaterGeometry.Clip(shifted);if(clipped.Count>=3)shiftedRings.Add(clipped);
                            }
                            if(feature.WaterRings.Count>0 && shiftedRings.Count==0)feature.Points.Clear();
                            feature.WaterRings=shiftedRings;
                        }
                    }
                    return features;
                }
                return null;
            }
            catch(IOException) { return null; }
            catch(UnauthorizedAccessException) { return null; }
        }
    }
}
