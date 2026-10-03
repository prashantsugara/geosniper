using System.Collections.Generic;
using UnityEngine;
namespace GeoSniper
{
    public static class StreetPlacement
    {
        static bool SegmentBox(Vector2 a,Vector2 b,Vector2 half)
        {
            float near=0,far=1;Vector2 delta=b-a;
            for(int axis=0;axis<2;axis++)
            {
                if(Mathf.Abs(delta[axis])<.00001f){if(Mathf.Abs(a[axis])>half[axis])return false;continue;}
                float low=(-half[axis]-a[axis])/delta[axis],high=(half[axis]-a[axis])/delta[axis];
                if(low>high){float swap=low;low=high;high=swap;}near=Mathf.Max(near,low);far=Mathf.Min(far,high);if(near>far)return false;
            }
            return true;
        }
        public static bool Clear(List<MapFeature> features,MapFeature ownRoad,Vector2 center,Vector2 along,float width,float length)
        {
            along.Normalize();Vector2 right=new Vector2(along.y,-along.x),half=new Vector2(width*.5f+.15f,length*.5f+.15f);
            Vector2 Local(Vector2 p){p-=center;return new Vector2(Vector2.Dot(p,right),Vector2.Dot(p,along));}
            foreach(var feature in features)
            {
                if(feature==ownRoad || feature.Points.Count<2)continue;
                if(feature.Kind=="road")
                {
                    Vector2 clearance=half+Vector2.one*(MapFeatureStyle.RoadWidth(feature)*.5f);
                    for(int i=1;i<feature.Points.Count;i++)if(SegmentBox(Local(feature.Points[i-1]),Local(feature.Points[i]),clearance))return false;
                }
                else if(feature.Kind=="building" || feature.Kind=="water")
                {
                    bool waterRings=feature.Kind=="water" && feature.WaterRings.Count>0;
                    if(waterRings && WaterGeometry.Contains(feature.WaterRings,center))return false;
                    int ringCount=waterRings?feature.WaterRings.Count:1;
                    for(int ringIndex=0;ringIndex<ringCount;ringIndex++)
                    {
                        var ring=waterRings?feature.WaterRings[ringIndex]:feature.Points;
                        bool inside=false;
                        for(int i=0;i<ring.Count;i++)
                        {
                            Vector2 a=Local(ring[i]),b=Local(ring[(i+1)%ring.Count]);
                            if(SegmentBox(a,b,half))return false;
                            if((a.y>0)!=(b.y>0) && 0<(b.x-a.x)*(-a.y)/(b.y-a.y)+a.x)inside=!inside;
                        }
                        if(!waterRings && inside)return false;
                    }
                }
            }
            return true;
        }
    }
}
