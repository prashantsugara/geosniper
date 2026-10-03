using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace GeoSniper
{
    public static class WaterGeometry
    {
        public const int MaxPoints=2048;
        public static List<Vector2> Clip(List<Vector2> input)
        {
            var points=new List<Vector2>(input);
            for(int edge=0;edge<4;edge++)
            {
                var output=new List<Vector2>(); if(points.Count==0) break;
                int axis=edge/2; float bound=edge%2==0?-320:320;
                var previous=points[points.Count-1]; bool prevInside=edge%2==0?previous[axis]>=bound:previous[axis]<=bound;
                foreach(var current in points)
                {
                    bool inside=edge%2==0?current[axis]>=bound:current[axis]<=bound;
                    if(inside!=prevInside) output.Add(Vector2.LerpUnclamped(previous,current,(bound-previous[axis])/(current[axis]-previous[axis])));
                    if(inside) output.Add(current);
                    previous=current;prevInside=inside;
                }
                points=output;
            }
            for(int i=points.Count-1;i>=0 && points.Count>1;i--)
                if((points[i]-points[(i+1)%points.Count]).sqrMagnitude<.000001f) points.RemoveAt(i);
            return points;
        }
        public static List<Vector2> Ribbon(List<Vector2> centerline,float width)
        {
            if(centerline==null || centerline.Count<2)return new List<Vector2>();
            var left=new List<Vector2>();var right=new List<Vector2>();
            float half=Mathf.Clamp(width,2f,60f)*.5f;
            for(int i=0;i<centerline.Count;i++)
            {
                Vector2 before=centerline[Mathf.Max(0,i-1)],after=centerline[Mathf.Min(centerline.Count-1,i+1)];
                var tangent=(after-before).normalized;
                if(tangent.sqrMagnitude<.001f)continue;
                var normal=new Vector2(-tangent.y,tangent.x)*half;
                left.Add(centerline[i]+normal);right.Add(centerline[i]-normal);
            }
            right.Reverse();left.AddRange(right);
            return Clip(left);
        }
        public static JArray Encode(List<List<Vector2>> rings)
        {
            var result=new JArray();foreach(var ring in rings){var row=new JArray();foreach(var p in ring) row.Add(new JArray(p.x,p.y));result.Add(row);}return result;
        }
        public static List<List<Vector2>> Decode(JToken token)
        {
            var result=new List<List<Vector2>>(); if(token==null) return result;
            if(!(token is JArray rings) || rings.Count>128) throw new FormatException("WATER_RINGS");
            int count=0;
            foreach(var row in rings)
            {
                if(!(row is JArray points) || points.Count<3 || (count+=points.Count)>MaxPoints) throw new FormatException("WATER_POINTS");
                var ring=new List<Vector2>();foreach(var item in points)
                {
                    if(!(item is JArray pair) || pair.Count!=2) throw new FormatException("WATER_POINT");
                    float x=(float)pair[0],y=(float)pair[1];
                    if(!float.IsFinite(x)||!float.IsFinite(y)||Math.Abs(x)>320.01f||Math.Abs(y)>320.01f) throw new FormatException("WATER_BOUNDS");
                    ring.Add(new Vector2(x,y));
                }
                result.Add(ring);
            }
            return result;
        }
        public static bool Contains(List<List<Vector2>> rings,Vector2 point)
        {
            bool inside=false;
            foreach(var ring in rings) for(int i=0,j=ring.Count-1;i<ring.Count;j=i++)
            {
                var a=ring[i];var b=ring[j];
                if((a.y>point.y)!=(b.y>point.y) && point.x<(b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x) inside=!inside;
            }
            return inside;
        }
        public static float DistanceToBoundary(List<List<Vector2>> rings,Vector2 point,float limit)
        {
            float bestSquared=limit*limit;
            foreach(var ring in rings)for(int i=0;i<ring.Count;i++)
            {
                var a=ring[i];var b=ring[(i+1)%ring.Count];var delta=b-a;
                float t=delta.sqrMagnitude>.000001f?Mathf.Clamp01(Vector2.Dot(point-a,delta)/delta.sqrMagnitude):0f;
                bestSquared=Mathf.Min(bestSquared,(point-a-delta*t).sqrMagnitude);
            }
            return Mathf.Sqrt(bestSquared);
        }
        struct Edge {public Vector2 a,b;public float At(float y)=>a.x+(b.x-a.x)*(y-a.y)/(b.y-a.y);}
        // Even/odd scanline tessellation retains concavity, disconnected outlines and island holes.
        public static List<Vector2> Triangles(List<List<Vector2>> rings)
        {
            var ys=new SortedSet<float>();var edges=new List<Edge>();int count=0;
            foreach(var ring in rings)
            {
                if((count+=ring.Count)>MaxPoints) throw new FormatException("WATER_COMPLEXITY");
                for(int i=0;i<ring.Count;i++){var a=ring[i];var b=ring[(i+1)%ring.Count];ys.Add(a.y);if(Mathf.Abs(a.y-b.y)>.00001f)edges.Add(new Edge{a=a,b=b});}
            }
            var levels=new List<float>(ys);var result=new List<Vector2>();var active=new List<Edge>();
            for(int i=1;i<levels.Count;i++)
            {
                float low=levels[i-1],high=levels[i],mid=(low+high)*.5f;if(high-low<.0001f)continue;
                active.Clear();foreach(var edge in edges)if(mid>Mathf.Min(edge.a.y,edge.b.y)&&mid<Mathf.Max(edge.a.y,edge.b.y))active.Add(edge);
                active.Sort((a,b)=>a.At(mid).CompareTo(b.At(mid)));
                if(active.Count%2!=0) throw new FormatException("WATER_TOPOLOGY");
                for(int j=0;j<active.Count;j+=2)
                {
                    var a=new Vector2(active[j].At(low),low);var b=new Vector2(active[j].At(high),high);
                    var c=new Vector2(active[j+1].At(high),high);var d=new Vector2(active[j+1].At(low),low);
                    result.AddRange(new[]{a,b,c,a,c,d});
                    if(result.Count>60000) throw new FormatException("WATER_MESH_BUDGET");
                }
            }
            return result;
        }
    }
}
