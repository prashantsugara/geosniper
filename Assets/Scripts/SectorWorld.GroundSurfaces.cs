using System.Collections.Generic;
using UnityEngine;
namespace GeoSniper
{
    public sealed partial class SectorWorld
    {
        struct StreetSegment { public Vector2 a,b; public float halfWidth; }
        readonly Dictionary<Vector2Int,List<StreetSegment>> groundRoadCells=new Dictionary<Vector2Int,List<StreetSegment>>();
        void IndexGroundRoads(List<MapFeature> features)
        {
            groundRoadCells.Clear();
            foreach(var f in features)
            {
                if(f.Kind!="road")continue;
                float radius=MapFeatureStyle.RoadWidth(f)*.5f+6f;
                for(int i=1;i<f.Points.Count;i++)
                {
                    var a=f.Points[i-1];var b=f.Points[i];
                    if((a-b).sqrMagnitude<.01f)continue;
                    var segment=new StreetSegment{a=a,b=b,halfWidth=MapFeatureStyle.RoadWidth(f)*.5f};
                    int minX=Mathf.Max(-10,Mathf.FloorToInt((Mathf.Min(a.x,b.x)-radius)/32f));
                    int maxX=Mathf.Min(10,Mathf.FloorToInt((Mathf.Max(a.x,b.x)+radius)/32f));
                    int minY=Mathf.Max(-10,Mathf.FloorToInt((Mathf.Min(a.y,b.y)-radius)/32f));
                    int maxY=Mathf.Min(10,Mathf.FloorToInt((Mathf.Max(a.y,b.y)+radius)/32f));
                    for(int y=minY;y<=maxY;y++)for(int x=minX;x<=maxX;x++)
                    {
                        var key=new Vector2Int(x,y);
                        if(!groundRoadCells.TryGetValue(key,out var list))groundRoadCells[key]=list=new List<StreetSegment>();
                        list.Add(segment);
                    }
                }
            }
        }
        float RoadsideBlend(float x,float z)
        {
            if(!groundRoadCells.TryGetValue(new Vector2Int(Mathf.FloorToInt(x/32f),Mathf.FloorToInt(z/32f)),out var list))return 0;
            var point=new Vector2(x,z);float weight=0;
            foreach(var segment in list)
            {
                var delta=segment.b-segment.a;
                float t=Mathf.Clamp01(Vector2.Dot(point-segment.a,delta)/delta.sqrMagnitude);
                float edge=(point-segment.a-delta*t).magnitude-segment.halfWidth;
                weight=Mathf.Max(weight,1f-Mathf.Clamp01((edge+.5f)/5f));
            }
            return weight;
        }
        static Texture2D soilGroundTex;
        static Texture2D BuildSoilGroundTexture()
        {
            if(soilGroundTex!=null)return soilGroundTex;
            soilGroundTex=new Texture2D(256,256,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Repeat};
            var pixels=new Color[256*256];
            for(int y=0;y<256;y++)for(int x=0;x<256;x++)
            {
                float broad=Mathf.PerlinNoise(x*.028f+3.7f,y*.028f+12.1f);
                float grain=Mathf.PerlinNoise(x*.42f,y*.42f);
                pixels[y*256+x]=Color.Lerp(new Color(.29f,.24f,.18f),new Color(.53f,.43f,.30f),broad*.7f+grain*.3f);
            }
            soilGroundTex.SetPixels(pixels);soilGroundTex.Apply(false);return soilGroundTex;
        }
    }
}
