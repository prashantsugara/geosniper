using System.Collections.Generic;
using UnityEngine;
namespace GeoSniper
{
    // Spatially bounded queries shared by curb and marking generation.
    public sealed class RoadJunctionMask
    {
        struct Segment {public MapFeature Road;public Vector2 A,B;public float HalfWidth;}
        readonly Dictionary<Vector2Int,List<Segment>> cells=new Dictionary<Vector2Int,List<Segment>>();
        static Vector2Int Cell(Vector2 p)=>new Vector2Int(Mathf.FloorToInt(p.x/32),Mathf.FloorToInt(p.y/32));
        public RoadJunctionMask(List<MapFeature> features)
        {
            foreach(var road in features)
            {
                if(road.Kind!="road" || MapFeatureStyle.Path(road))continue;
                float radius=MapFeatureStyle.RoadWidth(road)*.5f+2f;
                for(int i=1;i<road.Points.Count;i++)
                {
                    var a=road.Points[i-1];var b=road.Points[i];
                    var min=Cell(Vector2.Min(a,b)-Vector2.one*radius);var max=Cell(Vector2.Max(a,b)+Vector2.one*radius);
                    var segment=new Segment{Road=road,A=a,B=b,HalfWidth=radius};
                    for(int x=min.x;x<=max.x;x++)for(int y=min.y;y<=max.y;y++)
                    {var cell=new Vector2Int(x,y);if(!cells.TryGetValue(cell,out var list))cells[cell]=list=new List<Segment>();list.Add(segment);}
                }
            }
        }
        static bool Bridge(MapFeature road){string value=MapFeatureStyle.Text(road,"is_bridge");return value=="yes" || value=="true" || value=="1";}
        public bool Occupied(Vector2 point,MapFeature own,Vector2 direction=default)
        {
            if(direction.sqrMagnitude<.001f && own.Points.Count>1)direction=own.Points[1]-own.Points[0];
            direction.Normalize();
            if(!cells.TryGetValue(Cell(point),out var segments))return false;
            foreach(var other in segments)
            {
                if(other.Road==own || Bridge(other.Road)!=Bridge(own) || MapFeatureStyle.Number(other.Road,"level",0)!=MapFeatureStyle.Number(own,"level",0))continue;
                Vector2 ab=other.B-other.A;
                bool sharedNode=(other.A-own.Points[0]).sqrMagnitude<9f ||
                                (other.B-own.Points[0]).sqrMagnitude<9f ||
                                (other.A-own.Points[own.Points.Count-1]).sqrMagnitude<9f ||
                                (other.B-own.Points[own.Points.Count-1]).sqrMagnitude<9f;
                // A branch can leave a mapped road at a shallow angle. Its first
                // stretch still overlaps the through road and needs paint clearance.
                if(!sharedNode && Mathf.Abs(Vector2.Dot(direction,ab.normalized))>.985f)continue;
                float t=Mathf.Clamp01(Vector2.Dot(point-other.A,ab)/Mathf.Max(.001f,ab.sqrMagnitude));
                if((point-other.A-ab*t).sqrMagnitude<other.HalfWidth*other.HalfWidth)return true;
            }
            return false;
        }
        public bool OccupiedAlong(Vector2 from,Vector2 to,MapFeature own,Vector2 direction)
        {
            // Checking only the midpoint leaves short side streets crossing the
            // end of a long curb or edge-paint section.
            return Occupied(Vector2.Lerp(from,to,.1f),own,direction) ||
                Occupied(Vector2.Lerp(from,to,.5f),own,direction) ||
                Occupied(Vector2.Lerp(from,to,.9f),own,direction);
        }
    }
}
