using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace GeoSniper
{
    public sealed class SectorElevation
    {
        public const int Size=65;
        public const float Radius=1600, Step=50;
        const int Zoom=11, Tiles=1<<Zoom;
        public readonly float[] Heights=new float[Size*Size];
        public string Notice="Elevation unavailable - level practice terrain";
        public bool Available;
        public static string LastNotice="Level practice terrain";
        public const string Endpoint="https://s3.amazonaws.com/elevation-tiles-prod/terrarium/";

        // Match the two triangles used by the terrain mesh, including their diagonal.
        public float Sample(float x,float z)
        {
            float gx=Mathf.Clamp((x+Radius)/Step,0,Size-1),gz=Mathf.Clamp((z+Radius)/Step,0,Size-1);
            int ix=Math.Min((int)gx,Size-2), iz=Math.Min((int)gz,Size-2);
            float u=gx-ix,v=gz-iz;
            float a=Heights[iz*Size+ix],b=Heights[iz*Size+ix+1],c=Heights[(iz+1)*Size+ix],d=Heights[(iz+1)*Size+ix+1];
            return u>=v?a+(b-a)*u+(d-b)*v:a+(d-c)*u+(c-a)*v;
        }
        public static float Decode(Color32 rgb) => rgb.r*256+rgb.g+rgb.b/256f-32768;
        public static Vector2 Pixel(double lat,double lon)
        {
            lat=Math.Max(-85.05,Math.Min(85.05,lat));
            double sin=Math.Sin(lat*Math.PI/180);
            return new Vector2((float)((lon+180)/360*Tiles*256),
                (float)((.5-Math.Log((1+sin)/(1-sin))/(4*Math.PI))*Tiles*256));
        }
        static Vector2Int Key(int x,int y) => new Vector2Int((((int)Math.Floor(x/256.0))%Tiles+Tiles)%Tiles,Math.Max(0,Math.Min(Tiles-1,y/256)));
        static string TilePath(Vector2Int key) => Path.Combine(Application.persistentDataPath,"elevation-tiles",Zoom+"-"+key.x+"-"+key.y+".png");
        static byte[] ReadTile(Vector2Int key)
        {
            try { var f=new FileInfo(TilePath(key)); return f.Exists && f.Length<1000000 && DateTime.UtcNow-f.LastWriteTimeUtc<TimeSpan.FromDays(30)?File.ReadAllBytes(f.FullName):null; }
            catch { return null; }
        }
        static void SaveTile(Vector2Int key,byte[] bytes)
        {
            try
            {
                var dir=Directory.CreateDirectory(Path.GetDirectoryName(TilePath(key)));
                File.WriteAllBytes(TilePath(key),bytes);
                var files=dir.GetFiles("*.png"); Array.Sort(files,(a,b)=>a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
                for(int i=0;i<files.Length-16;i++) files[i].Delete();
            }
            catch(IOException) { }
            catch(UnauthorizedAccessException) { }
        }
        static Color32[] ReadPixels(byte[] data)
        {
            if(data==null || data.Length>1000000) return null;
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            try
            {
                if(!ImageConversion.LoadImage(texture,data,false) || texture.width!=256 || texture.height!=256) return null;
                return texture.GetPixels32();
            }
            catch { return null; }
            finally { UnityEngine.Object.Destroy(texture); }
        }
        static float HeightAt(Dictionary<Vector2Int,Color32[]> tiles,int x,int y)
        {
            int px=(x%256+256)%256,py=(y%256+256)%256;
            // PNG rows start north; Unity texture pixel rows start at the bottom.
            return Decode(tiles[Key(x,y)][(255-py)*256+px]);
        }
        public static IEnumerator Load(GameLocation location,Action<string> status,Action<SectorElevation> complete)
        {
            var field=new SectorElevation();
            var pixels=new Vector2[Size*Size]; var needed=new HashSet<Vector2Int>();
            double longitudeScale=111320*Math.Cos(location.Latitude*Math.PI/180);
            if(Math.Abs(location.Latitude)>85 || longitudeScale<1) { complete(field); yield break; }
            for(int z=0;z<Size;z++) for(int x=0;x<Size;x++)
            {
                Vector2 p=Pixel(location.Latitude+(z*Step-Radius)/111320.0,location.Longitude+(x*Step-Radius)/longitudeScale);
                pixels[z*Size+x]=p;
                int ix=(int)Math.Floor(p.x),iy=(int)Math.Floor(p.y);
                for(int dz=0;dz<=1;dz++) for(int dx=0;dx<=1;dx++) needed.Add(Key(ix+dx,iy+dz));
            }
            var tiles=new Dictionary<Vector2Int,Color32[]>();
            float deadline=Time.realtimeSinceStartup+30;
            bool downloaded=false;
            foreach(var key in needed)
            {
                status("LOADING LOCAL ELEVATION...");
                byte[] data=ReadTile(key); var values=ReadPixels(data);
                if(values==null && Time.realtimeSinceStartup<deadline)
                {
                    using(var request=UnityWebRequest.Get(Endpoint+Zoom+"/"+key.x+"/"+key.y+".png"))
                    {
                        request.timeout=Math.Max(1,Math.Min(12,(int)(deadline-Time.realtimeSinceStartup)));
                        yield return request.SendWebRequest();
                        if(request.result==UnityWebRequest.Result.Success && request.downloadedBytes<1000000)
                        {
                            data=request.downloadHandler.data; values=ReadPixels(data);
                            if(values!=null) { SaveTile(key,data); downloaded=true; }
                        }
                    }
                }
                if(values==null) { complete(field); yield break; }
                tiles.Add(key,values);
            }
            for(int i=0;i<pixels.Length;i++)
            {
                Vector2 p=pixels[i]; int x=(int)Math.Floor(p.x),y=(int)Math.Floor(p.y);
                float h=Mathf.Lerp(Mathf.Lerp(HeightAt(tiles,x,y),HeightAt(tiles,x+1,y),p.x-x),
                    Mathf.Lerp(HeightAt(tiles,x,y+1),HeightAt(tiles,x+1,y+1),p.x-x),p.y-y);
                if(h< -11000 || h>9000 || float.IsNaN(h)) { complete(new SectorElevation()); yield break; }
                field.Heights[i]=h;
            }
            float center=field.Heights[(Size/2)*Size+Size/2];
            for(int i=0;i<field.Heights.Length;i++) field.Heights[i]-=center;
            field.Available=true;
            field.Notice=(downloaded?"Local terrain":"Cached local terrain")+" - approximate 50 m elevation grid";
            complete(field);
        }
    }
}
